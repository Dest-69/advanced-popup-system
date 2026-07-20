---
tags: [tree/displays]
type: code
status: active
description: Display/animation pipeline — IDisplay/DisplayBase contract, BaseSettings, DisplayRegistry, DisplaySettingsFactory, cached vs per-call. Hub for the built-in and custom displays. Read when writing or wiring any transition.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IDisplay.cs
  - Assets/advanced-popup-system/Runtime/Core/Abstract/BaseSettings.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/DisplayRegistry.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/DisplaySettingsFactory.cs
  - Assets/advanced-popup-system/Runtime/Generated/Displays/
---

# Displays & Animations

A **display** is a stateless animation runner; its **settings** are the per-popup data it animates toward. A popup
caches one display+settings for show and one for hide ([[Popup Lifecycle]]); layer/typed calls can override per-call.

## Contract

- **`IDisplay`** (`AdvancedPS.Core.System`) — untyped surface the popup calls: `ShowInstantlyMethod`/
  `HideInstantlyMethod` (immediate state) + `ShowMethod`/`HideMethod` (awaitable, `Task` + `CancellationToken`), all
  taking `RectTransform` + `IDisplaySettings`.
- **`IDisplay<TSettings>` / `DisplayBase<TSettings>`** — the typed base you extend. `DisplayBase` implements the untyped
  `IDisplay` by coercing `IDisplaySettings → TSettings` through **`Resolve()`: null or a mismatched type falls back to
  `new TSettings()`** — the "if null, defaults are used" contract. This is what makes a typed call
  (`Show<T>`/`LayerShow<T>`/`HideAll<T>`) safe on a popup whose cached settings are a *different* display type; a raw
  `(TSettings)` cast would NRE/InvalidCast there. You only implement the four typed methods. `TSettings :
  IDisplaySettings, new()`.
- **`IDisplaySettings` / `BaseSettings<TDisplay>`** — every settings class extends `BaseSettings<TDisplay>`, which
  exposes `DisplayType => typeof(TDisplay)`, the `OnAnimationStart` / `OnAnimationEnd` actions (all built-ins fire them),
  and **`UnscaledTime`** (drive the loop with `Time.unscaledDeltaTime`; needed for popups that animate while
  `Time.timeScale == 0`, e.g. pause menus — built-ins and DoTween honor it). `[Serializable]`.

## Caching & allocation

- **`DisplayRegistry.Get<T>()`** returns a **lazy singleton** per display type (`ConcurrentDictionary<Type,
  Lazy<IDisplay>>`). Displays are stateless, so one instance is shared — animation state lives on the `RectTransform`/
  `CanvasGroup` and in local vars, never on the display. **Keep displays stateless** or this breaks.
- **`DisplaySettingsFactory.GetDefaultSettings<T>()`** builds default settings by reflection (cached per type):
  it finds the `IDisplay<TSettings>` interface on `T`, prefers a public static **`TSettings Default()`**, else a
  parameterless ctor. So a settings class needs a parameterless ctor **or** a static `Default()`.
- **Cached vs per-call:** `popup.Show()` uses the popup's cached display+settings; `popup.Show<T>(settings)` /
  `AdvancedPopupSystem.LayerShow<T>(…)` run a **per-call** display type `T` with the given (or cached-cast) settings —
  the display instance still comes from `DisplayRegistry` ([[Core System]]).

## Animation shape (how the built-ins are written)

Instant methods set the end state directly. Async methods: fire `OnAnimationStart` → loop accumulating
`settings.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime`, `t = elapsed/Duration`, `easedT =
EasingFunctions.Get(settings.Easing, t)`, `LerpUnclamped` toward the target, `await Task.Yield()` each frame, **bail via
`TaskUtils.OperationCancelled(token)`** → set the exact final state and fire `OnAnimationEnd`. (Note: with scaled time
under `timeScale == 0` the loop parks until time resumes — that's why pause-menu popups need `UnscaledTime`.) `CanvasGroup.interactable`/`blocksRaycasts` are toggled with visibility.
Easing curves: `EasingType` (30 curves, `Linear`…`EaseInOutBounce`) via `EasingFunctions.Get` (`Core/Easing`).

## Variants (tree)

Built-ins live under `Runtime/Generated/Displays/`; add your own via the **Displays** panel ([[Editor & Codegen]]).

- [[Display — Fade]] — `CanvasGroup.alpha` (Min/Max)
- [[Display — Scale]] — `transform.localScale` (Show/Hide scale)
- [[Display — Slide]] — `RectTransform.anchoredPosition`/`sizeDelta` (pivot-anchored)
- [[Display — DoTween]] — arbitrary DOTween `Sequence` (optional, `DOTWEEN` define)
- [[Display — Custom]] — how to author a new display + settings

## Depends on

- [[Popup Lifecycle]] (who caches/calls displays), [[Operations & Cancellation]] (token + cancel semantics)
