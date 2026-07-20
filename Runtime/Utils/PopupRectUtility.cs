using UnityEngine;

namespace AdvancedPS.Core.Utils
{
    /// <summary>
    /// Stateless geometry helpers shared by every interaction feature (built-in drag/resize and any custom handler):
    /// anchor- and pivot-agnostic clamping, screen↔local conversion, and edge-fixed resizing. Reuses a single corner
    /// buffer — no per-frame allocations. Main-thread only (the shared buffer is not reentrant).
    /// </summary>
    public static class PopupRectUtility
    {
        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary> Screen point → local point inside <paramref name="space"/>. </summary>
        public static bool ScreenToLocal(RectTransform space, Vector2 screen, Camera camera, out Vector2 local)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, camera, out local);

        /// <summary>
        /// Axis-aligned bounds of <paramref name="target"/> expressed in <paramref name="space"/>'s local coordinates.
        /// Uses world corners, so it is correct for any anchors, pivot, rotation-free nesting and scale.
        /// </summary>
        public static Rect GetLocalAABB(RectTransform target, Transform space)
        {
            target.GetWorldCorners(_corners);

            Vector3 p = space.InverseTransformPoint(_corners[0]);
            float minX = p.x, maxX = p.x, minY = p.y, maxY = p.y;
            for (int i = 1; i < 4; i++)
            {
                p = space.InverseTransformPoint(_corners[i]);
                if (p.x < minX) minX = p.x;
                else if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                else if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Pushes <paramref name="rect"/> back inside <paramref name="boundsLocal"/> (a rectangle in the canvas's local
        /// space) by adjusting its anchoredPosition. Anchor/pivot-agnostic: it compares world-corner AABBs and converts
        /// the correction back through the parent, so any anchors and nested scaling are handled. No-op when already in.
        /// </summary>
        public static void ClampToBounds(RectTransform rect, RectTransform parent, Canvas canvas, Rect boundsLocal)
        {
            Transform canvasT = canvas.transform;
            Rect aabb = GetLocalAABB(rect, canvasT);

            float dx = 0f, dy = 0f;
            if (aabb.xMin < boundsLocal.xMin) dx = boundsLocal.xMin - aabb.xMin;
            else if (aabb.xMax > boundsLocal.xMax) dx = boundsLocal.xMax - aabb.xMax;
            if (aabb.yMin < boundsLocal.yMin) dy = boundsLocal.yMin - aabb.yMin;
            else if (aabb.yMax > boundsLocal.yMax) dy = boundsLocal.yMax - aabb.yMax;
            if (dx == 0f && dy == 0f) return;

            Vector3 worldDelta = canvasT.TransformVector(dx, dy, 0f);
            Vector2 parentDelta = parent.InverseTransformVector(worldDelta);
            rect.anchoredPosition += parentDelta;
        }

        /// <summary>
        /// Computes the new size and anchoredPosition for a resize gesture that keeps the edge opposite the grabbed
        /// grip fixed, honoring the current pivot. Clamps each axis to [min, max] (max &lt;= 0 = unlimited). Designed
        /// for non-stretched anchors on the resized axis; on a stretched axis behaviour is approximate.
        /// </summary>
        /// <param name="initialSize"> Rect size at grab. </param>
        /// <param name="initialPos"> anchoredPosition at grab. </param>
        /// <param name="pivot"> Rect pivot. </param>
        /// <param name="deltaLocal"> Pointer movement since grab, in parent-local space. </param>
        /// <param name="dir"> Grabbed edge/corner. </param>
        /// <param name="minSize"> Minimum size (px). </param>
        /// <param name="maxSize"> Maximum size (px); &lt;= 0 on an axis = unlimited. </param>
        /// <param name="newSize"> Resulting size. </param>
        /// <param name="newPos"> Resulting anchoredPosition. </param>
        public static void ResizeKeepingOppositeEdge(Vector2 initialSize, Vector2 initialPos, Vector2 pivot,
            Vector2 deltaLocal, ResizeDirection dir, Vector2 minSize, Vector2 maxSize,
            out Vector2 newSize, out Vector2 newPos)
        {
            newSize = initialSize;
            newPos = initialPos;

            if ((dir & ResizeDirection.Left) != 0)
            {
                float w = ClampAxis(initialSize.x - deltaLocal.x, minSize.x, maxSize.x);
                newPos.x = initialPos.x - (1f - pivot.x) * (w - initialSize.x);
                newSize.x = w;
            }
            else if ((dir & ResizeDirection.Right) != 0)
            {
                float w = ClampAxis(initialSize.x + deltaLocal.x, minSize.x, maxSize.x);
                newPos.x = initialPos.x + pivot.x * (w - initialSize.x);
                newSize.x = w;
            }

            if ((dir & ResizeDirection.Bottom) != 0)
            {
                float h = ClampAxis(initialSize.y - deltaLocal.y, minSize.y, maxSize.y);
                newPos.y = initialPos.y - (1f - pivot.y) * (h - initialSize.y);
                newSize.y = h;
            }
            else if ((dir & ResizeDirection.Top) != 0)
            {
                float h = ClampAxis(initialSize.y + deltaLocal.y, minSize.y, maxSize.y);
                newPos.y = initialPos.y + pivot.y * (h - initialSize.y);
                newSize.y = h;
            }
        }

        private static float ClampAxis(float value, float min, float max)
        {
            if (value < min) value = min;
            if (max > 0f && value > max) value = max;
            return value;
        }
    }
}
