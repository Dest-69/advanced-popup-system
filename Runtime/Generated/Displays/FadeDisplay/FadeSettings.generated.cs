using System;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    [Serializable]
    public class FadeSettings : BaseSettings<FadeDisplay>
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
        /// Target alpha of CanvasGroup on shown.
        /// </summary>
        public float MaxValue = 1f;
        /// <summary>
        /// Target alpha of CanvasGroup on hidden.
        /// </summary>
        public float MinValue = 0f;
    }
}