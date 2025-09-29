using System;
using AdvancedPS.Core.System;
using UnityEngine;

namespace AdvancedPS.Core
{
    [Serializable]
    public class ScaleSettings : BaseSettings<ScaleDisplay>
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
        /// Target scale of GameObject on shown.
        /// </summary>
        public Vector3 ShowScale = Vector3.one;
        /// <summary>
        /// Target scale of GameObject on hidden.
        /// </summary>
        public Vector3 HideScale = Vector3.zero;
    }
}