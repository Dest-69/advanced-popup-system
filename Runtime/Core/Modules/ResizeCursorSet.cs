using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// The four directional cursors shown while hovering a resize grip, plus their shared hotspot. A single asset is the
    /// central place to re-skin resize cursors: assign one to <see cref="ResizeConfig.Cursors"/>, or leave that empty to
    /// use the package <see cref="Default"/> (a shipped set pointing at the built-in arrow icons). Create your own via
    /// <b>Assets ▸ Create ▸ Advanced Popup System ▸ Resize Cursor Set</b>.
    /// </summary>
    [CreateAssetMenu(fileName = "ResizeCursorSet", menuName = "Advanced Popup System/Resize Cursor Set")]
    public class ResizeCursorSet : ScriptableObject
    {
        /// <summary> Resources name of the shipped default set (loaded lazily by <see cref="Default"/>). </summary>
        public const string DefaultResourceName = "APS_ResizeCursorSet";

        [Tooltip("↔ cursor for the left and right edges.")]
        public Texture2D Horizontal;

        [Tooltip("↕ cursor for the top and bottom edges.")]
        public Texture2D Vertical;

        [Tooltip("╲ cursor for the top-left and bottom-right corners (NW–SE).")]
        public Texture2D DiagonalNWSE;

        [Tooltip("╱ cursor for the top-right and bottom-left corners (NE–SW).")]
        public Texture2D DiagonalNESW;

        [Tooltip("Click point of every cursor, in pixels from the texture's top-left — usually the center (e.g. 12,12 for a 24px cursor).")]
        public Vector2 Hotspot = new Vector2(12, 12);

        /// <summary>
        /// The cursor texture for a resize direction: horizontal for the L/R edges, vertical for the T/B edges, and the
        /// matching diagonal for a corner (both axes set). Returns null when nothing is assigned for that direction.
        /// </summary>
        public Texture2D Resolve(ResizeDirection dir)
        {
            bool left = (dir & ResizeDirection.Left) != 0;
            bool right = (dir & ResizeDirection.Right) != 0;
            bool top = (dir & ResizeDirection.Top) != 0;
            bool bottom = (dir & ResizeDirection.Bottom) != 0;
            bool horizontal = left || right;
            bool vertical = top || bottom;

            if (horizontal && vertical)
                return (top && left) || (bottom && right) ? DiagonalNWSE : DiagonalNESW;
            if (horizontal)
                return Horizontal;
            if (vertical)
                return Vertical;
            return null;
        }

        #region Default
        private static ResizeCursorSet _default;
        private static bool _defaultLoaded;

        /// <summary>
        /// The shipped default set, loaded once from Resources ("<see cref="DefaultResourceName"/>") and cached. Used
        /// when a popup's <see cref="ResizeConfig.Cursors"/> is left empty. Null if the asset was removed.
        /// </summary>
        public static ResizeCursorSet Default
        {
            get
            {
                if (!_defaultLoaded)
                {
                    _default = Resources.Load<ResizeCursorSet>(DefaultResourceName);
                    _defaultLoaded = true;
                }
                return _default;
            }
        }

        /// <summary> Drops the cached default so it is reloaded on next use. Called on play-mode exit (leak guard). </summary>
        internal static void ClearDefaultCache()
        {
            _default = null;
            _defaultLoaded = false;
        }
        #endregion
    }
}
