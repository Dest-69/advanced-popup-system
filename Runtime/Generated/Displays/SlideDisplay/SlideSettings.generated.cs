using System;
using AdvancedPS.Core.System;
using UnityEngine;

namespace AdvancedPS.Core
{
    [Serializable]
    public class SlideSettings : BaseSettings<SlideDisplay>
    {
        /// <summary>
        /// Duration animation in seconds.
        /// </summary>
        public float Duration = 0.5f;
        /// <summary>
        /// Type of animation curve.
        /// </summary>
        public EasingType Easing = EasingType.EaseInOutQuad;
        /// <summary>
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// The PosX, PosY, PosZ of RectTransform to which the popup will aim.
        /// </summary>
        public Vector3 TargetRectPosition = Vector3.zero;
        /// <summary>
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// The Width and Height of RectTransform to which the popup will aim.
        /// </summary>
        public Vector2 TargetRectSize = Vector2.zero;
    }
}