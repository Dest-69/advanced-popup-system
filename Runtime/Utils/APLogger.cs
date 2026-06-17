using System;
using AdvancedPS.Core.System;
using UnityEngine;

namespace AdvancedPS.Core.Utils
{
    public static class APLogger
    {
        public static void Log(string message)
        {
            if (SettingsManager.Settings.LogType is not "Info") return;
            Debug.Log(message);
        }

        public static void LogWarning(string message)
        {
            if (SettingsManager.Settings.LogType is not ("Info" or "Warning")) return;
            Debug.LogWarning(message);
        }

        public static void LogError(string message)
        {
            if (SettingsManager.Settings.LogType is "None") return;
            Debug.LogError(message);
        }
        
        public static void LogException(Exception e)
        {
            Debug.LogException(e);
        }
    }
}