using System.Collections.Generic;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Resizes the popup while the pointer drags a grip, keeping the opposite edge fixed and clamping size + bounds.
    /// Size is applied with <see cref="RectTransform.SetSizeWithCurrentAnchors"/> so it is correct for any anchors.
    /// Also drives the hover cursor (<see cref="IPopupCursorHandler"/>): a directional arrow over each grip.
    /// </summary>
    public class ResizeFeatureHandler : IPopupFeatureHandler, IPopupCursorHandler
    {
        public bool TryBegin(IAdvancedPopup popup, Vector2 pointerScreen, Canvas canvas, Camera camera, out GestureState state)
        {
            state = default;

            RectTransform rect = popup.RootTransform;
            RectTransform parent = rect != null ? rect.parent as RectTransform : null;
            if (rect == null || parent == null) return false;

            ResizeConfig cfg = popup.Modules.Resize;
            if (!TryGetGrip(cfg, pointerScreen, camera, out ResizeGrip grip)) return false;
            if (!PopupRectUtility.ScreenToLocal(parent, pointerScreen, camera, out Vector2 startLocal)) return false;

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

        /// <summary>
        /// Cursor feedback: when the pointer is over a grip, report the matching directional arrow from the popup's
        /// <see cref="ResizeConfig.Cursors"/> (or the shipped <see cref="ResizeCursorSet.Default"/> when unset).
        /// </summary>
        public bool TryGetCursor(IAdvancedPopup popup, Vector2 pointerScreen, Camera camera, out PopupCursor cursor)
        {
            cursor = default;

            ResizeConfig cfg = popup.Modules.Resize;
            if (cfg == null || !cfg.ChangeCursor) return false;
            if (!TryGetGrip(cfg, pointerScreen, camera, out ResizeGrip grip)) return false;

            ResizeCursorSet set = cfg.Cursors != null ? cfg.Cursors : ResizeCursorSet.Default;
            if (set == null) return false;

            Texture2D texture = set.Resolve(grip.Direction);
            if (texture == null) return false;

            cursor = new PopupCursor(texture, set.Hotspot);
            return true;
        }

        /// <summary> First grip whose rect contains the pointer (shared by begin + cursor hit-testing). </summary>
        private static bool TryGetGrip(ResizeConfig cfg, Vector2 pointerScreen, Camera camera, out ResizeGrip grip)
        {
            grip = default;
            if (cfg == null || cfg.Grips == null) return false;

            List<ResizeGrip> grips = cfg.Grips;
            for (int i = 0; i < grips.Count; i++)
            {
                ResizeGrip candidate = grips[i];
                if (candidate.Rect == null || candidate.Direction == ResizeDirection.None) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(candidate.Rect, pointerScreen, camera)) continue;

                grip = candidate;
                return true;
            }

            return false;
        }
    }
}
