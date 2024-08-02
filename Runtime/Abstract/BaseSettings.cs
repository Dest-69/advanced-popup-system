using System;
using UnityEngine.Events;

namespace AdvancedPS.Core.System
{
    [Serializable]
    public class BaseSettings
    {
        /// <summary>
        /// Duration animation in seconds.
        /// </summary>
        public float Duration;
        /// <summary>
        /// Type of animation curve.
        /// </summary>
        public EasingType Easing;
        /// <summary>
        /// Event should Invoke when animation will start.
        /// </summary>
        public Action OnAnimationStart;
        /// <summary>
        /// Event should Invoke when animation will end completely.
        /// </summary>
        public Action OnAnimationEnd;
        
        /// <summary>
        /// Setting default values.
        /// </summary>
        public BaseSettings()
        {
            Duration = 0.5f;
            Easing = EasingType.EaseInOutQuad;
        }
    }
}
