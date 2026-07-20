using System;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// One resize handle: a RectTransform the pointer can grab and the edge/corner it drives. Hit-testing is done on
    /// the RectTransform itself (no Graphic/raycast target required). Populate manually or with the inspector's
    /// "Generate Grips" button.
    /// </summary>
    [Serializable]
    public struct ResizeGrip
    {
        [Tooltip("The handle RectTransform the pointer grabs.")]
        public RectTransform Rect;

        [Tooltip("Which edge/corner this grip resizes.")]
        public ResizeDirection Direction;
    }
}
