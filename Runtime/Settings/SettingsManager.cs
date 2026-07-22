using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public static class SettingsManager
    {
        private const string FileName = "AP_Settings.json";
        private const string ResourceName = "AP_Settings";

        public static PopupSettings Settings { get; private set; }

        static SettingsManager()
        {
            LoadSettings();
        }

        private static string SettingsFsPath =>
            Path.Combine(Application.dataPath, "Resources", FileName).Replace('\\', '/');

        public static void SaveSettings()
        {
            try
            {
                string directoryPath = Path.Combine(Application.dataPath, "Resources");
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                // Crash-safe: serialize to a temp file, then atomically swap it in — a killed editor mid-write can no
                // longer truncate AP_Settings.json (it stays intact until the swap).
                WriteAtomic(SettingsFsPath, JsonConvert.SerializeObject(Settings));
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
            // Primary: the imported Resources TextAsset — the only path that works in a player build.
            try
            {
                TextAsset jsonText = Resources.Load<TextAsset>(ResourceName);
                if (jsonText != null && !string.IsNullOrWhiteSpace(jsonText.text))
                    return Settings = JsonConvert.DeserializeObject<PopupSettings>(jsonText.text);
            }
            catch (Exception ex)
            {
                // Never rethrow: this runs from the static constructor, so a throw here would poison the whole type
                // (TypeInitializationException) for the domain — the same trap the old code fell into.
                Debug.LogError($"Failed to load settings: {ex.Message}");
            }

#if UNITY_EDITOR
            // Editor recovery: the imported asset was missing or unreadable — read the on-disk file (atomic writes keep
            // it complete) before falling back to defaults, so a transient hiccup doesn't silently reset settings.
            PopupSettings recovered = TryReadFile(SettingsFsPath);
            if (recovered != null)
                return Settings = recovered;
#endif

            Settings = new PopupSettings
            {
                InspectorView = InspectorEnum.APSInspector,
                KeyEventSystemEnabled = true,
                LogType = "Warning",
                EscapeCloseEnabled = false,
                EscapeCloseKey = KeyCode.Escape
            };

            SaveSettings();

            return Settings;
        }

#if UNITY_EDITOR
        private static PopupSettings TryReadFile(string fsPath)
        {
            try
            {
                if (!File.Exists(fsPath)) return null;
                string text = File.ReadAllText(fsPath);
                return string.IsNullOrWhiteSpace(text) ? null : JsonConvert.DeserializeObject<PopupSettings>(text);
            }
            catch { return null; }
        }
#endif

        private static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null); // atomic where supported
                else File.Move(tmp, path);
            }
            catch
            {
                // Some filesystems / AV shields reject File.Replace — fall back to a delete + move.
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
        }
    }
}
