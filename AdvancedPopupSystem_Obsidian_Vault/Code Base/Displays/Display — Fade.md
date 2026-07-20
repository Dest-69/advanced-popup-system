---
tags: [tree/displays]
type: leaf
parent: "[[Displays & Animations]]"
status: active
description: FadeDisplay/FadeSettings — animates CanvasGroup.alpha (MinValue↔MaxValue) and toggles localScale one/zero.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Displays/FadeDisplay/
---

# Display — Fade

Built-in. Animates `CanvasGroup.alpha`; contract & pipeline in [[Displays & Animations]].

- **`FadeSettings`:** `Duration=0.5`, `Easing=EaseInOutQuad`, `MaxValue=1` (shown alpha), `MinValue=0` (hidden alpha).
- Show lerps `alpha` `initial → MaxValue`; hide `initial → MinValue`, then finalizes the canvas-group state
  (`alpha/interactable/blocksRaycasts`).
- **Also forces `localScale`** to `Vector3.one` on show and `Vector3.zero` on hide (so a faded-out popup neither catches
  raycasts nor holds layout) — don't rely on a Fade popup's scale for anything else.
