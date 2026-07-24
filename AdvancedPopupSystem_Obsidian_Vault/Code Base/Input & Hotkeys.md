---
type: code
status: active
description: KeyEventSystemAPS (New vs Old input, PlayerLoop-injected) driving the escape close stack, the per-popup CloseKey override, and Auto Switch Input Module. Read when working on the close key, key-triggered hide, or input-backend behavior.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Input/
  - Assets/advanced-popup-system/Samples/Utils/InputSwitcher.cs
---

# Input & Hotkeys

APS reads the keyboard for **exactly one thing** — stepping the escape close stack. Two **mutually exclusive** static
`KeyEventSystemAPS` implementations live in `AdvancedPS.Core.Input`, chosen by the `HAS_NEWINPUT` define / assembly
([[Project Map]]).

The old per-popup **show/hide key bindings** (`PopupKeyBinding`, `KeyBindingShowSettings`/`KeyBindingHideSettings`, with
their any-key / layer / required-popup gates and `OnTrigger` UnityEvent) were **cut** — they duplicated the escape stack
and nobody used them. Don't reintroduce a second key→popup path; a popup's own key belongs in `CloseKey` (below).

## Runtime wiring

- Both `Initialize()` run under `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and **inject a `PlayerLoopSystem` into
  the `Update` loop** (guarded against double-insert). The escape stack is the only thing that update does, so
  `Settings.EscapeCloseEnabled` **gates the injection itself** — off means zero per-frame cost, but also means flipping
  `IsEnabled` at runtime can't bring it back. `IsEnabled` remains the runtime gate for *temporarily* suppressing the key
  (cutscenes) while the feature is on. In editor, `ExitingPlayMode` restores the default player loop (leak/duplication
  guard — [[Invariants]]).
- New's `Initialize` calls `AutoSwitchInputModule()` **before** that early return — swapping the EventSystem's module is
  about UI input as a whole, not about the escape key.
- The per-frame `Update` bails unless a key was actually pressed this frame (`anyKey`/`anyKeyDown`), then calls
  `AdvancedPopupSystem.EscapeStep(predicate)` — the walk itself lives in [[Core System]].

## Per-popup close key

`IAdvancedPopup.CloseKey` (a single `KeyCode`, `None` by default) overrides `Settings.EscapeCloseKey` **for that popup**.
Read only for `EscapePolicyEnum.Hide` — `Block` swallows any key and `Ignore` is transparent, so neither consults it (the
inspector reveals the field for `Hide` only, [[Editor & Codegen]]). Matching semantics and the "topmost closable popup
owns the press" rule — [[Core System]]; per-popup `EscapePolicy` and the `ShownByCascade` grouping flag — in
[[Popup Lifecycle]].

**One key, not a list** — deliberate (user call, 2026-07-24): a popup closes on one key, and by default *the* key from
the settings. `None` means "inherit", resolved at press time rather than baked into the popup, so editing the setting
still reaches every popup that never overrode it. Don't reintroduce a list.

**Backend seam:** the backends don't resolve *which* key was pressed — core asks *them* about the one candidate key via
a `Predicate<KeyCode>` passed to `EscapeStep`. That is what lets one walk serve two input backends. Each backend caches
the delegate in a `static readonly` field so the hot path allocates nothing; a lambda closing over `Keyboard.current`
would allocate per frame. The Old backend's cached `KeyCode[]` + full `GetKeyDown` scan (it used to need the pressed
key's identity) is **gone** — don't bring it back.

## Backends

- **New** (`Input/New`): `Keyboard.current` (`anyKey.wasPressedThisFrame`, `kb[key].wasPressedThisFrame`) with a cached
  `KeyCode → Key` map (auto-matched by name + manual overrides for `Return→Enter`, `Alpha#→Digit#`, keypad→numpad, etc.).
  The map is still needed — it translates `CloseKey`/`EscapeCloseKey`, which stay `KeyCode` in the public API.
- **Old** (`Input/Old`): `UnityEngine.Input.anyKeyDown` + `UnityEngine.Input.GetKeyDown` as the predicate. Note the
  fully-qualified `UnityEngine.Input` — the enclosing namespace `AdvancedPS.Core.Input` shadows the bare name.

**Not compile-verifiable offline:** the dev project has no Input System package, so `newinput.csproj` lists no sources
and no references. Only the Old backend can be checked by the offline Roslyn recipe — the New one needs a real Unity with
the package installed.

## Auto Switch Input Module

With New Input, `Settings.AutoSwitchInputModule` makes APS swap the EventSystem's `StandaloneInputModule` for
`InputSystemUIInputModule` at startup — implemented both inside the New `KeyEventSystemAPS` and in the sample
`InputSwitcher` (`[RuntimeInitializeOnLoadMethod]`, no manual scene placement). Toggled in the Settings panel (disabled/
help-boxed without the Input System) ([[Settings & Logging]]).

## Depends on

- [[Core System]] (`EscapeStep`, `ActivePopups`, iteration order), [[Popup Lifecycle]] (`IsBeVisible`, `Hide`),
  [[Settings & Logging]] (`EscapeCloseEnabled`, `EscapeCloseKey`, `AutoSwitchInputModule`)
