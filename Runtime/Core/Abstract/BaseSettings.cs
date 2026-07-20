using System;

namespace AdvancedPS.Core.System
{
    public interface IDisplaySettings { Type DisplayType { get; } }
    public interface IDisplaySettings<TDisplay> : IDisplaySettings { }
    
    [Serializable]
    public abstract class BaseSettings<TDisplay> : IDisplaySettings<TDisplay>
    {
        public Type DisplayType => typeof(TDisplay);

        /// <summary>
        /// Drive the animation with <see cref="UnityEngine.Time.unscaledDeltaTime"/> instead of
        /// <see cref="UnityEngine.Time.deltaTime"/>. Set <c>true</c> for popups that must animate while the game is
        /// paused (<c>Time.timeScale == 0</c>) — e.g. a pause menu — otherwise the transition never progresses.
        /// </summary>
        public bool UnscaledTime;

        /// <summary>
        /// Event should Invoke when animation will start.
        /// </summary>
        public Action OnAnimationStart;
        /// <summary>
        /// Event should Invoke when animation will end completely.
        /// </summary>
        public Action OnAnimationEnd;
    }
}
