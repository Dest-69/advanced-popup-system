using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Per-popup resize settings. Shown in the inspector only when <see cref="PopupFeatureEnum.Resizable"/> is enabled.
    /// </summary>
    [Serializable]
    public class ResizeConfig
    {
        [Tooltip("Resize handles. Use 'Generate Grips' in the inspector to create a default 8-grip set, then tweak.")]
        public List<ResizeGrip> Grips = new List<ResizeGrip>();

        [Tooltip("Minimum popup size (px).")]
        public Vector2 MinSize = new Vector2(100, 100);

        [Tooltip("Maximum popup size (px). Zero or negative on an axis = unlimited.")]
        public Vector2 MaxSize = Vector2.zero;

        [Tooltip("Rectangle the popup is kept inside while resizing.")]
        public BoundsMode Bounds = BoundsMode.Canvas;

        [Tooltip("Custom bounds rect — used only when Bounds is set to Custom.")]
        public RectTransform CustomBounds;

        [Tooltip("Extra inset (px) applied to the bounds on each side.")]
        public RectOffset Padding = new RectOffset();

        [Tooltip("Change the OS cursor to a directional arrow while the pointer hovers a grip.")]
        public bool ChangeCursor = true;

        [Tooltip("Cursor icons used for the hover feedback. Leave empty to use the package default set.")]
        public ResizeCursorSet Cursors;
    }
}
