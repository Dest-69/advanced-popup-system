using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AdvancedPS.Core.System
{
    public static class DisplaySettingsFactory
    {
        // Cache per display type to avoid repeated reflection
        private static readonly Dictionary<Type, Func<object>> _creators = new();

        public static IDisplaySettings<T> GetDefaultSettings<T>() where T : IDisplay
        {
            if (!_creators.TryGetValue(typeof(T), out var creator))
            {
                // 1) Find IDisplay<TSettings> implemented by T
                var idisplayGeneric = typeof(T).GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDisplay<>));
                if (idisplayGeneric == null)
                    throw new InvalidOperationException($"Type {typeof(T).Name} must implement IDisplay<TSettings>.");

                var settingsType = idisplayGeneric.GetGenericArguments()[0];

                // 2) Prefer public static TSettings Default()
                var defaultMethod = settingsType.GetMethod(
                    "Default",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                if (defaultMethod != null && settingsType.IsAssignableFrom(defaultMethod.ReturnType))
                {
                    creator = () => defaultMethod.Invoke(null, null);
                }
                else
                {
                    // 3) Fallback to parameterless ctor
                    var ctor = settingsType.GetConstructor(Type.EmptyTypes)
                               ?? throw new MissingMethodException(
                                   $"Settings type {settingsType.Name} must have parameterless ctor or static Default().");
                    creator = () => ctor.Invoke(null);
                }

                _creators[typeof(T)] = creator;
            }

            return (IDisplaySettings<T>)creator();
        }
    }
}
