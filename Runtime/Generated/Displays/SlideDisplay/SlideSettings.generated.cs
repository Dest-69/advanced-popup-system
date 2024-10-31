using System;
using AdvancedPS.Core.System;
using UnityEngine;

namespace AdvancedPS.Core
{
    [Serializable]
    public class SlideSettings : BaseSettings
    {
        /// <summary>
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// The PosX, PosY, PosZ of RectTransform to which the popup will aim.
        /// </summary>
        public Vector3 TargetRectPosition { get; set; }
        /// <summary>
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// The Width and Height of RectTransform to which the popup will aim.
        /// </summary>
        public Vector2 TargetRectSize { get; set; }

        /// <summary>
        /// Setting default values.
        /// </summary>
        public SlideSettings()
        {
            Duration = 0.75f;
            TargetRectPosition = Vector3.zero;
            TargetRectSize = Vector2.zero;
        }
    }
}