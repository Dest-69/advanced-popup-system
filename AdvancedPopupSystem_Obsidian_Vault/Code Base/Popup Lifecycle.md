---
type: code
status: active
description: IAdvancedPopup + AdvancedPopup — init/cache, subscribe/unsubscribe, show/hide (sync/async/typed), Cmd_/Switch commands, deep popups, close button. Read when creating or changing any popup or its lifecycle.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IAdvancedPopup.cs
  - Assets/advanced-popup-system/Runtime/Core/Popups/AdvancedPopup.cs
  - Assets/advanced-popup-system/Runtime/Core/Popups/AdvancedPopupData.cs
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IDataPopup.cs
---

# Popup Lifecycle

`IAdvancedPopup` (`AdvancedPS.Core.System`) is the **abstract `MonoBehaviour` base** (not an interface — [[Invariants]])
that holds all fields, cache, and `Init`; `AdvancedPopup` (`AdvancedPS.Core`) is the concrete implementation of the
Show/Hide/Switch overrides. **User popups extend `AdvancedPopup`.**

## Inspector fields (on the base)

`PopupLayer` (which layers can show this), `ManualInit`, `AutoHideOnInit` (default `true`), `Inactive` (blocks Show),
`EscapePolicy` (`EscapePolicyEnum`: `Hide` / `Ignore` default / `Block` — escape-stack participation, and the field the
`Add/RemoveFromEscapeStack` API writes at runtime, see [[Core System]]),
the **Addressable** box `Addressable` / `AddressableLoadMode` + the per-scene selection
`PreloadSceneGuids` (empty = Everyone) / `UnloadSceneGuids` (empty = None), both scene **GUIDs**, and the **Pool** box
`PoolCapacity` (unified on-hide + pool control: -1 unlimited / 0 despawn / N keep — all [[Addressables]]).
Hidden: `RootTransform`, `canvasGroup`, `IsBeVisible` (set when animation **starts**), `IsVisible` (set when it
**ends**).
`AdvancedPopup` adds public `OnShowing`/`OnHided` actions. The optional **close button** is no longer a field here — it
moved into `Modules` as the `Closable` feature (`Modules.Close.CloseButton`, revealed when the flag is set); the base
still wires it in Subscribe/Unsubscribe (see below and [[Interaction Modules]]).
**Draw order is deliberately not a field here** — it is authored per popup *type* in the Order tool, not per prefab
([[Hierarchy Order]]); the inspector only shows it read-only.

## Init & cache

- `Awake()` calls `Init()` unless `ManualInit` (then you call `Init()` after instantiating/injecting data).
  **Addressable popups are the exception — they always auto-init** (`!ManualInit || Addressable`): the resolver
  instantiates them and never calls `Init()` itself, so `ManualInit` is ignored for them (the inspector greys it with a
  note, like `AutoHideOnInit`) and stays meaningful only for scene-placed popups (see [[Addressables]]).
- `OnDestroy()` calls `TaskUtils.CancelAndDispose(Source)` (stops any in-flight transition so its loop can't touch the
  destroyed transform) then `AdvancedPopupSystem.DeactivateAdvancedPopup(this)`.
- `Init()` (base): `SetupCache()` → ensure `RectTransform` + `CanvasGroup` → if `AutoHideOnInit` run the cached
  **hide-instantly** (else, if active, show-instantly) → `AdvancedPopupSystem.InitAdvancedPopup(this)`.
- **`SetupCache()` only fills nulls:** both caches null → defaults to `ScaleDisplay`; exactly one null → mixes the set
  one with `ScaleDisplay` (via reflection `SetCachedDisplayMixed`).
- **Override order (important, non-obvious):** call `SetCachedDisplay(...)` **first**, then `base.Init()` **last** — as
  in every shipped example. That way the cache is non-null when `base.Init()` runs, so the auto-hide uses **your**
  display, not the `ScaleDisplay` fallback. (The XML summary saying "keep base.Init() first" is misleading; follow the
  examples — see [[Shipped Docs]].)
- `SetCachedDisplay<T>(showSettings=null)` sets show **and** hide to `T`; `SetCachedDisplay<T,J>(show, hide)` sets them
  separately. Display instances come from `DisplayRegistry`, settings from the arg or
  `DisplaySettingsFactory.GetDefaultSettings<T>()` ([[Displays & Animations]]). The cache (`cachedShowDisplay`,
  `CachedShowSettings`, …) is **runtime-only** — interface refs Unity can't inspector-serialize, so set it in code
  (`Init`), not the inspector.

## Subscribe / Unsubscribe

Guarded by `_isSubscribed` (idempotent). Base `Subscribe()`: wires `Modules.CloseButton.onClick → OnCloseButtonPress`
(→ `Hide()`; `Modules.CloseButton` is the flag-gated accessor — null unless `Closable` is set, see
[[Interaction Modules]]), invokes `OnShowing`, adds to `ActivePopups`. Base `Unsubscribe()`: unwires, invokes `OnHided`,
removes from `ActivePopups`. **Overrides must call base and stay symmetric** ([[Invariants]]) — add/remove your local listeners in
the matching method. `Subscribe` runs at the start of a show, `Unsubscribe` at the start of a hide.

## Show / Hide

Four entry shapes, each in a cached-display and a typed (`<T>`) variant:

- **`Show()` / `Hide()`** (and `Show<T>` / `Hide<T>`) return an `Operation` — fire-and-forget with `.OnComplete()` /
  `.Cancel()` ([[Operations & Cancellation]]).
- **`ShowAsync(token, settings)` / `HideAsync(...)`** (and `<T>`) are the awaitable core.
- **`Cmd_Show` / `Cmd_Hide` / `Cmd_SwitchShowHide`** — `void`, for wiring to `UnityEvent`s / `Button.onClick` in the
  inspector. `SwitchShowHide[...]` toggles based on `IsVisible`/`IsBeVisible`/`Inactive`.

`ShowAsync` flow: bail if `Inactive`/`IsBeVisible` → set `IsBeVisible=true` → refresh the linked CTS
(`TaskUtils.UpdateCancellationTokenSource`, cancels any prior transition) → `APSStats.RegisterTask` → `SetActive(true)`
→ `AdvancedPopupSystem.ApplyOrder(this)` (claim the ordered sibling slot — a show, not the load, decides who is in
front; see [[Hierarchy Order]]) → `Subscribe()` → await the display's `ShowMethod` →
**on cancel roll back `IsBeVisible=false` and return**, else `IsVisible=true`. The `RegisterTask`…`UnregisterTask` pair
brackets the run in a **`try/finally`** (an exception from a display can't leak the `APSStats` task counter).
`HideAsync` is the mirror: `Unsubscribe()`, animate, and on success `SetActive(false)` + `IsVisible=false` (cancel rolls
back to `IsBeVisible=true`). This rollback ordering is the contract displays rely on ([[Invariants]]). For an Addressable
Lane-A popup with `PoolCapacity == 0` (despawn on hide) the success path **releases the handle** instead of `SetActive(false)` ([[Addressables]]).

> **Known gap (not yet fixed):** a Show cancelled by an *external* token (not by a following `Hide`) rolls back only
> `IsBeVisible`; it does **not** `Unsubscribe()` / `SetActive(false)`, so the popup can linger in `ActivePopups` and on
> screen. Blindly adding that rollback regresses the common *Hide-cancels-Show* path (Hide is the one that should own
> teardown), so it needs a proper transition state machine — see the review notes before touching it.

## Data popups (`AdvancedPopup<TData>`)

`AdvancedPopup<TData>` (`Popups/AdvancedPopupData.cs`, a same-name generic beside `AdvancedPopup`; implements
`IDataPopup` — the non-generic `HasData`/`ClearData` surface in `Abstract/`, used by `Despawn`) is the typed
per-open-data base (2026-07-23). The contract:

- **`Bind(TData)` (abstract) is the single place data meets UI** — called from `SetData` only, never from data-less
  shows. `SetData` stores `Data`/`HasData` **before** invoking `Bind` and skips entirely when
  `IsSameData(current, next)` — a virtual whose **default is `false` = always re-bind**. Deliberate: a skipped bind on
  changed data is a stale-UI bug, a redundant bind is only wasted work; popups with expensive binds override this one
  standardized hook (version field, `Equals` for record data) instead of consumers writing ad-hoc dedup checks.
- **`Show(data)`/`ShowAsync(data, …)` bind then show.** No `IsBeVisible` guard before `SetData` on purpose — on a
  visible popup it is a live content update (`ShowAsync`'s own guard prevents a double show). A `Bind` throw inside
  the `Operation` faults it (logged) and the show is skipped — an unconfigured popup never appears.
- **Retention is Lane-A-only:** data survives hide/show, so escape/`LayerShow`/`SwitchShowHide` re-shows render
  the last content with zero re-bind cost. Lane B: `Despawn` clears via `IDataPopup` (a pooled copy must not leak the
  previous use's content); `SpawnAsync<TPopup,TData>(data)` is the paired bind-on-spawn ([[Addressables]]).
- Statics on the system: `Show<TPopup,TData>(data, settings)` and the popup-agnostic `Show<T>(Action<T> configure)`
  ([[Core System]] "Show / hide by type").

## Popup relationships (why there is no `DeepPopups` any more)

**Deleted in 2.2.0** (`DeepPopups`, `ContainsDeepPopup`, `ShownByCascade`, `MarkCascadeShow`, and the `Task.WhenAll`
fan-out in all four show/hide methods). It bundled three unrelated responsibilities, each of which belongs elsewhere:

- **composing a screen** → that is a **layer** ([[Layers]]): `LayerShow` opens its popups together, each with its own
  display, and awaits them all;
- **lifetime dependency** ("B must not outlive A") → one consumer line, and correct under lazy loading because
  `Hide<T>` never loads: `a.OnHided += () => AdvancedPopupSystem.Hide<B>();`
- **escape grouping** → the per-layer `EscapeClosesLayer` flag ([[Core System]] "Escape stack step").

Two decisions worth keeping: the authoring direction was **inverted** (the parent's prefab listed its optional
satellites, so a screen had to know about everything that might attach to it), and it was the **last hard-reference
mechanism** in an otherwise type-keyed, lazily-loaded API — an Addressable popup could not be referenced at all, while
dragging in a *prefab asset* silently made show/hide run against the asset. Don't reintroduce a reference list; if a
grouping need appears, express it as data keyed by type or by layer, like the rest of 2.x.

## Spawning & pooling (was `AdvancedPopupInstantiate`)

The old `AdvancedPopupInstantiate` NoOp stub is **removed** (breaking — [[Invariants]]). Runtime spawn + pooling is now
`AdvancedPopupSystem.SpawnAsync<T>`/`Despawn` over Addressable prefabs (Lane B), and a popup's `PoolCapacity` (`1`
single-instance default / `-1` resident-unlimited / `0` despawn on hide / `N` keep-up-to-N) decides retention for both
lanes. See [[Addressables]].

## Depends on

- [[Displays & Animations]] (the display/settings driving animation), [[Operations & Cancellation]] (CTS, `Operation`,
  `APSStats`), [[Core System]] (registration, the escape stack `EscapePolicy` feeds)
