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

- **DoTweenShowcase** (`DOTWEEN` define) — mass-instantiates popups, each caching a per-instance `DoTweenSettings`
  (`DOLocalJump`/`DOLocalMove`), and loops `Show().OnComplete(() => Hide().OnComplete(...))`. Its stats panel reads
  `ActivePopups`, `APSStats.ActiveOperationsCount/ActiveTasksCount`, and DOTween totals — the canonical
  [[Operations & Cancellation]] monitor.
- **EasingShowcase** — demonstrates the `EasingType` curves (with `InfoBlock`) via the built-in displays.
- **PerformanceShowcase** — stress test of many simultaneous show/hide operations.
- **AddressablesShowcase** (`APS_ADDRESSABLES` define) — demos on-demand loading ([[Addressables]]): a scene popup
  (immediate), a lazy Addressable popup (HUB), a preloaded-at-boot Addressable popup (LOGIN), and pooled `SpawnAsync`
  toasts, driven by a code-built button bar; all draggable. The demo popups build their own visuals in `Init`, so the
  prefabs are bare (one distinct `AdvancedPopup` subclass per type — the index is type-keyed).
- **InputSwitcher** (`Samples/Utils`) — auto-swaps the EventSystem input module under `ENABLE_INPUT_SYSTEM` when
  `AutoSwitchInputModule` is on ([[Input & Hotkeys]]); `[RuntimeInitializeOnLoadMethod]`, no scene placement needed.

Samples are shipped in the package (`package.json` is a library; scenes/prefabs live here) but are **not** part of the
public API — patterns here can change with the demo without a version concern.

## Depends on

- [[Popup Lifecycle]], [[Displays & Animations]], [[Operations & Cancellation]] (what the demos exercise)
