---
type: code
status: active
description: APS editor window (Layers/Displays/Settings), the layer-enum & display code generation, FileSearcher paths, the create-popup menu, custom inspectors, and package-folder guards. Read when changing tooling, codegen, or inspectors.
code_paths:
  - Assets/advanced-popup-system/Editor/
  - Assets/advanced-popup-system/Runtime/Utils/FileSearcher.cs
---

# Editor & Codegen

## APS window

`PopupSystemEditor` (`AdvancedPS.Editor`) — one window, three tabs, opened from the top **`APS/`** menu
(`APS/Layers`, `APS/Displays`, `APS/Settings`). Title shows the version from `PackageVersionHelper` (resolves via
PackageManager or a walked `package.json`; `"Dev"` fallback).

## Layer generation (`PopupLayerEditorPanel` → `LayerCatalog`)

The panel edits names and validates to `UPPER_CASE` (spaces/dashes → `_`); **Auto-Save** persists in `PlayerPrefs`
(`APS_AutoSaveEnabled`). All persistence/codegen is centralized in **`LayerCatalog`** (the single codegen path):

- **Source of truth is external** — `LayerCatalog.SaveNames` writes the ordered list to `ProjectSettings/APS_Layers.json`
  (outside the package, never shipped, never clobbered by import). `PopupLayerEnum.generated.cs` is a *projection*.
- **`GenerateEnumSource`** builds the enum (`None = 0`, then `1 << bit`, max 31); **`Reconcile`** rewrites the file
  from the store only when content differs (idempotent — no needless recompile).
- **`LayerEnumSyncPostprocessor`** heals the enum after an import: `OnPostprocessAllAssets` runs on the *already-loaded*
  editor assembly **before** the imported scripts recompile, so restoring the store's layers happens in time for
  consumer code that references them to compile. `[InitializeOnLoadMethod]` is a secondary post-reload safety net; a
  `SuppressReconcile` flag lets the exporter stage clean defaults without the heal fighting it. This is the
  **non-destructive-update** mechanism ([[Layers]], [[Build & Packaging]]).

Hand-editing the file is futile — it's regenerated from the store ([[Invariants]], [[Layers]]).

## Display generation (`PopupDisplaysEditorPanel`)

Lists subfolders of `FileSearcher.DisplaysFolderPath` ending in `Display`. **Add** writes
`<Name>Display/<Name>Display.generated.cs` + `<Name>Settings.generated.cs` from string templates — **only if the folder
doesn't exist** (never overwrites your bodies). **Delete** removes the folder (confirm dialog). Names validated to
letters-only, then `RemoveDisplaySuffix` + `"Display"`/`"Settings"` ([[Displays & Animations]], [[Display — Custom]]).

## FileSearcher (paths)

Locates the package by folder name `advanced-popup-system` (AssetDatabase in editor; `dataPath` at runtime) and exposes
`DisplaysFolderPath`, `LayersEnumFilePath`, `AddressableIndexFilePath`, `ImagesFolderPath`; `ToAssetPath`/`ToFsPath`
convert between filesystem and `Assets/...` paths. **`FolderRenamePrevention`** (an `AssetPostprocessor`) reverts any rename of that folder — the
lookup keys off the name, so renaming would break codegen/images.

## Addressable index generation (`AddressablePopupIndexGenerator`, optional)

Under `APS_ADDRESSABLES` (`Editor/Addressables/`). Scans `t:Prefab` for `IAdvancedPopup.Addressable`, keeps them in the
**"Advanced Popup System"** Addressables group (address = type `FullName`, one prefab per type — warns on duplicates),
prunes un-flagged entries, and rewrites `AddressablePopupIndex.generated.cs` via `FileSearcher.AddressableIndexFilePath`
— **idempotently** (no rewrite/recompile unless content changed). Runs from `Tools/Advanced Popup System/Regenerate
Addressable Index` and **auto** via `AddressablePopupPostprocessor` on `.prefab` changes (deferred out of the import
callback). The inspector's Addressable box lives in `IAdvancedPopupEditor`. See [[Addressables]].

## Other editor pieces

- **`CreateAdvancedPopup`** — `GameObject ▸ UI ▸ Advanced Popup`: builds a stretched `RectTransform` GameObject with
  `Image` + `AdvancedPopup`, creating a `Canvas` (ScreenSpaceOverlay + scaler + raycaster) if none is in the parent.
- **Custom inspectors:** `IAdvancedPopupEditor` + `BaseSettingsDrawer` render the popup/settings; `InspectorEnum`
  ([[Settings & Logging]]) switches between the APS view, an optimized view, and Unity's default. `CustomHierarchyrIcon`
  adds the hierarchy icon; `APSEditorStyles`/`EditorGUILayoutExtensions` are shared GUI helpers.

## Depends on

- [[Layers]] & [[Displays & Animations]] (what gets generated), [[Settings & Logging]] (the Settings tab)
