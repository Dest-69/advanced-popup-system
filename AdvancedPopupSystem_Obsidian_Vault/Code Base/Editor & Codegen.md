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

The panel edits names and validates to `UPPER_CASE` (spaces/dashes → `_`; ASCII letters/digits/underscore, no leading
digit — `ValidateAndFormatEnumName` and `LayerCatalog.Sanitize` share the `^[A-Z_][A-Z0-9_]*$` rule and **must stay in
sync**, else a name the panel accepts is stripped on save); **Auto-Save** persists in `PlayerPrefs`
(`APS_AutoSaveEnabled`). All persistence/codegen is centralized in **`LayerCatalog`** (the single codegen path):

- **Source of truth is external** — `LayerCatalog.SaveNames` writes the ordered list to `ProjectSettings/APS_Layers.json`
  **atomically** (`.tmp` + `File.Replace` → `.bak`), outside the package, never shipped, never clobbered by import.
  `PopupLayerEnum.generated.cs` is a *projection* that **ships in the package** (`Runtime/Generated/Layers/`, resolved by
  `FileSearcher.LayersEnumFilePath`) — regenerated in place, which needs a **writable** package.
- **Editing is gated** — `PopupLayerEditorPanel` locks add/rename/delete behind a **Customization** toggle; on a
  read-only install unlocking offers `FileSearcher.EmbedPackage` (make writable) first. Canvas order/prefab stay editable
  (consumer-side `LayerCanvasConfig`).
- **Read safety** — `LayerCatalog.TryLoadNames` returns `Missing`/`Ok`/`Unreadable`; `Reconcile` seeds defaults only on
  `Missing` and **aborts on `Unreadable`** so a locked/mid-write/corrupt store is never overwritten with defaults (the
  corruption bug). It recovers from `.bak` when the live store is missing/corrupt.
- **`GenerateEnumSource`** builds the enum (`None = 0`, then `1 << bit`, max 31); **`Reconcile`** rewrites the file
  from the store only when content differs (EOL-insensitive; idempotent — no needless recompile). A write to a read-only
  package fails silently (caught) — the shipped default stands.
- **`LayerEnumSyncPostprocessor`** heals the enum after a package update (writable installs): `OnPostprocessAllAssets`
  runs on the *already-loaded* editor assembly **before** the imported scripts recompile; `[InitializeOnLoadMethod]` is a
  secondary post-reload safety net. This is the **non-destructive-update** mechanism ([[Layers]], [[Build & Packaging]]).
- **Gotcha — "+" adds an empty "being named" row that must NOT dirty the set.** `AddLayer` appends a blank entry to
  `_enumNames` but leaves `_namesChanged` false. `SaveChanges` strips empty names, so if adding dirtied the set, with
  Auto-Save on `DrawFooter` would run `SaveChanges` + `LoadState` in the *same* OnGUI pass and wipe the row before it
  could be typed into — the "+ button does nothing" bug. The set is dirtied only when `RenameLayer` gives the row a
  valid name (which then triggers the codegen path).

Hand-editing the file is futile — it's regenerated from the store ([[Invariants]], [[Layers]]).

### Per-layer canvas config (`LayerCanvasConfigStore`)

Beside each name the panel edits a **sorting order** + a **canvas prefab** (mandatory), persisted to the runtime
`LayerCanvasConfig` SO via **`LayerCanvasConfigStore`**: `LoadOrCreate` the single asset in the consumer's
`Assets/Resources/`, `Reconcile` its entries with the current names (add missing, prune orphans — keyed **by name**, and
**back-fill any entry with no canvas** with the consumer-owned default from **`DefaultCanvasFactory`** — a responsive
full-screen overlay prefab (UI layer, ScaleWithScreenSize 1920×1080) baked once into
`Assets/AdvancedPopupSystem/APS_DefaultCanvas.prefab`, outside the package so updates never clobber the user's edits),
then `Save`. Clearing a row's canvas in the panel snaps it back to that default, so the field is never empty. Names stay owned by `LayerCatalog`; this config is joined to them by name. The rows are **displayed sorted
by sorting order**, but the canonical name/bit order is left untouched — reordering it would renumber `PopupLayerEnum`
and break serialized `PopupLayer` masks ([[Invariants]]). Sorting/prefab edits save only the SO (no recompile); name
add/rename/delete go through the codegen path above and re-sync the SO. Runtime consumption in [[Core System]]
("Canvas routing").

## Display generation (`PopupDisplaysEditorPanel`)

Lists display subfolders (ending in `Display`) from **both** `FileSearcher.BuiltinDisplaysFolderPath` (the package's
built-ins — read-only, shown but not renamable/deletable) and `FileSearcher.CustomDisplaysFolderPath` (the consumer's
`Assets/AdvancedPopupSystem/Generated/Displays/`, assembly `AdvancedPS.Generated.Displays`). **Add**/**Delete** operate
only on the custom folder; **Add** writes `<Name>Display/…generated.cs` from string templates **only if the folder
doesn't exist** (never overwrites your bodies). Names validated to letters-only, then `RemoveDisplaySuffix` +
`"Display"`/`"Settings"` ([[Displays & Animations]], [[Display — Custom]]).

## FileSearcher (paths)

Resolves the package via `UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FileSearcher).Assembly)` (works
under `Packages/` **or** `Assets/`), with a folder-name (`advanced-popup-system`) AssetDatabase search as fallback.
**Package paths** — `ImagesFolderPath` (asset path, for `LoadAssetAtPath`), `BuiltinDisplaysFolderPath` (real FS), and
`LayersEnumFilePath` (the shipped enum — writable only when the package is). **Consumer path** —
`CustomDisplaysFolderPath` (`Assets/AdvancedPopupSystem/Generated/Displays/`, ensures folder + asmdef). **Writability** —
`IsPackageWritable` + `EmbedPackage` (Embedded/Local/loose = writable; Git/registry = read-only until embedded).
`ToAssetPath`/`ToFsPath` convert both `Assets/` and `Packages/`. **All accessors are lazy and
non-throwing** — a failure logs once and returns `null` (callers guard); the old eager static ctor threw
`TypeInitializationException` under UPM and poisoned every downstream site. **`FolderRenamePrevention`** still reverts a
rename of the loose `advanced-popup-system` folder (the fallback keys off the name).

## Addressable index generation (`AddressablePopupIndexGenerator`, optional)

Under `APS_ADDRESSABLES` (`Editor/Addressables/`). Keeps Addressable-flagged popups in the **"Advanced Popup System"**
Addressables group (address = type `FullName`, one prefab per type — warns on duplicates), prunes un-flagged/dead
entries, and writes the **`AddressablePopupIndexAsset`** ScriptableObject
(`Assets/Resources/APS_AddressablePopupIndex.asset`, created on first run like `LayerCanvasConfigStore`) —
**idempotently** (no write/reimport unless the catalog changed; entries sorted by TypeName so both paths below agree on
order). Writing a **data asset** instead of C# is the point: flagging a popup Addressable no longer recompiles scripts
or reloads the domain (the old cost of the generated `.cs` index). Two entry points: the **menu**
`Tools/Advanced Popup System/Regenerate Addressable Index` → `Regenerate()` — the only full `t:Prefab` project scan —
and **auto** via `AddressablePopupPostprocessor` → `SyncChanged()` (deferred out of the import callback), which is
**incremental**: it inspects just the changed prefabs and touches settings/group/index only when an Addressable popup
is actually involved, so unrelated prefab saves and project open trigger no Addressables work (details in
[[Addressables]] "Editor tooling"). The inspector's Addressable box lives in `IAdvancedPopupEditor`.

## Other editor pieces

- **`CreateAdvancedPopup`** — `GameObject ▸ UI ▸ Advanced Popup`: builds a stretched `RectTransform` GameObject with
  `Image` + `AdvancedPopup`, creating a `Canvas` (ScreenSpaceOverlay + scaler + raycaster) if none is in the parent.
- **Custom inspectors:** `IAdvancedPopupEditor` + `BaseSettingsDrawer` render the popup/settings; `InspectorEnum`
  ([[Settings & Logging]]) switches between the APS view, an optimized view, and Unity's default. `CustomHierarchyrIcon`
  adds the hierarchy icon; `APSEditorStyles`/`EditorGUILayoutExtensions` are shared GUI helpers. `IAdvancedPopupEditor`
  caches its `SerializedProperty`s in `OnEnable` (no per-repaint `FindProperty`) and draws `Inactive` at the very top and
  `EscapePolicy` beside the layer controls; every field it draws by hand is listed in the static `ExcludedProperties` so
  the default-inspector fallback (which still catches user-added fields on `AdvancedPopup` subclasses) doesn't double them.
- **`APSEditorStyles` styles are lazy + self-healing.** The 1×1 background textures behind the dark box groups are plain
  `Texture2D`s that Unity culls on memory cleanup (play-mode enter/exit, scene load, `UnloadUnusedAssets`); a static style
  built once then held a *destroyed* texture, so the box background silently vanished until the next domain reload — the
  "boxes sometimes don't show" bug. Fix: each accessor rebuilds its style when the backing texture is gone (`Culled`), and
  textures are `HideFlags.HideAndDontSave`. Building lazily (first hit in OnGUI) also keeps `GUI.skin`/`EditorStyles` off a
  static constructor that may run outside a GUI context. Section headers go through `EditorGUILayoutExtensions.DrawSectionHeader`.

## Depends on

- [[Layers]] & [[Displays & Animations]] (what gets generated), [[Settings & Logging]] (the Settings tab)
