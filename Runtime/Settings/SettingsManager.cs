using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public static class SettingsManager
    {
        public static PopupSettings Settings { get; private set; }

        static SettingsManager()
        {
            LoadSettings();
        }

        public static void SaveSettings()
        {
            string directoryPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            
            try
            {
                string path = Path.Combine(Application.dataPath, "Resources", "AP_Settings.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(Settings));
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save settings: {ex.Message}");
            }
        }

        public static PopupSettings LoadSettings()
        {
            try
            {
                TextAsset jsonText = Resources.Load<TextAsset>("AP_Settings");
                if (jsonText != null) 
                    return Settings = JsonConvert.DeserializeObject<PopupSettings>(jsonText.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load settings: {ex.Message}");
                throw;
            }
            
            Settings = new PopupSettings
            {
                InspectorView = InspectorEnum.APSInspector,
                KeyEventSystemEnabled = true,
                LogType = "Warning"
            };
            
            SaveSettings();
            
            return Settings;
        }
    }
}