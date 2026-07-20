---
tags: [tree/displays]
type: leaf
parent: "[[Displays & Animations]]"
status: active
description: ScaleDisplay/ScaleSettings — animates transform.localScale (ShowScale↔HideScale). The default fallback display.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Displays/ScaleDisplay/
---

# Display — Scale

Built-in. Animates `transform.localScale`; contract & pipeline in [[Displays & Animations]].

- **`ScaleSettings`:** `Duration=0.5`, `Easing=EaseInOutQuad`, `ShowScale=Vector3.one`, `HideScale=Vector3.zero`.
- **This is the default display** — `SetupCache()` in [[Popup Lifecycle]] falls back to `ScaleDisplay` when a popup
  caches no display, and it's the "mix" fill when only one direction is set.
- Show lerps `localScale` `initial → ShowScale` (canvas-group made visible up front); hide `initial → HideScale`, then
  sets the canvas-group hidden. `Vector3.LerpUnclamped`, so easings that overshoot (Back/Elastic) work.
