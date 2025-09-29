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
        /// Event should Invoke when animation will start.
        /// </summary>
        public Action OnAnimationStart;
        /// <summary>
        /// Event should Invoke when animation will end completely.
        /// </summary>
        public Action OnAnimationEnd;
    }
}
