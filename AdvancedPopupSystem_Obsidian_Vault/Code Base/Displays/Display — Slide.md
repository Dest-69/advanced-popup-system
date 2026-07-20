---
tags: [tree/displays]
type: leaf
parent: "[[Displays & Animations]]"
status: active
description: SlideDisplay/SlideSettings — animates RectTransform anchoredPosition3D + sizeDelta toward one target. Pivot-anchored only; single-target quirk.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Displays/SlideDisplay/
---

# Display — Slide

Built-in. Animates `RectTransform.anchoredPosition3D` + `sizeDelta`; contract & pipeline in [[Displays & Animations]].

- **`SlideSettings`:** `Duration=0.5`, `Easing=EaseInOutQuad`, `TargetRectPosition` (Vector3), `TargetRectSize`
  (Vector2). `SlideEnum` (Up/Down/Left/Right) exists in the folder but the runtime lerps to explicit target values.
- **Pivot/anchor-based only.** For min–max (stretch) anchors, wrap the popup in an empty parent and slide that — noted
  in the settings XML.
- **Single-target quirk (gotcha):** both show **and** hide lerp from the *current* rect toward the **same**
  `TargetRectPosition`/`TargetRectSize`; hide then collapses `localScale` to zero. So it reads as a slide-**in**; for a
  real slide-**out** pair a different hide display via `SetCachedDisplay<SlideDisplay, T>` ([[Popup Lifecycle]]), or use
  a [[Display — Custom]].
