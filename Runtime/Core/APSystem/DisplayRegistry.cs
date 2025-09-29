using System;
using System.Collections.Concurrent;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    public static class DisplayRegistry
    {
        /// <summary>
        /// Instances of IDisplay which was used. (using for reduce allocations)
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Lazy<IDisplay>> Map = new();

        /// <summary>
        /// Get display cached Display or lazy create.
        /// </summary>
        public static TDisplay Get<TDisplay>() where TDisplay : IDisplay, new()
            => (TDisplay)Map.GetOrAdd(typeof(TDisplay), _ => new Lazy<IDisplay>(() => new TDisplay())).Value;

        /// <summary>
        /// Get display cached Display or lazy create.
        /// </summary>
        public static IDisplay Get(Type t)
            => Map.GetOrAdd(t, _ => new Lazy<IDisplay>(() => (IDisplay)Activator.CreateInstance(t))).Value;
    }
}