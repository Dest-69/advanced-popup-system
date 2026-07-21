---
type: code
status: active
description: Sample showcases (DoTween / Easing / Performance / Addressables) and the InputSwitcher util — what each demonstrates and its asmdef/define. Read when editing samples or reproducing usage patterns.
code_paths:
  - Assets/advanced-popup-system/Samples/
---

# Samples

Showcase scenes + scripts under `Samples/`, each with its own `AdvancedPS.Core.Examples` assembly ([[Project Map]]).
They are also the most faithful **usage references** for the public API.

**All showcases run on the Addressables pipeline** ([[Addressables]]). Each has its **own distinct `AdvancedPopup`
subclass** (the index is type-keyed — no sharing between samples), flagged Addressable on its prefab, and materializes
copies via `AdvancedPopupSystem.SpawnAsync<T>` (Lane B) instead of `Instantiate`. Consequently they now **require the
Addressables package** — every sample below is gated on `APS_ADDRESSABLES` (the DoTween one on `DOTWEEN` **and**
`APS_ADDRESSABLES`), mirroring the AddressablesShowcase asmdef. Their prefabs keep authored visuals (unlike the bare,
Init-built AddressablesShowcase demos), so the subclasses are empty type-markers. `GeneratePopups` is `async` and the
action buttons stay disabled until spawning finishes.

- **DoTweenShowcase** (`DOTWEEN` + `APS_ADDRESSABLES`) — spawns N `TweenPopupDemo` via `SpawnAsync`, each caching a
  per-instance `DoTweenSettings` (`DOLocalJump`/`DOLocalMove`), and loops `Show().OnComplete(() => Hide().OnComplete(...))`.
  Its stats panel reads `ActivePopups`, `APSStats.ActiveOperationsCount/ActiveTasksCount`, and DOTween totals — the
  canonical [[Operations & Cancellation]] monitor.
- **EasingShowcase** (`APS_ADDRESSABLES`) — spawns one `EasingPopupDemo` per `EasingType` (with `InfoBlock`), each caching
  a `SlideSettings` for that curve. Shows/hides the grid by **iterating the spawned list** — Lane B popups aren't in layer
  batches, so it no longer uses `LayerShow`/`LayerHide`.
- **PerformanceShowcase** (`APS_ADDRESSABLES`) — stress test of many simultaneous show/hide operations on
  `SpawnAsync`-ed `PerformancePopupDemo` copies.
- **AddressablesShowcase** (`APS_ADDRESSABLES` define) — demos on-demand loading ([[Addressables]]): a scene popup
  (immediate), a lazy Addressable popup (MENU), a preloaded-at-boot Addressable popup (GUI), and pooled `SpawnAsync`
  toasts, driven by a code-built button bar; all draggable. The demo popups build their own visuals in `Init`, so the
  prefabs are bare (one distinct `AdvancedPopup` subclass per type — the index is type-keyed).
- **InputSwitcher** (`Samples/Utils`) — auto-swaps the EventSystem input module under `ENABLE_INPUT_SYSTEM` when
  `AutoSwitchInputModule` is on ([[Input & Hotkeys]]); `[RuntimeInitializeOnLoadMethod]`, no scene placement needed.

**Gotcha:** flagging a prefab Addressable only takes effect once the editor postprocessor regenerates the index asset +
Addressables group ([[Addressables]] "Editor tooling"). After a fresh checkout or a **hand-edited** `Addressable` flag,
open the project once so the postprocessor fires — until then `SpawnAsync` finds no index entry and the samples spawn
nothing (they log and degrade gracefully). The consumer-side index asset/group are not shipped (regenerated on import).

Samples are shipped in the package (`package.json` is a library; scenes/prefabs live here) but are **not** part of the
public API — patterns here can change with the demo without a version concern.

## Depends on

- [[Popup Lifecycle]], [[Displays & Animations]], [[Operations & Cancellation]] (what the demos exercise)
