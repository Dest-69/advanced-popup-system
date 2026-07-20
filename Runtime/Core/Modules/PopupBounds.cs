using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Resolves a <see cref="BoundsMode"/> to a clamp rectangle expressed in the canvas's local space — the form
    /// <see cref="PopupRectUtility.ClampToBounds"/> expects. Keeps <see cref="PopupRectUtility"/> free of feature-level
    /// types (it is pure geometry).
    /// </summary>
    public static class PopupBounds
    {
        /// <summary>
        /// Bounds rect for <paramref name="mode"/> in the local space of <paramref name="canvas"/>, minus
        /// <paramref name="padding"/>. Callers should skip clamping entirely for <see cref="BoundsMode.None"/>.
        /// </summary>
        public static Rect Resolve(Canvas canvas, BoundsMode mode, RectTransform custom, RectOffset padding)
        {
            RectTransform canvasRect = (RectTransform)canvas.transform;

            Rect r;
            switch (mode)
            {
                case BoundsMode.Custom:
                    r = custom != null ? PopupRectUtility.GetLocalAABB(custom, canvas.transform) : canvasRect.rect;
                    break;
                case BoundsMode.SafeArea:
                    r = SafeAreaLocal(canvas, canvasRect);
                    break;
                default:
                    r = canvasRect.rect;
                    break;
            }

            if (padding != null && (padding.left != 0 || padding.right != 0 || padding.top != 0 || padding.bottom != 0))
                r = Rect.MinMaxRect(r.xMin + padding.left, r.yMin + padding.bottom, r.xMax - padding.right, r.yMax - padding.top);

            return r;
        }

        private static Rect SafeAreaLocal(Canvas canvas, RectTransform canvasRect)
        {
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect sa = Screen.safeArea;

            if (!PopupRectUtility.ScreenToLocal(canvasRect, sa.min, cam, out Vector2 min) ||
                !PopupRectUtility.ScreenToLocal(canvasRect, sa.max, cam, out Vector2 max))
                return canvasRect.rect;

            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                                   Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
    }
}
