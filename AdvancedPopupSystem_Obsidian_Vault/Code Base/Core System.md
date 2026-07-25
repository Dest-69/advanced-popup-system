---
type: code
status: active
description: AdvancedPopupSystem — the static coordinator. Registries, type cache, scene/play-mode cleanup, hierarchy-depth sorting, and layer show/hide/hide-all orchestration. Read when touching registration, lookup, or layer batch logic.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AdvancedPopupSystem.cs
---

# Core System

`AdvancedPopupSystem` (`AdvancedPS.Core`) is a **static coordinator**, not a singleton — there is no `.Instance`
([[Invariants]]). It owns all global state and orchestrates layer-level batches; individual show/hide lives on the
popup ([[Popup Lifecycle]]).

## Registries (the only global state)

- **`AllPopups`** — every popup present in loaded scenes (visible or not). Source of truth for lookups and key events.
- **`ActivePopups`** — currently visible popups. **Mutated by the popup's `Subscribe`/`Unsubscribe`**, not here.
- **`PopupCacheByType`** (`Dictionary<Type, IAdvancedPopup>`) — O(1) `TryGetPopup<T>` for the non-`activeOnly` path. One
  representative per **concrete** type; removal re-points to a survivor rather than dropping the entry (see below).
- **`ActiveLayer`** (`PopupLayerEnum` bitmask) — combined active layers; **only** `Layer*`/`HideAll` here change it
  ([[Layers]]).
- **Addressables surface** (optional, [[Addressables]]): `Resolver` (`IPopupResolver`, null default → scene-only),
  `SpawnedPopups` (Lane B, kept out of `AllPopups`), the reuse pool, `_inFlightLoads` (Lane-A concurrent-load dedup, keyed
  by `Type`), and `Root` (default parent for loaded/spawned popups — auto persistent Canvas, overridable). All follow the
  leak-guard rule — `_inFlightLoads` is cleared on play-mode exit but needs **no** scene-unload prune (self-removing, holds
  a Task not a Unity ref).
- **`_layerCanvases`** (`Dictionary<PopupLayerEnum, Transform>`, keyed by **single** flag) — per-layer canvas routing
  (see "Canvas routing" below). Leak-guarded like the rest.

## Lifetime & cleanup

- `Initialize()` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`) subscribes `SceneManager.sceneUnloaded` and, in
  editor, `playModeStateChanged`. `SceneManager.sceneLoaded` is subscribed later, in `PreloadOnBoot` (AfterSceneLoad),
  which also runs the first per-scene pass for the boot scene — `sceneLoaded` doesn't fire for the already-loaded first
  scene ([[Addressables]] "Per-scene preload/unload").
- **Per-scene preload/unload (Addressables):** `OnSceneLoaded` → `ProcessScene(scene.path)` releases popups whose
  unload set includes the scene (`UnloadType`) then eager-loads `Preload` popups that want it. Matched by `Scene.path`
  against the index's baked scene paths (popups store stable scene **GUIDs**; the generator bakes them to paths) — so
  reordering Build Settings can't shift it. `UnloadType(typeName)` frees a type's resident unique (Lane-A) instance
  (`Resolver.Release`, skipping a visible one) and drains its `_pool`/`_singlePool`; scene-authored and user-spawned
  popups are untouched. **No new static collection** — it reuses the existing registries/pools, so no new leak guard.
  Mechanism/gotchas in [[Addressables]].
- **Leak guards ([[Invariants]]):** on `ExitingPlayMode` all registries + `ActiveLayer` are cleared; on scene unload,
  destroyed (`== null`) entries are pruned and `PopupCacheByType` is rebuilt from survivors. `_layerCanvases` gets the
  same pass — mappings whose **canvas** was destroyed are dropped (a reused scratch key-list avoids alloc). Any new
  static collection needs the same treatment.
- **Registration:** `InitAdvancedPopup(popup)` (called from `AdvancedPopup.Init`) adds to `AllPopups` + type cache and
  re-sorts; it also warns on legacy multi-flag `PopupLayer` data (one layer per popup — [[Layers]]).
  `DeactivateAdvancedPopup(popup)` (from `OnDestroy`) removes from `AllPopups`/`ActivePopups`, and from the
  type cache **only if this popup was the cached representative** — then re-points that type to a surviving instance if
  one remains. (Guards the case of several popups sharing a type: removing a non-cached one must not evict the entry,
  and removing the cached one must not blind-drop it.)

## Sort order (non-obvious)

`SortPopups()` orders `AllPopups`: **active-scene popups first**, then background-scene popups; within each group by
**hierarchy depth descending (deepest first)**. So nested/child popups are iterated before their parents — relevant to
any "first match" lookup. Runs on **every registration**, so it is kept
allocation-light on purpose: one scratch list, depth computed once per popup, `List.Sort` stabilized by original index
(no LINQ `OrderBy`/`ToList`) — keep it that way.

## Lookups

- `TryGetPopup<T>(out popup, activeOnly=false)` — type-cache fast path when `!activeOnly`; otherwise linear over
  `ActivePopups`/`AllPopups`. **Synchronous, resident-only** — never loads. That is shipped contract, not detail: the
  docs (§0) sell it as **secondary-only** for consumers (primary open = `Show<T>`/`GetPopupAsync`) — don't make it
  load, don't make it allocate.
- `GetPopupAsync<T>(token)` — **async counterpart** that loads a Lane-A Addressable popup if it isn't resident. Fast-path
  is `TryGetPopup<T>` (so a resident popup returns without a round-trip, and that same check is the scene-wins/dedup
  guard); on a miss it resolves the type in the Addressable index and instantiates via `Resolver` under the layer canvas,
  returning the **unique** instance (contrast `SpawnAsync`, which detaches its copy from the registries — [[Addressables]]).
  Null when not in a scene and not Addressable / no integration. No-op-cheap on the scene-only path (the `TryGetPopup` hit).
  **Concurrent-load dedup:** simultaneous calls for the same not-yet-loaded type **share one in-flight load**
  (`_inFlightLoads`, keyed by `Type`, published synchronously *before* the first await so a suspended-then-resumed caller
  finds it) — no duplicate instance. That load runs under `CancellationToken.None` (a singleton must not be released
  because one caller cancelled); each caller honors its own `token` after the await. `RemoveInFlightWhenComplete` clears
  the entry on completion (success/failure) so a failed load can't poison the type.
- `GetPopupByLayer(layer, activeOnly=true)` — first popup whose layer is in the query (any-of bitwise; the query may
  be a mask — [[Layers]] "one layer per popup").
- `GetPopupByName(name, activeOnly=true)` — by `GameObject.name`, **case-sensitive**.

## Layer orchestration

All `Layer*`/`HideAll` methods return an `Operation` ([[Operations & Cancellation]]) and internally fan out to each
popup's `ShowAsync`/`HideAsync` via `Task.WhenAll`; exceptions are caught and logged through `APLogger`.

- **`LayerShow(layer, autohide=true)`** — `autohide=true`: no-op if `ActiveLayer == layer`; else set `ActiveLayer =
  layer`, **hide** popups excluding the layer, then **show** the layer's popups (sequential await — hide finishes
  before show). `autohide=false`: no-op if the flag is already set; else OR the flag and only show.
- **Typed overloads** `LayerShow<T>(…, settings, autohide)` and `LayerShow<T,J>(…, showSettings, hideSettings)` run the
  batch with a **per-call** display type/settings instead of each popup's cached display ([[Displays & Animations]]).
- **`LayerHide(layer)` / `LayerHide<T>`** — clear the flag (`&= ~layer`) and hide the layer's popups.
- **`HideAll()` / `HideAll<T>`** — no-op if `ActiveLayer == 0`; else reset to 0 and hide every **visible** popup
  (iterates an `ActivePopups` snapshot, so spawned Lane B popups close too — [[Addressables]]).
- **`SpawnAsync<T>` / `Despawn`** and **`PreloadAll` / `PreloadLayer`** — the on-demand loading APIs ([[Addressables]]).

**Gotchas:** the `ActiveLayer` equality/flag checks make layer calls idempotent — don't add your own guards on top.
Because manual `popup.Show()` doesn't touch `ActiveLayer`, mixing manual and layer control can desync what "active
layer" means vs. what's visible ([[Layers]]). The batch path (`GetPopupsByLayer`/`GetPopupsExcludingLayer` →
`Show/HidePopupsAsync` → `HideAllPopupsAsync`) is deliberately LINQ-free — manual loops into pre-sized `List<Task>`, the
`HideAll` snapshot is a plain `List` copy (still required: `Unsubscribe` mutates `ActivePopups` mid-batch). Don't
reintroduce `Where`/`Select`/`ToList` on these navigation-triggered paths.

## Show / hide by type

`Show<T>()` / `Show<T,J>()` / `Hide<T>()` / `Hide<T,J>()` / `SwitchShowHide<T>()` / `SwitchShowHide<T,J>()` (region
`SHOW / HIDE (BY TYPE)`) — summon, dismiss or toggle a **single known popup by its type** in one call, without holding
a reference or driving a whole layer (the by-type counterpart to `Layer*`). All return an `Operation`. `Show<T>` awaits
`GetPopupAsync<T>` first, so an Addressable popup **loads on demand**; the typed `Show<T,J>` uses a per-call display
like `LayerShow<T>`. **`Hide` never loads** — nothing unloaded can be visible, so it's a synchronous `TryGetPopup`
wrapped in an `Operation`, a no-op when the popup isn't resident. **`SwitchShowHide<T>`** delegates to the instance
`SwitchShowHideAsync` when resident (keeps its `IsVisible`/`IsBeVisible`/`Inactive` semantics — one source of truth),
else `GetPopupAsync` → `ShowAsync` (contract: not loaded = not shown → toggle is a show). Toggle racing an in-flight
load joins it and resolves to show (the second `ShowAsync` no-ops on `IsBeVisible`).

**Configure/data variants:** `Show<T>(Action<T> configure)` and `Show<TPopup,TData>(data)` (constraint
`TPopup : AdvancedPopup<TData>`) run load → configure/`SetData` → show, so per-open setup lands before visibility. The
data overload shares generic arity 2 with `Show<TPopup,TDisplay>` — **constraints disambiguate** (C# 7.3+
constraint-aware overload resolution; both call shapes smoke-compile-verified). A throw from `configure`/`Bind` faults
the `Operation` and skips the show. Data model — [[Popup Lifecycle]] "Data popups".

**Gotcha:** these are **manual** shows — like `popup.Show()` they touch only `ActivePopups`, **never `ActiveLayer`**, and
don't autohide other layers; don't mix them with `Layer*` control expecting layer bookkeeping ([[Layers]]).

## Canvas routing (per layer)

Only popups **the system instantiates** need routing — Addressable lazy/preload loads (`EnsureEntryLoadedAsync`) and
`SpawnAsync` (Lane B). Scene-authored popups keep their own hierarchy and never touch this. Both instantiation sites
resolve the parent through **`GetCanvasForLayer(popupLayer)`** instead of always using `Root`. Mappings live in
`_layerCanvases` (`Dictionary<single-flag, Transform>`) and come from **two sources**, checked together:

- **Tool config (primary)** — the Layers panel writes a per-layer sorting order + canvas prefab (panel-mandatory, seeded with the consumer-owned `APS_DefaultCanvas`) to the runtime
  `LayerCanvasConfig` SO (`Runtime/Settings/LayerCanvasConfig.cs`; cached `Resources.Load`, consumer asset in
  `Assets/Resources/`; [[Editor & Codegen]], [[Layers]]). `GetCanvasForLayer` → `EnsureLayerCanvases` **lazily
  materializes** each matching layer's canvas on first use: `CreateLayerCanvas` instantiates the entry's prefab (its
  `sortingOrder` **forced** to the entry's value — tool beats prefab) or a plain overlay via the shared
  `CreateCanvas(name, order)` (also backs `Root`), `DontDestroyOnLoad`, **renames the instance `<Layer> - APS Canvas`**, and caches it into `_layerCanvases[flag]`. Name↔flag via
  `LayerCanvasConfig.TryParseLayer` (`Enum.TryParse`); stale/renamed entries just don't parse and are skipped.
- **Runtime override** — `RegisterLayerCanvas(layer, canvas)` / `UnregisterLayerCanvas(layer)` split the mask into single
  bits and store one entry per flag (null clears). A pre-populated `_layerCanvases[flag]` makes `EnsureLayerCanvases` skip
  that flag → the manual mapping **wins** over the config.

`GetCanvasForLayer` then returns the mapped canvas for the popup's layer, else `Root`. A popup carries **one** layer
([[Layers]]); legacy multi-flag data resolves deterministically to the **lowest-bit** mapped layer's canvas (and warns
at registration). Lane A reads the popup's layer from the index asset's `Entry.Layer` (no instance yet at load); Lane B from
the pooled instance (reuse) or `Entry.Layer` (fresh load). The cached SO is dropped on play-mode exit
(`LayerCanvasConfig.ClearCache`, beside `_layerCanvases.Clear`); auto-created canvases are `DontDestroyOnLoad` (Unity
destroys them on exit like `APS_Root`) and dead mappings are pruned on scene unload.

- **Gotcha:** `Preload`/boot loads (`AfterSceneLoad`) may run **before** a consumer's runtime `RegisterLayerCanvas` →
  those popups take the tool config (or `Root`). Register in `BeforeSceneLoad` (or before `PreloadAll`) if a preloaded
  popup must start on a specific *runtime-registered* canvas.

## Escape stack step

`EscapeStep()` — one step of escape-close. Walks `ActivePopups` **from the end**: the list is a recency stack for free
(`Subscribe` appends at show-start, `Unsubscribe` removes at hide-start — self-cleaning; no separate static stack, so
no new leak guards). `IsEscapeCandidate` skips `null`/`!IsBeVisible` (also shields the known cancel-rollback gap) and
`ShownByCascade` popups (cascade groups are represented by their root — [[Popup Lifecycle]]). First relevant popup's
`EscapePolicy`: `Hide` → `popup.Hide()` + consumed; `Block` → consumed without closing (modal); `Ignore` → keep walking.
`LayerShow` batches get one step per popup (no layer grouping in v1 — group via DeepPopups instead).

**APS drives no input of its own (user call, 2026-07-25).** The consumer calls `EscapeStep()` from whatever means
"back". The key-driven overload `EscapeStep(Predicate<KeyCode>)`, `MatchesCloseKey`, the per-popup `CloseKey` and the
`EscapeCloseEnabled`/`EscapeCloseKey` settings are **gone** — and with them the "topmost closable popup owns the press"
drop, which existed only to stop a key bound to a background popup closing it from under the popup on screen (with no
keys there is nothing to mismatch). History and the don't-reintroduce rule: [[Input Backends]].

**The stack is derived, not stored** — membership is `visible ∧ ¬ShownByCascade ∧ EscapePolicy != Ignore`. Hence the
public API mutates the **policy** rather than a list: `AddToEscapeStack(popup, policy = Hide)` /
`RemoveFromEscapeStack(popup)` — neither shows nor hides, because position is show order and belongs to the popup's
visibility. Read side: `IsInEscapeStack`, `PeekEscapeStack` (who owns the next step — drives a Back button's
visibility) and `GetEscapeStack(buffer)` (caller-owned list, top-first, snapshot). Keeping it derived is the decision: a
second collection would need its own leak guards ([[Invariants]]) and could desync from what is actually on screen.

Runs on the input hot path — index loop, no LINQ, no allocation.

## Depends on

- [[Popup Lifecycle]] (the `ShowAsync`/`HideAsync` it drives), [[Operations & Cancellation]] (return type), [[Layers]]
  (`PopupLayerEnum`, `ActiveLayer` semantics)
