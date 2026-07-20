---
type: code
status: active
description: IAdvancedPopup + AdvancedPopup — init/cache, subscribe/unsubscribe, show/hide (sync/async/typed), Cmd_/Switch commands, deep popups, close button. Read when creating or changing any popup or its lifecycle.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IAdvancedPopup.cs
  - Assets/advanced-popup-system/Runtime/Core/Popups/AdvancedPopup.cs
---

# Popup Lifecycle

`IAdvancedPopup` (`AdvancedPS.Core.System`) is the **abstract `MonoBehaviour` base** (not an interface — [[Invariants]])
that holds all fields, cache, and `Init`; `AdvancedPopup` (`AdvancedPS.Core`) is the concrete implementation of the
Show/Hide/Switch overrides. **User popups extend `AdvancedPopup`.**

## Inspector fields (on the base)

`PopupLayer` (which layers can show this), `ManualInit`, `AutoHideOnInit` (default `true`), `Inactive` (blocks Show),
`DeepPopups` (child/dependent popups), `KeyBindingShowSettings`/`KeyBindingHideSettings` ([[Input & Hotkeys]]). Hidden:
`RootTransform`, `canvasGroup`, `IsBeVisible` (set when animation **starts**), `IsVisible` (set when it **ends**).
`AdvancedPopup` adds public `OnShowing`/`OnHided` actions and an optional `closeButton`.

## Init & cache

- `Awake()` calls `Init()` unless `ManualInit` (then you call `Init()` after instantiating/injecting data).
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

Guarded by `_isSubscribed` (idempotent). Base `Subscribe()`: wires `closeButton.onClick → OnCloseButtonPress` (→
`Hide()`), invokes `OnShowing`, adds to `ActivePopups`. Base `Unsubscribe()`: unwires, invokes `OnHided`, removes from
`ActivePopups`. **Overrides must call base and stay symmetric** ([[Invariants]]) — add/remove your local listeners in
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
→ `Subscribe()` → `Task.WhenAll` of the display's `ShowMethod` **plus** every `DeepPopups` `ShowAsync` (parallel) →
**on cancel roll back `IsBeVisible=false` and return**, else `IsVisible=true`. The `RegisterTask`…`UnregisterTask` pair
brackets the run in a **`try/finally`** (an exception from a display can't leak the `APSStats` task counter).
`HideAsync` is the mirror: `Unsubscribe()`, animate, and on success `SetActive(false)` + `IsVisible=false` (cancel rolls
back to `IsBeVisible=true`). This rollback ordering is the contract displays rely on ([[Invariants]]).

> **Known gap (not yet fixed):** a Show cancelled by an *external* token (not by a following `Hide`) rolls back only
> `IsBeVisible`; it does **not** `Unsubscribe()` / `SetActive(false)`, so the popup can linger in `ActivePopups` and on
> screen. Blindly adding that rollback regresses the common *Hide-cancels-Show* path (Hide is the one that should own
> teardown), so it needs a proper transition state machine — see the review notes before touching it.

## Deep popups

`DeepPopups` are children/dependents animated **in parallel** with the parent; the parent's `ShowAsync`/`HideAsync`
awaits all of them (`Task.WhenAll`). `ContainsDeepPopup(popup)` is a **cycle-safe DFS** (visited `HashSet`) — use it
before wiring nested popups to avoid loops.

## `AdvancedPopupInstantiate`

A **stub** (all methods `NoOp`/`Task.CompletedTask`) reserved for the planned runtime spawn + pooling feature — not
functional yet ([[Shipped Docs]] lists it as "planned"). Don't build on it.

## Depends on

- [[Displays & Animations]] (the display/settings driving animation), [[Operations & Cancellation]] (CTS, `Operation`,
  `APSStats`), [[Core System]] (registration), [[Input & Hotkeys]] (key bindings)
