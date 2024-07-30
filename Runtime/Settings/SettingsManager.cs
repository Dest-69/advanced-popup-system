using System.IO;
using AdvancedPS.Core.Utils;
using Newtonsoft.Json;

namespace AdvancedPS.Core.System
{
    public class SettingsManager
    {
        public static PopupSettings Settings { get; private set; }
        private static readonly string SettingsFilePath;

        static SettingsManager()
        {
            SettingsFilePath = FileSearcher.SettingsFilePath;
            LoadSettings();
        }

        public static void SaveSettings()
        {
            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(Settings));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public static PopupSettings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                Settings = JsonConvert.DeserializeObject<PopupSettings>(File.ReadAllText(SettingsFilePath));
            }
            else
            {
                Settings = new PopupSettings
                {
                    CustomIconsEnabled = true,
                    KeyEventSystemEnabled = true,
                    LogType = "Warning"
                };
                SaveSettings();
            }

            return Settings;
        }
    }
}