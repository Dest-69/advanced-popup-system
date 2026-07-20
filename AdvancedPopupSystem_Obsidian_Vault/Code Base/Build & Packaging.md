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
| Custom displays | consumer's own `<Name>Display/` folders | not in the package → import never deletes them |
| `PopupLayerEnum.generated.cs` | ships (needed at compile time) | shipped as a **clean default**, then healed from the store |
| `AddressablePopupIndex.generated.cs` | ships (needed at compile time) | shipped **empty**; rescanned from the consumer's prefabs |

**Why the two generated files must still ship:** both are compile-time dependencies of the public API
(`LayerShow(PopupLayerEnum)`, `AddressablePopupIndex.Entries`). Omitting them breaks the *fresh*-install compile before
any generator could run, so "generate only at startup" is impossible for them — ship a clean default and protect it from
overwrite instead (layer heal; index rescan).

## `FileSearcher` default-content safety net

When a generated file is missing, `FileSearcher` seeds a **compilable** default (never an empty `.cs` — that removes the
`PopupLayerEnum` type / the `Entries` array and hard-fails compilation). `DefaultLayersEnumContent` = `None`-only enum;
`DefaultAddressableIndexContent` = empty index. Guarded `#if UNITY_EDITOR` (only meaningful in the editor).

## The exporter (`APSPackageExporter`)

Dev-only `EditorWindow` at `APS ▸ Build ▸ Export Package…`, in `Editor/Build/` — **excludes itself** from the package.
Pipeline:

1. **Rebuild sample sub-packages** — each `Samples/<Showcase>/` → `Samples/<Showcase>.unitypackage` via
   `ExportPackage(..., Recurse)` **without** dependencies. The package ships those `.unitypackage` files, **not** the raw
   sample sources (kept out by the deny-list); `Samples/Utils/` ships raw ([[Samples]]).
2. **Optional `package.json` bump** — behind a checkbox (version bumps are user-gated, [[Invariants]]); otherwise the
   entered version only names the output file.
3. **Stage clean generated defaults** — `SuppressReconcile = true`, then write the default `PopupLayerEnum`
   (`LayerCatalog.DefaultLayerNames`) and the empty index; **restored in a `finally`** so the dev project is never left
   on the defaults.
4. **Collect allow-list** — every asset under `advanced-popup-system/` **minus** the deny-list: the Obsidian vault,
   `CLAUDE.md`, `Editor/Build/` (the exporter), and raw sample sources. `.git`/`.github`/`.agents`/`.claude` are
   dot-folders Unity already ignores.
5. **Export** with `ExportPackageOptions.Default` (**no `IncludeDependencies`**) → only files under the package, zero
   third-party deps → `Assets/Development/AdvancedPS_v<version>.unitypackage`.

**Gotcha (transition update):** the heal only runs if the *previously installed* version already carried
`LayerEnumSyncPostprocessor`. A consumer updating **from a pre-heal version to the first version that adds it** can lose
custom layers once (the old assembly has no heal); every update after that is safe. Note it in `CHANGELOG`.

## Depends on

- [[Layers]] (the layer store + heal), [[Settings & Logging]] (the `AP_Settings.json` precedent), [[Project Map]]
  (assemblies, what ships), [[Invariants]] (version/API gates, generated-code rules).
