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

## Layer generation (`PopupLayerEditorPanel`)

Edits `PopupLayerEnum` names, validates to `UPPER_CASE` (spaces/dashes → `_`), and **rewrites the whole**
`PopupLayerEnum.generated.cs` (`None = 0`, then `1 << bit`, max 31) via `FileSearcher.LayersEnumFilePath` +
`AssetDatabase.Refresh`. **Auto-Save** toggle persists in `PlayerPrefs` (`APS_AutoSaveEnabled`). Hand-editing the file
is futile — it's regenerated ([[Invariants]], [[Layers]]).

## Display generation (`PopupDisplaysEditorPanel`)

Lists subfolders of `FileSearcher.DisplaysFolderPath` ending in `Display`. **Add** writes
`<Name>Display/<Name>Display.generated.cs` + `<Name>Settings.generated.cs` from string templates — **only if the folder
doesn't exist** (never overwrites your bodies). **Delete** removes the folder (confirm dialog). Names validated to
letters-only, then `RemoveDisplaySuffix` + `"Display"`/`"Settings"` ([[Displays & Animations]], [[Display — Custom]]).

## FileSearcher (paths)

Locates the package by folder name `advanced-popup-system` (AssetDatabase in editor; `dataPath` at runtime) and exposes
`DisplaysFolderPath`, `LayersEnumFilePath`, `ImagesFolderPath`; `ToAssetPath`/`ToFsPath` convert between filesystem and
`Assets/...` paths. **`FolderRenamePrevention`** (an `AssetPostprocessor`) reverts any rename of that folder — the
lookup keys off the name, so renaming would break codegen/images.

## Other editor pieces

- **`CreateAdvancedPopup`** — `GameObject ▸ UI ▸ Advanced Popup`: builds a stretched `RectTransform` GameObject with
  `Image` + `AdvancedPopup`, creating a `Canvas` (ScreenSpaceOverlay + scaler + raycaster) if none is in the parent.
- **Custom inspectors:** `IAdvancedPopupEditor` + `BaseSettingsDrawer` render the popup/settings; `InspectorEnum`
  ([[Settings & Logging]]) switches between the APS view, an optimized view, and Unity's default. `CustomHierarchyrIcon`
  adds the hierarchy icon; `APSEditorStyles`/`EditorGUILayoutExtensions` are shared GUI helpers.

## Depends on

- [[Layers]] & [[Displays & Animations]] (what gets generated), [[Settings & Logging]] (the Settings tab)
