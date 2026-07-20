using System.Collections.Generic;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Resizes the popup while the pointer drags a grip, keeping the opposite edge fixed and clamping size + bounds.
    /// Size is applied with <see cref="RectTransform.SetSizeWithCurrentAnchors"/> so it is correct for any anchors.
    /// </summary>
    public class ResizeFeatureHandler : IPopupFeatureHandler
    {
        public bool TryBegin(IAdvancedPopup popup, Vector2 pointerScreen, Canvas canvas, Camera camera, out GestureState state)
        {
            state = default;

            RectTransform rect = popup.RootTransform;
            RectTransform parent = rect != null ? rect.parent as RectTransform : null;
            if (rect == null || parent == null) return false;

            ResizeConfig cfg = popup.Modules.Resize;
            if (cfg == null || cfg.Grips == null) return false;

            List<ResizeGrip> grips = cfg.Grips;
            for (int i = 0; i < grips.Count; i++)
            {
                ResizeGrip grip = grips[i];
                if (grip.Rect == null || grip.Direction == ResizeDirection.None) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(grip.Rect, pointerScreen, camera)) continue;
                if (!PopupRectUtility.ScreenToLocal(parent, pointerScreen, camera, out Vector2 startLocal)) continue;

                state.Popup = popup;
                state.Rect = rect;
                state.Parent = parent;
                state.Canvas = canvas;
                state.Camera = camera;
                state.PointerStartLocal = startLocal;
                state.InitialAnchoredPos = rect.anchoredPosition;
                state.InitialSize = rect.rect.size;
                state.GripDir = grip.Direction;
                return true;
            }

            return false;
        }

        public void Update(Vector2 pointerScreen, ref GestureState state)
        {
            if (!PopupRectUtility.ScreenToLocal(state.Parent, pointerScreen, state.Camera, out Vector2 cur)) return;

            ResizeConfig cfg = state.Popup.Modules.Resize;
            Vector2 delta = cur - state.PointerStartLocal;

            PopupRectUtility.ResizeKeepingOppositeEdge(state.InitialSize, state.InitialAnchoredPos, state.Rect.pivot,
                delta, state.GripDir, cfg.MinSize, cfg.MaxSize, out Vector2 size, out Vector2 pos);

            state.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            state.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            state.Rect.anchoredPosition = pos;

            if (cfg.Bounds == BoundsMode.None) return;
            Rect bounds = PopupBounds.Resolve(state.Canvas, cfg.Bounds, cfg.CustomBounds, cfg.Padding);
            PopupRectUtility.ClampToBounds(state.Rect, state.Parent, state.Canvas, bounds);
        }

        public void End(ref GestureState state) { }
    }
}
