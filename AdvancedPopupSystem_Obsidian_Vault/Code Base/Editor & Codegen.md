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

`PopupSystemEditor` (`AdvancedPS.Editor`) — one window, four tabs, opened from the top **`APS/`** menu
(`APS/Layers`, `APS/Order`, `APS/Displays`, `APS/Settings`). Title shows the version from `PackageVersionHelper` (resolves
via PackageManager or a walked `package.json`; `"Dev"` fallback).

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
  read-only install unlocking offers to embed the package (make writable) first. Canvas order/prefab stay editable
  (consumer-side `LayerCanvasConfig`). The install-shape half lives in `PackageUpdater` (below).
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

### Install shape (`PackageUpdater`)

Embedding is the price of shipping the enum, and it carries costs Package Manager won't cover: it labels an embedded
package **Custom** and updates neither it *nor* a Git dependency (a Git URL has to be re-added). `PackageUpdater`
(`Editor/`, `[InitializeOnLoad]`) owns the whole install story — the embed handshake, the version check, the update, and
the way back out.

- **UI split by meaning, not by mechanism.** Updating is a property of the *install*, so it lives on the version line in
  `PopupSystemEditor.DrawVersionBar` (badge + **Update**, shown only when `UpdateAvailable && CanUpdate`). Embedding and
  **Remove embedded copy** are the two directions of the Customization toggle, so they stay in the Layers panel.
- **The lock must not outlive the install it was taken against.** `Unlocked` is a `PlayerPrefs` flag while writability
  is a property of the install, so removing the embedded copy (or reinstalling from Git) used to strand the panel on a
  permanent *"Embedding… once Unity finishes recompiling"* — an embed that was never coming. `SyncLockWithPackage`
  relocks whenever the package reads read-only and no embed is actually in flight (`EmbedInProgress` = a `Client.Embed`
  fired this session, or a running run). `BeginEmbed` is the only embed entry point, so "we asked" is always recorded.
- **One state machine, three intents** (`UpdateGit`, `UpdateEmbedded`, `Detach`) over `Client.Add`/`Add`/`Embed`. Only
  the ends differ: a Git install skips the prime step (no folder in the way), `Detach` stops before the re-embed.
  **Ordering is load-bearing** for the other two: the first Add runs *while still embedded* — the embedded folder
  shadows it, so nothing on disk changes — purely to validate the URL and pull the revision into Unity's global cache.
  Only then is the folder deleted. Delete-first would leave the project without APS, taking this class, the run, and the
  consumer's compile down together — the same deadlock that killed the consumer-side enum ([[Build & Packaging]]).
- **Every request recompiles**, so the step is parked in `SessionState`: `Poll` finishes a request that outlived its
  step, `Resume` picks up the ones a domain reload swallowed. Post-reload there is no request object, so project state
  is the only evidence — after the prime step the **manifest entry** is the gate (`Client.Add` writes it only on
  success). A reload landing between "sent" and "package caught up" is indistinguishable from a failure, hence **one
  bounded retry per run**, never a loop. `SessionState` (not `PlayerPrefs`) is deliberate: a run interrupted by closing
  the editor is abandoned, never replayed against a project it no longer knows.
- **The Git URL is recovered, not stored** — the consumer's `manifest.json` entry first (embedding doesn't rewrite it,
  and it is the only source that keeps a pinned branch/tag), then the repository the package declares in its own
  `package.json` (default branch, so `.git` is appended). A registry version or a `file:` path fails the Git-URL test,
  which is the point: it is what gates `CanUpdate` down to Embedded/Git installs.
- **The latest version is read off the remote, not from a release feed** — raw `package.json` at the ref the install
  actually tracks (`#pin`, else `HEAD`, which spares us guessing `main` vs `master`), so the badge answers "what an
  update would give me". GitHub-only URL rewriting; once per session, polled from `EditorApplication.update` because
  `AsyncOperation.completed` is unreliable outside play mode; **silent on every failure** — a missing badge is the right
  amount of noise for a nice-to-have, and the editor must never stall or spam on a network hiccup (the editor-perf rule).
- The enum needs no special handling here — `LayerEnumSyncPostprocessor` heals it from the store once a writable copy is
  back ([[Layers]]). After a `Detach` there is none, so the shipped default stands until the user embeds again.

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

## Popup order panel (`PopupOrderEditorPanel` → `PopupOrderConfigStore`)

The **Order** tab: a drag-sortable list of popup types (front → back), **filtered by layer** (the canvas boundary), persisted
to the consumer's `PopupOrderConfig` asset. Store mirrors `LayerCanvasConfigStore` (`LoadOrCreate`/`Reconcile`/`Save` in
`Assets/Resources/`); types come from `TypeCache` (no prefab load — the editor-perf rule), layer tags from the inspector /
`PopupOrderPostprocessor` / a prefab scan the panel runs on open, gated by a `SessionState` flag so it fires after real
prefab changes rather than on every recompile. Reached from the Layers tab's per-row **Order** button and
the popup inspector's *Edit Order*. Panel/store specifics, the deferred-removal gotcha, the filtered-drag mapping and the
postprocessor cost budget: [[Hierarchy Order]].

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
`IsPackageWritable` + `EmbedPackage` (Embedded/Local/loose = writable; Git/registry = read-only until embedded) — the
embed is always fired through `PackageUpdater.BeginEmbed`, never directly, so the panel can tell it apart from a stale lock.
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
or reloads the domain (the old cost of the generated `.cs` index). Two entry points: the **Settings tab's**
*Regenerate Addressable Index* button → `Regenerate()` — the only full `t:Prefab` project scan —
and **auto** via `AddressablePopupPostprocessor` → `SyncChanged()` (deferred out of the import callback), which is
**incremental**: it inspects just the changed prefabs and touches settings/group/index only when an Addressable popup
is actually involved, so unrelated prefab saves and project open trigger no Addressables work (details in
[[Addressables]] "Editor tooling"). The inspector's Addressable box lives in `IAdvancedPopupEditor`.

### Optional-integration seam (`APSEditorTools`)

`APS_ADDRESSABLES` assemblies can't be referenced by the main editor assembly (they may not exist), so an optional tool
**registers itself**: `AddressablePopupIndexGenerator.RegisterEditorTool` (`[InitializeOnLoadMethod]`) assigns
`APSEditorTools.RegenerateAddressableIndex`, and `PopupSettingsEditor.DrawMaintenance` draws the button only while the
delegate is non-null. Same shape as the runtime `AdvancedPopupSystem.Resolver` seam ([[Addressables]]), and the reason no
APS action lives in a top-level `Tools/` menu any more. The reference direction is optional → main
(`dest-69.advanced-popup-system.addressables.editor` gained the main editor asmdef GUID); never the reverse, which would
dangle when the integration is absent.

## Other editor pieces

- **`CreateAdvancedPopup`** — `GameObject ▸ UI ▸ Advanced Popup`: builds a stretched `RectTransform` GameObject with
  `Image` + `AdvancedPopup`, creating a `Canvas` (ScreenSpaceOverlay + scaler + raycaster) if none is in the parent.
- **Custom inspectors:** `IAdvancedPopupEditor` + `BaseSettingsDrawer` render the popup/settings; `InspectorEnum`
  ([[Settings & Logging]]) switches between the APS view, an optimized view, and Unity's default. `CustomHierarchyrIcon`
  adds the hierarchy icon; `APSEditorStyles`/`EditorGUILayoutExtensions` are shared GUI helpers. `IAdvancedPopupEditor`
  caches its `SerializedProperty`s in `OnEnable` (no per-repaint `FindProperty`) and draws `Inactive` at the very top and
  the escape-close block beside the layer controls; every field it draws by hand is listed in the static
  `ExcludedProperties` so the default-inspector fallback (which still catches user-added fields on `AdvancedPopup`
  subclasses) doesn't double them. The **Popup Layer row is a single-select** `Popup` (one layer per popup —
  [[Layers]]), never `EnumFlagsField`; its name/value options are rebuilt each `OnEnable` (the enum is regenerated by
  the Layers panel — never cache them statically), and legacy multi-flag values get a warning + a one-click
  "keep lowest bit" fix matching the runtime canvas tie-break.
  - **Conditional config is the house pattern:** Modules reveals a feature's config only when its flag is ticked and
    Addressable reveals load mode only when the toggle is on. Don't draw a control the runtime ignores. Two key blocks
    died by that rule: the **Show / Hide Key Settings** pair (PlayerPrefs-backed switch buttons over
    `KeyBindingShowSettings`/`KeyBindingHideSettings`) and, with the key tracking itself, `DrawEscapeClose`'s
    `CloseKey` row — it is now the `EscapePolicy` field alone ([[Input Backends]]).
- **Edit-mode Preview (`PopupPreviewDriver`)** — the inspector's Preview button plays show → 1s hold → hide entirely
  editor-side, then restores an exact snapshot (activeSelf, localScale, anchoredPosition3D, sizeDelta,
  alpha/interactable/blocksRaycasts; no Undo/SetDirty — nothing is dirtied). The runtime path can never run in edit
  mode — `OperationCancelled` treats `!isPlaying` as cancelled AND `Time.deltaTime` is frozen there (verified: constant
  across editor ticks even with `QueuePlayerLoopUpdate`) — so the driver re-samples the three built-ins from
  `EditorApplication.timeSinceStartup` with the same lerp + easing as their generated bodies (**keep the samplers in
  sync when editing those bodies**; Slide lerps clamped, Fade enables interactable only at show-end). Any other
  display (custom/DoTween) degrades to its instant methods. It never calls Show/Hide/Subscribe: the first preview runs
  the popup's public `Init()` once (the only way to learn the user-cached displays — they are runtime-only; its
  instant-hide fires the user's OnAnimation* once, same as runtime init) and `DeactivateAdvancedPopup` undoes the
  registration on cleanup; `OnAnimationStart/End` are reflection-detached from the cached settings for the whole
  preview so the instant calls stay silent. Ends-and-restores on beforeAssemblyReload / ExitingEditMode / sceneSaving /
  prefabSaving / destroyed target (`_show`, a plain C# object, is the "preview live" marker — a destroyed popup is
  fake-null). Button disabled in play mode, for multi-select, and on the persistent prefab asset (Prefab Mode works).
- **`APSEditorStyles` styles are lazy + self-healing.** The 1×1 background textures behind the dark box groups are plain
  `Texture2D`s that Unity culls on memory cleanup (play-mode enter/exit, scene load, `UnloadUnusedAssets`); a static style
  built once then held a *destroyed* texture, so the box background silently vanished until the next domain reload — the
  "boxes sometimes don't show" bug. Fix: each accessor rebuilds its style when the backing texture is gone (`Culled`), and
  textures are `HideFlags.HideAndDontSave`. Building lazily (first hit in OnGUI) also keeps `GUI.skin`/`EditorStyles` off a
  static constructor that may run outside a GUI context. Section headers go through `EditorGUILayoutExtensions.DrawSectionHeader`.

## Depends on

- [[Layers]] & [[Displays & Animations]] (what gets generated), [[Settings & Logging]] (the Settings tab)
