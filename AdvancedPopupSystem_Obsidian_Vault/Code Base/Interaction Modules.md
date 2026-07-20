---
type: code
status: active
tags: [tree/modules]
description: Interaction modules — data-driven drag/resize via a PopupFeatureEnum flag set + per-feature config on the popup, a central PlayerLoop pointer system (New/Old), stateless handlers + registry, and anchor/pivot-agnostic clamp math. Read when touching drag, resize, pointer input, or adding an interactive feature.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/Modules/
  - Assets/advanced-popup-system/Runtime/Core/Input/New/PointerEventSystemAPS.cs
  - Assets/advanced-popup-system/Runtime/Core/Input/Old/PointerEventSystemAPS.cs
  - Assets/advanced-popup-system/Runtime/Utils/PopupRectUtility.cs
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IPopupFeatureHandler.cs
  - Assets/advanced-popup-system/Runtime/Core/Abstract/GestureState.cs
---

# Interaction Modules

Runtime **pointer interactions** on a popup — drag and resize in v1 — that keep the popup inside a bounds rect for
**any anchors/pivot**. Deliberately **not** displays: displays are stateless one-shot animators driven by Show/Hide
([[Displays & Animations]]); interactions are stateful, continuous, input-driven. The design borrows the displays'
*extensibility shape* (contract + settings + registry) but not their lifetime.

## Data model (why a flag enum, not components)

Features are **data on the popup**, not per-feature MonoBehaviours (explicit product decision — no pile of runtime
behaviours):

- **`IAdvancedPopup.Modules`** — one serialized `PopupModules` field: `PopupFeatureEnum Features` (a `[Flags]` set:
  `Draggable`, `Resizable`) + per-feature configs `DragConfig` / `ResizeConfig`. `HasAny` = any flag set.
- **Configs** hold references + tunables: `DragConfig` (`DragZone`, `Bounds`, `CustomBounds`, `Padding`);
  `ResizeConfig` (`Grips` list, `MinSize`, `MaxSize`, `Bounds`, `CustomBounds`, `Padding`). A `ResizeGrip` is just a
  `{ RectTransform Rect, ResizeDirection Direction }` pair — hit-tested by rect, **no Graphic/raycast target needed**.
- The custom inspector (`IAdvancedPopupEditor.DrawModules`) draws a **"Modules" box**: an `EnumFlagsField`, then a
  config block **revealed only for each enabled flag** (same reveal pattern as the key-binding hot-keys). Resize adds a
  **Generate Grips** button → `GenerateGrips` builds a default 8-grip set under a dedicated **`[Grips]` root as the last
  sibling** (renders on top, one layer to manage) and writes them into `Resize.Grips`; positions are then hand-tweaked.

*Extensibility trade-off vs displays:* a C# enum is a **closed set** (max 31 flags, recompile to add) — unlike the
open display registry. Chosen for the requested tick-box UX. To make it open later, **generate `PopupFeatureEnum`** from
an APS panel exactly like `PopupLayerEnum` is generated ([[Editor & Codegen]]).

## Runtime pipeline (no MonoBehaviours)

Mirrors [[Input & Hotkeys]] — a **static system injected into the PlayerLoop `Update`**, not a scene object:

- **`PointerEventSystemAPS`** (one per input backend, `Input/New` + `Input/Old`, chosen by `HAS_NEWINPUT` like
  `KeyEventSystemAPS`) injects a `PlayerLoopSystem` (double-insert guarded; restores the default loop on
  `ExitingPlayMode`). Each frame it reads **only** the pointer position + primary-button state — New: `Pointer.current`
  (covers mouse/pen/touch); Old: `UnityEngine.Input` mouse with a primary-touch fallback — and calls
  `PopupInteractionSystem.Tick(pos, pressed)`. All real logic is backend-agnostic in core; the backend files are thin.
- **`PopupInteractionSystem`** (core, static) — the gesture state machine. Runs **at most one gesture** at a time.
  Hit-testing happens **only on the press edge**: idle frame = one bool check, active frame = one handler `Update`, zero
  per-frame allocations. Holds no per-scene collection — only the single in-flight `GestureState`, `Reset()` on
  `ExitingPlayMode` (its own leak guard, [[Core System]]).
- On press it walks **`AdvancedPopupSystem.ActivePopups` from the end** (recency stack = topmost first), skips
  non-`IsVisible` / no-feature popups, resolves the popup's `Canvas`/camera, then tries each registered handler whose
  flag is set. First `TryBegin` that returns true owns the gesture until release / hide / destroy.

## Handlers & registry (the displays-like part)

- **`IPopupFeatureHandler`** (`Core/Abstract`, next to `IDisplay`) — `TryBegin` / `Update` / `End`. **Stateless**: a
  single shared instance per feature; all per-gesture data lives in **`GestureState`** (rect/parent/canvas/camera, grab
  pointer + initial pos/size, grip dir), passed by ref. Same "keep it stateless, state lives elsewhere" rule as
  displays.
- **`PopupFeatureRegistry`** — maps flag → handler in **priority order** (`Resizable` before `Draggable`: a grip near
  an edge must win over the drag zone beneath it). Built-ins registered in the static ctor; **`Register(flag, handler,
  prepend)`** is the open extension point (parallel to `DisplayRegistry`). Stateless handlers only → no leak guards.

## Geometry — "any anchors" (`PopupRectUtility`, [[Utilities]])

Pure, allocation-free (`Utils`, one reused `Vector3[4]` buffer, main-thread only). The two hard problems:

- **Drag is anchor-independent by construction:** it applies a pointer **delta** to `anchoredPosition` (recomputed from
  the grab each frame — no drift), so the anchor never enters the move. Anchors only matter for the clamp.
- **`ClampToBounds`** is anchor/pivot/scale-agnostic: compares the popup's **world-corner AABB** (`GetLocalAABB`, in the
  canvas's local space) against a bounds rect, then converts the push-back through `canvas.TransformVector` →
  `parent.InverseTransformVector` back into an `anchoredPosition` delta. `PopupBounds.Resolve` turns a `BoundsMode`
  (`Canvas` = the canvas rect ≈ screen for Overlay / `SafeArea` / `Custom` / `None`) minus `Padding` into that rect.
- **`ResizeKeepingOppositeEdge`** computes new size + `anchoredPosition` so the edge opposite the grabbed grip stays
  fixed (pivot-aware: `pos.x += pivot.x·ΔW` for a right grab, `-(1-pivot.x)·ΔW` for a left grab; same on Y), clamped to
  `[Min,Max]` (max ≤ 0 = unlimited). The handler applies it via `SetSizeWithCurrentAnchors` (correct for stretched
  anchors) then re-clamps to bounds.

## Gotchas / limits (v1)

- **Enabled only while `IsVisible`** (fully shown); a gesture cancels if the popup hides/destroys mid-drag.
- **Pointer, not EventSystem:** hit-testing is `RectTransformUtility.RectangleContainsScreenPoint` on the zone/grip
  rect — it does **not** know about other UI on top (no raycast). Topmost = first visible feature-popup in the recency
  stack. This is the deliberate "APS input, optimize later" path — refine to a proper raycast if overlap matters.
- **Resize doesn't clamp *size* to bounds**, only repositions after — growing a grip at a screen edge shoves the whole
  popup inward. Fine for popups smaller than the bounds; revisit if needed.
- **Stretched anchors on a resized axis** are approximate (size maps to insets); drag/resize target fixed-anchor popups.
- No settings toggle yet (unlike `KeyEventSystemEnabled`): `PointerEventSystemAPS.IsEnabled` defaults true. Add a
  `PopupSettings` flag + panel toggle if a global off-switch is wanted ([[Settings & Logging]]).

## Depends on

- [[Popup Lifecycle]] (`Modules` field lives on the base; `IsVisible`/`ActivePopups` gate interaction),
  [[Core System]] (`ActivePopups` recency stack, play-mode cleanup contract), [[Input & Hotkeys]] (the PlayerLoop-inject
  + New/Old backend pattern this copies), [[Utilities]] (`PopupRectUtility` placement, `APLogger`).
