using System;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Per-popup drag settings. Shown in the inspector only when <see cref="PopupFeatureEnum.Draggable"/> is enabled.
    /// </summary>
    [Serializable]
    public class DragConfig
    {
        [Tooltip("Area that starts a drag (e.g. a title bar). If null, the whole popup rect is draggable.")]
        public RectTransform DragZone;

        [Tooltip("Rectangle the popup is kept inside while dragging.")]
        public BoundsMode Bounds = BoundsMode.Canvas;

        [Tooltip("Custom bounds rect — used only when Bounds is set to Custom.")]
        public RectTransform CustomBounds;

        [Tooltip("Extra inset (px) applied to the bounds on each side.")]
        public RectOffset Padding = new RectOffset();
    }
}
