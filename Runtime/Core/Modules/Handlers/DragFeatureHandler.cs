using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Moves the popup while the pointer drags its drag zone. Dragging applies a pointer <b>delta</b> to
    /// anchoredPosition, so it is anchor-independent; only the clamp needs the popup's bounds.
    /// </summary>
    public class DragFeatureHandler : IPopupFeatureHandler
    {
        public bool TryBegin(IAdvancedPopup popup, Vector2 pointerScreen, Canvas canvas, Camera camera, out GestureState state)
        {
            state = default;

            RectTransform rect = popup.RootTransform;
            RectTransform parent = rect != null ? rect.parent as RectTransform : null;
            if (rect == null || parent == null) return false;

            DragConfig cfg = popup.Modules.Drag;
            RectTransform zone = cfg != null && cfg.DragZone != null ? cfg.DragZone : rect;

            if (!RectTransformUtility.RectangleContainsScreenPoint(zone, pointerScreen, camera)) return false;
            if (!PopupRectUtility.ScreenToLocal(parent, pointerScreen, camera, out Vector2 startLocal)) return false;

            state.Popup = popup;
            state.Rect = rect;
            state.Parent = parent;
            state.Canvas = canvas;
            state.Camera = camera;
            state.PointerStartLocal = startLocal;
            state.InitialAnchoredPos = rect.anchoredPosition;
            return true;
        }

        public void Update(Vector2 pointerScreen, ref GestureState state)
        {
            if (!PopupRectUtility.ScreenToLocal(state.Parent, pointerScreen, state.Camera, out Vector2 cur)) return;

            state.Rect.anchoredPosition = state.InitialAnchoredPos + (cur - state.PointerStartLocal);

            DragConfig cfg = state.Popup.Modules.Drag;
            if (cfg == null || cfg.Bounds == BoundsMode.None) return;

            Rect bounds = PopupBounds.Resolve(state.Canvas, cfg.Bounds, cfg.CustomBounds, cfg.Padding);
            PopupRectUtility.ClampToBounds(state.Rect, state.Parent, state.Canvas, bounds);
        }

        public void End(ref GestureState state) { }
    }
}
