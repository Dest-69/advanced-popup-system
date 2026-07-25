---
type: code
status: active
description: The pointer input backends (New vs Old, PlayerLoop-injected) and Auto Switch Input Module. APS reads no keyboard at all — the escape stack is stepped from consumer code. Read when working on input-backend behavior.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Input/
  - Assets/advanced-popup-system/Samples/Utils/InputSwitcher.cs
---

# Input Backends

APS polls **only the pointer**, and only for the drag/resize gestures ([[Interaction Modules]]). Two **mutually
exclusive** static `PointerEventSystemAPS` implementations live in `AdvancedPS.Core.Input`, chosen by the `HAS_NEWINPUT`
define / assembly ([[Project Map]]).

## No key handling — deliberate, cut twice

Two key→popup paths existed and both were removed, in this order:

1. Per-popup **show/hide key bindings** (`PopupKeyBinding`, `KeyBindingShowSettings`/`KeyBindingHideSettings` with their
   any-key / layer / required-popup gates) — they duplicated the escape stack and nobody used them.
2. The **escape key** path itself (user call, 2026-07-25): `KeyEventSystemAPS` (both backends), the per-popup
   `CloseKey`, `Settings.EscapeCloseEnabled`/`EscapeCloseKey`, and `EscapeStep(Predicate<KeyCode>)` with its
   `MatchesCloseKey` fallback resolution. The stack is now driven **purely from consumer code** —
   `AdvancedPopupSystem.EscapeStep()` plus the membership API ([[Core System]] "Escape stack step").

**Don't reintroduce either.** A library that owns a key fights the consumer's input system, and every rule key matching
needed died with it: the "topmost closable popup owns the press" drop, the per-press settings fallback, the inspector's
conditional Close Key row, and the `KeyCode → Key` translation map the New backend carried only to serve `KeyCode` in
the public API.

## Runtime wiring

- `PointerEventSystemAPS.Initialize()` runs under `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and **injects a
  `PlayerLoopSystem` into the `Update` loop** (guarded against double-insert); in editor, `ExitingPlayMode` restores the
  default player loop (leak/duplication guard — [[Invariants]]). The per-frame tick and its gesture contract live in
  [[Interaction Modules]].
- **`AutoSwitchInputModule` is owned by the New backend's `Initialize`**, called *before* the player-loop wiring and
  regardless of its outcome — swapping the EventSystem's module is about UI input as a whole, not about popup gestures.
  It moved here when `KeyEventSystemAPS` (its previous home) was deleted, and is now the only startup job APS has under
  the New Input System besides the pointer loop.

## Backends

- **New** (`Input/New`): `Pointer.current` — covers mouse, pen and touch — plus `AutoSwitchInputModule`.
- **Old** (`Input/Old`): `UnityEngine.Input` mouse with a primary-touch fallback for devices. Note the fully-qualified
  `UnityEngine.Input` — the enclosing namespace `AdvancedPS.Core.Input` shadows the bare name.

**Not compile-verifiable offline:** the dev project has no Input System package, so `newinput.csproj` lists no sources
and no references. Only the Old backend can be checked by the offline Roslyn recipe — the New one needs a real Unity with
the package installed.

## Auto Switch Input Module

With New Input, `Settings.AutoSwitchInputModule` makes APS swap the EventSystem's `StandaloneInputModule` for
`InputSystemUIInputModule` at startup — implemented both inside the New `PointerEventSystemAPS` and in the sample
`InputSwitcher` (`[RuntimeInitializeOnLoadMethod]`, no manual scene placement). Toggled in the Settings panel (disabled/
help-boxed without the Input System) ([[Settings & Logging]]).

## Depends on

- [[Interaction Modules]] (the pointer tick contract), [[Settings & Logging]] (`AutoSwitchInputModule`)
