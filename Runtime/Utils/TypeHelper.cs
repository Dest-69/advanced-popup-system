using System;
using System.Linq;

namespace AdvancedPS.Core.Utils
{
    public static class TypeHelper
    {
        public static Type GetDisplaySettings(string displayName)
        {
            // Scan loaded assemblies by simple name — Type.GetType(shortName) only resolves the calling assembly/mscorlib,
            // so it always returned null for APS display/settings types living in their own assemblies.
            return GetTypeByName(RemoveDisplaySuffix(displayName) + "Settings");
        }

        public static Type GetDisplay(string displayName)
        {
            return GetTypeByName(RemoveDisplaySuffix(displayName) + "Display");
        }
        
        public static Type GetTypeByFullName(string typeFullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.FullName == typeFullName);
        }
        public static Type GetTypeByName(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == typeName);
        }
        
        /// <summary>
        /// Strips a single trailing Display/Settings suffix (case-insensitive): "FadeDisplay" → "Fade",
        /// "ScaleSettings" → "Scale". Only the trailing suffix is removed, so names that merely contain the words
        /// (e.g. "DisplayBoardWidget") are left intact — unlike a blanket replace-anywhere.
        /// </summary>
        public static string RemoveDisplaySuffix(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Longest variants first so the plural/longer form wins over its own prefix.
            string[] suffixes = { "Displays", "Settings", "Display", "Setting" };
            foreach (string suffix in suffixes)
            {
                if (input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return input.Substring(0, input.Length - suffix.Length);
            }
            return input;
        }
    }
}