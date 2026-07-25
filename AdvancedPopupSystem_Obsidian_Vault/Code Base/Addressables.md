---
type: code
status: active
description: On-demand popup loading via Addressables — the resolver seam, the index data asset, the two loading lanes (unique auto-resolve vs explicit spawn/pool), Root parenting, preload, and the optional runtime/editor assemblies. Read when touching loading, spawning, or the index asset.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/IPopupResolver.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AddressablePopupIndex.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AddressablePopupIndexAsset.cs
  - Assets/advanced-popup-system/Runtime/Addressables/
  - Assets/advanced-popup-system/Editor/Addressables/
---

# Addressables

Optional integration that lets popups load from **Addressables on demand** instead of being pre-placed in a scene.
Everything here is **additive and off by default**: with no Addressable-flagged popups and no resolver, the system is
scene-only exactly as before (see [[Core System]]).

## The seam (why core stays dependency-light)

`IPopupResolver` (`AdvancedPS.Core.System`) + the static `AdvancedPopupSystem.Resolver` (null by default) are the whole
coupling. **Core owns the catalog, the dedup, and the scene-wins rule; a resolver only turns an address into a live
instance and releases it** (`LoadAsync(address, parent, token)` / `Release(popup)`). Core never references Addressables
— keeping the "lean runtime" invariant ([[Invariants]]). The optional assembly registers `AddressablesPopupResolver`
into the seam at load; without the package the seam stays null.

## The index (catalog)

The catalog is a **data asset**: `AddressablePopupIndexAsset` (a `ScriptableObject` at the consumer's
`Assets/Resources/APS_AddressablePopupIndex.asset`, loaded once + cached via `Loaded`) holds an `Entry[]` of
`{TypeName, Address, Layer, LoadMode, PreloadScenePaths, UnloadScenePaths}` (empty `PreloadScenePaths` = every scene;
empty `UnloadScenePaths` = never unload). `AddressablePopupIndex` (`internal static`) is the **query half**
(`ForLayer`, `Preloads`, `PreloadsForScene(scenePath)`,
`UnloadsForScene(scenePath)`, `TryGetByTypeName`, `HasEntries`). **Scene selection is by stable identity, not
position:** the popup stores scene **asset GUIDs**; the generator resolves them to current scene **paths**
(`PreloadScenePaths`/`UnloadScenePaths`) and bakes those, so the runtime matches `Scene.path` with **no AssetDatabase**
and reordering Build Settings never shifts anything (see "Per-scene preload/unload"). **Data, not codegen:** the
generator writes the asset (so flagging a popup Addressable no longer recompiles or reloads the domain), and the `Entry`
shape lives in **one** place — the SO — with no kept-in-sync copies to break.

- **Keyed by type `FullName` (string), not `Type`** — deliberate: user popups live in the **consumer** assembly, which
  references APS, not vice-versa, so this core-side file cannot `typeof` them. The string also bridges the scene-wins
  check (`IsLive(typeName)` scans `AllPopups` by `GetType().FullName`).
- **Address = the popup type's FullName too** (the generator sets both the Addressables entry address and the index
  Address to it) — deterministic, survives prefab moves, unique per type.

## Two lanes (the core distinction)

- **Lane A — unique / singletons.** `LayerShow`, `Show<T>`, `SwitchShowHide<T>`, `GetPopupAsync<T>`, `TryGetPopup`. One instance per type,
  lives in `AllPopups`/`PopupCacheByType`, participates in layers + escape. `LayerShow` calls `EnsureLayerLoadedAsync`
  before showing → materializes the layer's Addressable popups not already live. `GetPopupAsync<T>` is the **by-type**
  equivalent: same `Resolver.LoadAsync(entry.Address, GetCanvasForLayer(entry.Layer))` load, but for one type, returning
  the **unique** instance (its `Init()` self-registers it — no `DeactivateAdvancedPopup`, unlike Lane B). Concurrent gets of
  the same not-yet-loaded type **share one in-flight load** (`_inFlightLoads`) so no duplicate is made — details in
  [[Core System]] "Lookups". `Show<T>` is a thin `Operation` over it; `Hide<T>` never loads ([[Core System]] "Show / hide by type"). **Scene wins the index:** a
  scene-authored popup of the same type suppresses the load (dedup by type name — the `TryGetPopup`/`IsLive` check every
  Lane-A entry point runs first), so testing one screen = drop its prefab in a scene and press Play — no Addressables
  round-trip.
- **Lane B — many copies.** `SpawnAsync<T>(parent)` / `Despawn(popup, release)`. For toasts / list rows. Reuses a
  pooled instance or loads a fresh one, then **pulls it out of the unique registries** its `Init()` joined
  (`DeactivateAdvancedPopup`) and tracks it in `SpawnedPopups` — so `LayerShow` ignores it, but it is still in
  `ActivePopups` while visible, so **escape + HideAll reach it** ([[Core System]]). Data popups:
  `SpawnAsync<TPopup,TData>(data)` binds on spawn, and `Despawn` **clears** `IDataPopup` data before pooling/release —
  a pooled copy never carries the previous use's content ([[Popup Lifecycle]] "Data popups").

## Loading, parenting, preload

- **Parenting:** loaded/spawned popups go under **`GetCanvasForLayer(popupLayer)`** — the canvas mapped to the popup's
  layer via `RegisterLayerCanvas`, else `AdvancedPopupSystem.Root` (auto persistent DontDestroyOnLoad overlay Canvas,
  overridable). This is the per-layer canvas routing (HUD vs dialogs vs … on independent sort orders — mechanism in
  [[Core System]] "Canvas routing", surfaced on [[Layers]]). `SpawnAsync` still takes an explicit `parent` that wins
  when non-null. Scene-authored popups never touch any of this. Every parenting site here (both pool re-parents, the fresh
  Lane-B load, `GetPopupAsync`, `EnsureEntryLoadedAsync`) also calls `ApplyOrder` so the instance lands at its ordered
  sibling index instead of last ([[Hierarchy Order]]).
- **Per-scene preload/unload:** `ProcessScene(scenePath)` (`Core System`) runs for the boot scene and on every
  `SceneManager.sceneLoaded`, matched by **`Scene.path`** against the entries' baked paths (identity-based → reordering
  Build Settings never shifts it; no 31-scene cap). It **unloads then preloads** (a scene in both a popup's sets ends up
  loaded): release entries whose `UnloadScenePaths` contains the path via `UnloadType`, then eager-load `LoadMode.Preload`
  entries that want it (**empty** `PreloadScenePaths` = every scene, else `PreloadScenePaths` contains it) and aren't
  `IsLive`. **Empty = Everyone** for preload (so the untouched default → matches the first scene → boot preload) and
  **empty = None** for unload (never) — no separate flag, and a fresh/old popup deserializes to empty either way, so
  there is no migration. **Preload is a hint, not a gate:** a popup shown before its preload scene still loads on demand
  (the show / `EnsureLayerLoadedAsync` path is scene-agnostic — the "load, don't fall back" requirement). `LoadMode` is
  **kept** and orthogonal: `Lazy` ignores the preload scenes (on-demand only, and the inspector hides them), `Preload`
  adds the eager scene loads; the unload set applies to both.
- **Boot wiring gotcha:** `sceneLoaded` does **not** fire for the already-loaded first scene, so `PreloadOnBoot`
  (`[RuntimeInitializeOnLoadMethod` **`AfterSceneLoad`**`]`) both *subscribes* `sceneLoaded` (idempotent `-=`/`+=`)
  **and** runs `ProcessScene` once for the boot scene. AfterSceneLoad (not Before) so scene-authored popups have
  registered and scene-wins can suppress duplicates; the resolver registered at **`BeforeSceneLoad`**, so it is set.
  `PreloadAll()` / `PreloadLayer(layer)` still return `Operation`s and load **all** `Preload` entries regardless of
  scene — for gating a loading screen.
- **Per-entry fault isolation** (2026-07-23, from a consumer incident): `EnsureEntryLoadedAsync` catches a failed load
  (bad asset, an exception in the popup's `Awake`/DI injection…), logs it **naming the popup type**, and swallows it —
  so every sequential batch above it (`PreloadAll`, `EnsureLayerLoadedAsync` → `LayerShow`/`PreloadLayer`, the
  per-scene pass) continues with its remaining entries instead of silently dropping everything after the broken popup.
  Isolation lives in that **one** method on purpose — don't re-add per-loop catches. Cancellation is not a failure:
  the catch re-checks `OperationCancelled` and returns, and each loop's own check exits.
- **`UnloadType(typeName)`** (`Core System`): releases the resident unique (Lane-A) instance via `Resolver.Release`
  (its `OnDestroy` prunes the registries) + drains `_pool`/`_singlePool` of that type; **skips a visible
  (`IsBeVisible`) instance** and user-owned `SpawnedPopups`. Releasing a scene-authored popup is a no-op (the resolver
  only frees what it created), so scene-wins popups are safe. No new static collection → no new leak guard.
- **`IAdvancedPopup.PoolCapacity`** (one value, both lanes): how many idle copies to keep alive instead of releasing.
  `-1` = keep unlimited (resident, like a scene popup), `0` = **despawn on hide** (release the handle so memory
  can unload; reloads next show), `1` = a **single on/off instance** (default; one idle copy, no pool list), `N` (≥2) = keep at
  most N idle copies. This **replaced** the old `HideBehavior` enum (`Despawn` ≡ capacity `0`) + `MaxPoolCount` — one
  field, no separate enum.
  - **Lane A** (`AdvancedPopup.HideAsync`): only `0` vs non-`0` matters — a unique popup either releases on hide (`0`) or
    stays resident (any other value; the single instance can't "pool").
  - **Lane B** (`AdvancedPopupSystem.Despawn`, `release:false`): `0` → release now; `1` → **`_singlePool`** (one retained
    instance per type, a light `Dictionary<string, IAdvancedPopup>` — no list allocated for the single-instance case);
    `-1` → unlimited **`_pool`** list; `N` (≥2) → `_pool` list of up to N, release the extra. `SpawnAsync` reuses
    `_singlePool` before `_pool`. `Despawn(…, release:true)` always releases regardless of capacity. Both stores obey the
    leak-guard rule (cleared on play-mode exit, dead entries pruned on scene unload — [[Core System]]).
  - Inspector: the **Pool** box — an info box + a `Pool Capacity` slider (−1…64, manual entry allows >64), see "Editor tooling".

## Optional assemblies (isolation, mirrors DoTween/input)

Both gated by `APS_ADDRESSABLES` (a `versionDefine` on `com.unity.addressables`, per-asmdef) + `defineConstraints`:

- **Runtime** `Runtime/Addressables/` → `dest-69.advanced-popup-system.addressables`: `AddressablesPopupResolver`
  (`Addressables.InstantiateAsync` → `TryGetComponent<IAdvancedPopup>`; `ReleaseInstance` per tracked handle;
  self-registers `BeforeSceneLoad`).
- **Editor** `Editor/Addressables/` → `dest-69.advanced-popup-system.addressables.editor`: the index generator +
  postprocessor. The main Editor asmdef also carries the versionDefine so the inspector's Addressable box can `#if`.

## Editor tooling ([[Editor & Codegen]])

Two entry points, one catalog (**"Advanced Popup System"** group, address = type FullName; `AddressablePopupIndexAsset`
written idempotently, entries **sorted by TypeName** so the paths never fight over order → no recompile, no domain
reload). **APS ▸ Settings ▸ Regenerate Addressable Index** → `AddressablePopupIndexGenerator.Regenerate()` (no top-level
menu item — every APS tool lives in the APS window; this optional assembly can't be referenced by the main editor one, so
it publishes the action through the `APSEditorTools` seam at `[InitializeOnLoadMethod]`, and the Settings tab draws the
button only while the delegate is set — [[Editor & Codegen]]): the full `t:Prefab`
rescan — the only path that loads every prefab. **Auto** — `AddressablePopupPostprocessor` collects changed paths
(string checks only) → deferred `SyncChanged(paths, prefabsDeleted, scenesMoved)`: **incremental** — one `GetComponent`
per imported/moved prefab, **capped**: above `BulkPrefabCap` (64) changed prefabs the per-prefab pass is skipped (logged)
and the index re-bakes from the group's own membership instead — a checkout / Library rebuild / imported package must not
turn "one lookup per changed prefab" into loading every prefab in the project; a flag flipped inside such a batch is
picked up by that prefab's next save or the menu item ([[Hierarchy Order]] "Postprocessor budget"); `EnsureEntry` dirties the Addressables settings only on a real add/move/re-address (no-op
pass = no dirty, no save); a prefab delete prunes dead-GUID entries; the index re-bakes **from the group's membership**
(loads only the few Addressable popups) and only when the catalog could differ (membership changed / an Addressable
popup saved / a scene path moved). Cold path never creates anything — `GetSettings(false)`, and settings/group/index
come into existence only when a popup flagged Addressable actually appears. Net effect: unrelated prefab churn and
project open cost ~one component lookup, invisible; only the menu item pays the full-scan price. The
inspector (`IAdvancedPopupEditor`) shows an "Addressable" box (toggle + `LoadMode`, plus a **Preload Scenes** control
shown only when `LoadMode == Preload` and always an **Unload Scenes** checklist) and a **separate "Pool" box** — an info
box explaining the values plus a custom `Pool Capacity` control (`PoolCapacity`): a slider (−1…64) fed a clamped value +
an `IntField` that is the source of truth and accepts manual values >64 (lower-clamped to −1). Both boxes are editable
only on the prefab asset (`IsEditingPrefabAsset`), read-only with a small note on a scene object. Scene selection:
`DrawPreloadScenes` = a `DrawSceneChecklist` + a hint when nothing is checked ("→ preloads in every scene", i.e. the
empty=Everyone default); `DrawSceneChecklist` writes scene **GUIDs** into the `List<string>` (`s.guid.ToString()` from
`EditorBuildSettings.scenes`, unioned with any already-stored GUID so a scene later dropped from Build Settings stays
removable). **Single-object edit only** — multi-select shows a note (per-object lists differ). Toggling a row does
`arraySize++`/`DeleteArrayElementAtIndex` on the list property.

## Gotchas

- **`ManualInit` is ignored for Addressable popups** — they always auto-init on `Awake` (`!ManualInit || Addressable`),
  because the resolver instantiates them and never calls `Init()`; `SpawnAsync`/preload/lazy all assume `Init()` ran. The
  flag applies to scene popups only; the inspector greys it with a note (mirrors `AutoHideOnInit`) — see [[Popup Lifecycle]].
- **Flag `Addressable` on the PREFAB**, not a scene instance — only the prefab asset gets grouped/indexed (the
  postprocessor fires on prefab changes). The inspector **enforces** this: `IAdvancedPopupEditor.DrawAddressable`
  disables the toggle unless `IsEditingPrefabAsset()` (prefab asset selected, or Prefab Mode), with a small note —
  editing the flag on a scene instance would desync it from the prefab.
- **One Addressable prefab per popup type.** The index is type-keyed; the generator warns and skips a second prefab
  sharing a type. Give each Addressable popup a distinct `AdvancedPopup` subclass.
- **`TryGetPopup<T>` stays synchronous** → returns only *resident* popups (scene / preloaded / already loaded); "load if
  missing" happens only on the async paths (`LayerShow`/`Show<T>`/`GetPopupAsync<T>`/`SpawnAsync`). `GetPopupAsync<T>` is
  the async **get** when you need the instance reference and it may not be loaded yet; `TryGetPopup` stays the sync,
  zero-alloc, resident-only get — which is why `Preload` still matters for a synchronous get.
- **Empty preload list = Everyone (no migration).** There is deliberately no "all scenes" bool: an **empty**
  `PreloadSceneGuids` means "preload everywhere" (the default), a non-empty list narrows it. Because empty is the natural
  deserialization default for both a fresh popup and one serialized before the field existed, no migration guard is
  needed. Trade-off: a popup scoped to specific scenes that are **all later deleted** goes empty → flips to "everywhere"
  (rare; "preload nowhere" is what Lazy already expresses).
- **Determinism: identity, not position.** The popup stores scene **GUIDs**, so reordering / adding / removing scenes in
  Build Settings never re-points a selection (the whole reason for GUIDs over a build-index bitmask). The generator bakes
  GUID→**path** for runtime; a scene **rename/move** changes the path, so `AddressablePopupPostprocessor` also re-bakes
  the index on moved/deleted `.unity` (only when the group is non-empty; a plain scene *save* re-imports without a path
  change and is ignored — no churn). A stored
  GUID that no longer resolves (scene deleted) is skipped by `GuidsToScenePaths` and simply drops from the baked paths.
- `AdvancedPopupInstantiate` (the old NoOp stub) was **removed** — its planned role is now `SpawnAsync`/`Despawn` + the
  pool ([[Popup Lifecycle]]). Breaking API change — see [[Shipped Docs]] CHANGELOG.

## Depends on

- [[Invariants]] (lean runtime / optional-assembly rule, static cleanup), [[Core System]] (registries, Root, preload,
  HideAll), [[Popup Lifecycle]] (fields, PoolCapacity, spawn), [[Operations & Cancellation]] (`Operation` return type),
  [[Editor & Codegen]] (the generator), [[Project Map]] (assemblies & the `APS_ADDRESSABLES` define)
