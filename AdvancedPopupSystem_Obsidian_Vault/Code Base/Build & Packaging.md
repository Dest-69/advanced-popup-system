---
type: code
status: active
tags: [tree/build]
description: How a distributable .unitypackage is built (APSPackageExporter) and the non-destructive-update architecture — what state ships vs. lives in the consumer project vs. self-generates. Read before changing the exporter, what ships, or the update-safety mechanism.
code_paths:
  - Assets/advanced-popup-system/Editor/Build/APSPackageExporter.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/LayerCatalog.cs
  - Assets/advanced-popup-system/Editor/LayerEnumSyncPostprocessor.cs
  - Assets/advanced-popup-system/Runtime/Utils/FileSearcher.cs
---

# Build & Packaging

## Non-destructive updates — the core rule

A `.unitypackage` import **overwrites every file it contains** (by GUID) and **deletes nothing** absent from it. So any
*consumer* state that both (a) lives inside `advanced-popup-system/` and (b) ships in the package gets clobbered on
update. The fix is to keep consumer state **out of the shipped package**, and to ship only clean, rebuildable defaults.

| State | Where it lives | Update-safe because |
|-------|----------------|---------------------|
| Settings | `Assets/Resources/AP_Settings.json` (consumer project) | outside the package ([[Settings & Logging]]) |
| Layer set | `ProjectSettings/APS_Layers.json` (consumer project) | outside the package; enum is healed from it ([[Layers]]) |
| Custom displays | `Assets/AdvancedPopupSystem/Generated/Displays/` (consumer, own asmdef) | outside the package → import never touches them |
| `PopupLayerEnum.generated.cs` | **ships in the package** (`Runtime/Generated/Layers/`, own assembly) | a compile-time type must ship; it's a rebuildable projection of the store — reset to the default set on export, healed in place when the package is writable |
| Addressable index | `Assets/Resources/APS_AddressablePopupIndex.asset` (consumer project) | outside the package; rescanned from the consumer's prefabs |

**Why the enum must ship (and can't be consumer-side):** `PopupLayerEnum` is a compile-time dependency of the public API
(`LayerShow(PopupLayerEnum)`), so it must exist in an assembly core references, and it must exist **the instant the
package is imported** — no code can create it first, because Unity won't complete a domain reload while core has a
compile error. So it ships inside the package (assembly `AdvancedPS.Generated.Layers`). Two ways to instead put it in the
consumer were tried and **proven impossible** (see [[Invariants]]): a fresh-install *bootstrap* (can't run — core is red)
and a *same-name define-swap* (Unity rejects duplicate assembly names even when define-constrained apart). The cost of
shipping it: editing layers needs a **writable** package, so a read-only UPM install must be **embedded** first (the
Layers panel's Customization toggle offers it — [[Layers]]). The Addressable index has no such constraint — it is **data**
(`AddressablePopupIndexAsset`, read at runtime), rescanned from the consumer's prefabs.

## `FileSearcher` default-content safety net

The shipped `PopupLayerEnum` file must always be **compilable** (never an empty `.cs` — that removes the type and
hard-fails compilation). `LayerCatalog` regenerates it from the store (else `DefaultLayerNames`) only into a **writable**
package; a read-only install keeps the shipped default. The Addressable index needs no such seed (data asset → missing
asset is an empty catalog).

## Package location (`FileSearcher`)

Read-only package assets (images, built-in displays) are resolved via
`UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FileSearcher).Assembly)` — works whether APS is under
`Packages/` (UPM) or `Assets/` (embedded/loose), with a folder-name search as fallback. The old eager static-ctor that
threw `TypeInitializationException` when the folder wasn't found (poisoning every icon site + the heal under UPM) is
gone: accessors are lazy and return `null` on failure. `FolderRenamePrevention` still guards the folder-name fallback.

## The exporter (`APSPackageExporter`)

Dev-only `EditorWindow` at `APS ▸ Build ▸ Export Package…`, in `Editor/Build/` — **excludes itself** from the package.
Pipeline:

1. **Rebuild sample sub-packages** — each `Samples/<Showcase>/` → `Samples/<Showcase>.unitypackage` via
   `ExportPackage(..., Recurse)` **without** dependencies. The package ships those `.unitypackage` files, **not** the raw
   sample sources (kept out by the deny-list); `Samples/Utils/` ships raw ([[Samples]]).
2. **Optional `package.json` bump** — behind a checkbox (version bumps are user-gated, [[Invariants]]); otherwise the
   entered version only names the output file.
3. **Reset the shipped enum to the default set** — `PopupLayerEnum` ships in the package, so the exporter should stage
   the clean `DefaultLayerNames` (`SuppressReconcile` on so the heal doesn't fight it) and restore the dev's working enum
   in a `finally`, so a dev's custom layers never ship. (The dev repo currently just keeps the default set; re-add the
   staging if that changes.)
4. **Collect allow-list** — every asset under `advanced-popup-system/` **minus** the deny-list: the Obsidian vault,
   `CLAUDE.md`, `Editor/Build/` (the exporter), and raw sample sources. `.git`/`.github`/`.agents`/`.claude` are
   dot-folders Unity already ignores. The consumer-side `Assets/AdvancedPopupSystem/Generated/` is outside the package
   root, so it is excluded automatically.
5. **Export** with `ExportPackageOptions.Default` (**no `IncludeDependencies`**) → only files under the package, zero
   third-party deps → `Assets/Development/AdvancedPS_v<version>.unitypackage`.

**Gotcha (1.x → 2.0 migration):** 1.x shipped the enum at `Runtime/Generated/PopupLayerEnum.generated.cs` (compiled into
core); 2.0 ships it at `Runtime/Generated/Layers/PopupLayerEnum.generated.cs` (its own assembly). A **UPM** update
replaces the package wholesale — fine. A **`.unitypackage`/Assets** update leaves the old file orphaned, and it would
**double-define** `PopupLayerEnum` (old-in-core + new-in-Layers-assembly) → the consumer must delete the old
`Runtime/Generated/PopupLayerEnum.generated.cs`. Custom layers themselves survive via `ProjectSettings/APS_Layers.json`
(healed on a writable install). Noted in `CHANGELOG`.

## Depends on

- [[Layers]] (the layer store + heal), [[Settings & Logging]] (the `AP_Settings.json` precedent), [[Project Map]]
  (assemblies, what ships), [[Invariants]] (version/API gates, generated-code rules).
