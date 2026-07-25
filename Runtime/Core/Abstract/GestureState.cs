using UnityEngine;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// Transient state of the single in-flight pointer gesture, owned by <see cref="AdvancedPS.Core.PopupInteractionSystem"/>
    /// and passed to handlers by ref. Keeps handlers stateless (like displays) — all per-gesture data lives here, so
    /// the shared handler instances hold nothing.
    /// </summary>
    public struct GestureState
    {
        /// <summary> The popup being manipulated. </summary>
        public IAdvancedPopup Popup;
        /// <summary> Popup root RectTransform (the thing that moves/resizes). </summary>
        public RectTransform Rect;
        /// <summary> Parent RectTransform — the space anchoredPosition and pointer conversions are done in. </summary>
        public RectTransform Parent;
        /// <summary> Canvas the popup lives under (bounds are resolved in its local space). </summary>
        public Canvas Canvas;
        /// <summary> UI camera (null for Screen Space - Overlay). </summary>
        public Camera Camera;
        /// <summary> Pointer position at grab, in <see cref="Parent"/> local space. </summary>
        public Vector2 PointerStartLocal;
        /// <summary> anchoredPosition captured at grab. </summary>
        public Vector2 InitialAnchoredPos;
        /// <summary> Rect size captured at grab (resize only). </summary>
        public Vector2 InitialSize;
        /// <summary>
        /// Popup AABB in <see cref="Canvas"/> local space at grab (resize only). Holds the edge opposite the grip —
        /// the one a resize keeps fixed — so the room left up to the bounds can be measured without re-deriving it.
        /// </summary>
        public Rect InitialCanvasAABB;
        /// <summary> Grabbed edge/corner (resize only). </summary>
        public ResizeDirection GripDir;
    }
}
