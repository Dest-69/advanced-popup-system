---
tags: [tree/displays]
type: leaf
parent: "[[Displays & Animations]]"
status: active
description: DoTweenDisplay/DoTweenSettings — runs an arbitrary DOTween Sequence built by a factory. Optional, gated by the DOTWEEN define + its own assembly.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Displays/DoTweenDisplay/
---

# Display — DoTween

**Optional** display for arbitrary DOTween sequences; contract & pipeline in [[Displays & Animations]]. Lives in its own
assembly under the **`DOTWEEN`** define ([[Project Map]]) — absent DoTween, it isn't compiled.

- **`DoTweenSettings`:** built via `DoTweenSettings.Create((rect, seq) => { … })`, which stores a `Factory`
  (`Func<RectTransform, Sequence>`) that produces a **fresh `Sequence` per run**. Engine knobs: `UnscaledTime`,
  `Recyclable`, `AutoKill`, `Link` (+ fluent `WithUnscaledTime/WithRecyclable/WithAutoKill/WithLink`).
- **`DoTweenDisplay`** builds the sequence, applies `SetUpdate/SetRecyclable/SetAutoKill/SetLink`, then awaits it via a
  `TaskCompletionSource` hooked to `onComplete`/`onKill`. **Cancellation kills the sequence** (`token.Register → Kill`),
  resolving the task as not-completed. Instant methods `Goto` the end and `Kill` (no play, no `OnComplete`).
- Show makes the canvas-group visible + `localScale = one` **before** animating; hide sets hidden + `scale = zero`
  **only if the sequence completed** (a killed/cancelled hide leaves state as-is).
- **Gotcha:** a null `Factory` logs an error via `APLogger` and bails — always build through `Create(...)`.
- Usage: `SetCachedDisplay(showDoTweenSettings, hideDoTweenSettings)` in the popup's `Init()` ([[Popup Lifecycle]]).
