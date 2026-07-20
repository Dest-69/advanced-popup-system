---
type: code
status: active
description: KeyEventSystemAPS (New vs Old input, PlayerLoop-injected), PopupKeyBinding, and Auto Switch Input Module. Read when working on hotkeys, key-triggered show/hide, or input-backend behavior.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Input/
  - Assets/advanced-popup-system/Samples/Utils/InputSwitcher.cs
---

# Input & Hotkeys

Popups can toggle themselves on key presses. Two **mutually exclusive** static `KeyEventSystemAPS` implementations live
in `AdvancedPS.Core.Input`, chosen by the `HAS_NEWINPUT` define / assembly ([[Project Map]]).

## Runtime wiring

- Both `Initialize()` under `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` **inject a `PlayerLoopSystem` into the
  `Update` loop** (guarded against double-insert). In editor, `ExitingPlayMode` restores the default player loop
  (leak/duplication guard — [[Invariants]]). `IsEnabled` mirrors `Settings.KeyEventSystemEnabled`
  ([[Settings & Logging]]).
- The per-frame `Update` scans `AdvancedPopupSystem.AllPopups` and, on the **first** matching popup, calls `Show()` or
  `Hide()` and fires the binding's `OnTrigger`, then `break`s. Iteration order is deepest-first ([[Core System]]).

## Match conditions (per popup, per direction)

A show binding fires when: popup **not** `IsBeVisible` **and** (`AnyHotKey` or a bound key was pressed) **and** the
`Layers` gate passes (`default` or `ActiveLayer` has the flag) **and** required `Popups` are visible **and**
**`AreParentsVisible`** (every ancestor popup is `IsBeVisible`). Hide binding is the mirror for a visible popup. The
`Popups`/parent checks make nested popups' keys context-aware.

## Backends

- **New** (`Input/New`): uses `Keyboard.current` (`anyKey.wasPressedThisFrame`, `kb[key].wasPressedThisFrame`) with a
  cached `KeyCode → Key` map (auto-matched by name + manual overrides for `Return→Enter`, `Alpha#→Digit#`, keypad→
  numpad, etc.).
- **Old** (`Input/Old`): uses `UnityEngine.Input.anyKeyDown` + a `KeyCode` scan (`GetKeyDown`) over a **cached**
  `KeyCode[]` (`Enum.GetValues` runs once at load, not per keypress).

**Per-frame allocation gotcha:** the `Update` scan runs on the hot path, so it must stay allocation-free — the `Layers`
gate uses a bitwise `(Layers & ActiveLayer) == ActiveLayer` check, **not** `Enum.HasFlag` (which boxes under IL2CPP),
and the Old backend iterates the cached `KeyCode[]`. Don't reintroduce `HasFlag` or `Enum.GetValues` in these loops.

## PopupKeyBinding

`struct` with two instances per popup (`KeyBindingShowSettings` / `KeyBindingHideSettings`): `AnyHotKey`, `HotKeys`
(`List<KeyCode>`), `Layers` (gate), `Popups` (required visible), `OnTrigger` (`UnityEvent`).

## Escape close stack

One key steps back through open popups, Android-back style. Both backends, in `Update` **before** the binding scan:
if `Settings.EscapeCloseEnabled`, the `Settings.EscapeCloseKey` was pressed and `AdvancedPopupSystem.EscapeStep()`
returned true → the frame is **consumed** (binding scan skipped, so one press can't also fire a binding or an
`AnyHotKey` show). The walk itself lives in [[Core System]]; per-popup `EscapePolicy` and the `ShownByCascade`
grouping flag — in [[Popup Lifecycle]]. Key-driven path needs **both** `KeyEventSystemEnabled` and
`EscapeCloseEnabled` ([[Settings & Logging]]); calling `EscapeStep()` manually (UI "back" button) works regardless.

## Auto Switch Input Module

With New Input, `Settings.AutoSwitchInputModule` makes APS swap the EventSystem's `StandaloneInputModule` for
`InputSystemUIInputModule` at startup — implemented both inside the New `KeyEventSystemAPS` and in the sample
`InputSwitcher` (`[RuntimeInitializeOnLoadMethod]`, no manual scene placement). Toggled in the Settings panel (disabled/
help-boxed without the Input System) ([[Settings & Logging]]).

## Depends on

- [[Core System]] (`AllPopups`, iteration order, `ActiveLayer`), [[Popup Lifecycle]] (`IsBeVisible`, `Show`/`Hide`),
  [[Settings & Logging]] (`KeyEventSystemEnabled`, `AutoSwitchInputModule`)
