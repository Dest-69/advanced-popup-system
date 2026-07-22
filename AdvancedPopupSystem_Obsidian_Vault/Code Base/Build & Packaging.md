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
| `PopupLayerEnum.generated.cs` | `Assets/AdvancedPopupSystem/Generated/Layers/` (consumer, own asmdef) | outside the package; seeded by `AdvancedPS.Bootstrap` on fresh install, then healed from the store |
| Addressable index | `Assets/Resources/APS_AddressablePopupIndex.asset` (consumer project) | outside the package; rescanned from the consumer's prefabs |

**Why the enum moves to the consumer (and needs a bootstrap):** `PopupLayerEnum` is a compile-time dependency of the
public API (`LayerShow(PopupLayerEnum)`), so it must exist in an assembly core references. A read-only UPM package
(`Library/PackageCache`) can't be regenerated, so the enum can't live *in* the package if consumers are to add layers —
it lives in the consumer project (`Assets/AdvancedPopupSystem/Generated/Layers/`, assembly `AdvancedPS.Generated.Layers`,
which core references by name). That creates a fresh-install chicken-and-egg (core references an assembly that doesn't
exist yet → core won't compile → the APS tooling that would create it can't run). The **`AdvancedPS.Bootstrap`** editor
assembly (`Editor/Bootstrap/`, **no core references**) resolves it: it compiles and runs even while core is red, seeds
the assembly (asmdef + enum, from the store when present, else defaults), and triggers the recompile that unblocks core.
Cost: one brief self-healing compile pass on first install. The Addressable index has no such constraint — it is
**data** (`AddressablePopupIndexAsset`, read at runtime), rescanned from the consumer's prefabs.

## `FileSearcher` / bootstrap default-content safety net

When the generated `PopupLayerEnum` file is missing, a **compilable** default is seeded (never an empty `.cs` — that
removes the `PopupLayerEnum` type and hard-fails compilation): `DefaultLayersEnumContent` = `None`-only enum. Two writers
enforce this and must stay in sync: `FileSearcher.LayersEnumFilePath` (when core is alive) and `AdvancedPS.Bootstrap`
(on a fresh install, before core compiles — it prefers the store's names over the bare default). Both only write when
the file is absent, so they never fight — first writer wins. The Addressable index needs no such seed (data asset →
missing asset is an empty catalog).

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
3. **No enum staging** — `PopupLayerEnum` is generated consumer-side now, not under the package, so there is nothing to
   reset/restore (the old `SuppressReconcile` + default-write + `finally`-restore dance is gone). `Editor/Bootstrap/`
   ships so consumers self-heal on import.
4. **Collect allow-list** — every asset under `advanced-popup-system/` **minus** the deny-list: the Obsidian vault,
   `CLAUDE.md`, `Editor/Build/` (the exporter), and raw sample sources. `.git`/`.github`/`.agents`/`.claude` are
   dot-folders Unity already ignores. The consumer-side `Assets/AdvancedPopupSystem/Generated/` is outside the package
   root, so it is excluded automatically.
5. **Export** with `ExportPackageOptions.Default` (**no `IncludeDependencies`**) → only files under the package, zero
   third-party deps → `Assets/Development/AdvancedPS_v<version>.unitypackage`.

**Gotcha (migration to the consumer-side enum):** on update, the package stops shipping the in-package
`PopupLayerEnum`; the bootstrap recreates it under `Assets/AdvancedPopupSystem/Generated/Layers/` **from
`ProjectSettings/APS_Layers.json`**, so custom layers survive as long as that store exists (it has since the store was
introduced). A consumer coming from a *pre-store* version (no `APS_Layers.json`) gets defaults once — they re-add layers
via the panel, which writes the store, and every update after is safe. The old in-package
`Runtime/Generated/PopupLayerEnum.generated.cs` is simply left orphaned by the update (it is no longer part of the
package); a consumer can delete it. Note the new install location in `CHANGELOG`.

## Depends on

- [[Layers]] (the layer store + heal), [[Settings & Logging]] (the `AP_Settings.json` precedent), [[Project Map]]
  (assemblies, what ships), [[Invariants]] (version/API gates, generated-code rules).
