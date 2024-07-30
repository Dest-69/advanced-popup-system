using System;
using AdvancedPS.Core.System;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    public static class PopupSettingsEditor
    {
        private static PopupSettings _settings;
        
        private static bool _customIconsEnabled;
        private static bool _keyEventSystemEnabled;
        private static int _logTypeIndex;
        private static readonly string[] LOGTypes = { "Error", "Warning", "Info" };
        
        public static void Initialize()
        {
            LoadSettings();
        }

        public static void OnGUIInternall()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Custom icons:", GUILayout.ExpandWidth(false));
            string toggleLable = "";
            if (EditorGUIUtility.isProSkin)
                toggleLable = _customIconsEnabled ? "[x]" : "[ ]";
            bool newCustomIconsEnabled = GUILayout.Toggle(_customIconsEnabled, toggleLable, APSEditorStyles.ToggleStyle);
            if (newCustomIconsEnabled != _customIconsEnabled)
            {
                _customIconsEnabled = newCustomIconsEnabled;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key Event Tracking:", GUILayout.ExpandWidth(false));
            if (EditorGUIUtility.isProSkin)
                toggleLable = _keyEventSystemEnabled ? "[x]" : "[ ]";
            bool newKeyEventSystemEnabled = GUILayout.Toggle(_keyEventSystemEnabled, toggleLable, APSEditorStyles.ToggleStyle);
            if (newKeyEventSystemEnabled != _keyEventSystemEnabled)
            {
                _keyEventSystemEnabled = newKeyEventSystemEnabled;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Log Type:", GUILayout.ExpandWidth(false));
            int newLogTypeIndex = EditorGUILayout.Popup(_logTypeIndex, LOGTypes);
            if (newLogTypeIndex != _logTypeIndex)
            {
                _logTypeIndex = newLogTypeIndex;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
        }

        private static void SaveSettings()
        {
            _settings.CustomIconsEnabled = _customIconsEnabled;
            _settings.LogType = LOGTypes[_logTypeIndex];
            _settings.KeyEventSystemEnabled = _keyEventSystemEnabled;
            
            SettingsManager.SaveSettings();
        }

        private static void LoadSettings()
        {
            _settings = SettingsManager.LoadSettings();
            
            _customIconsEnabled = _settings.CustomIconsEnabled;
            _keyEventSystemEnabled = _settings.KeyEventSystemEnabled;
            _logTypeIndex = Array.IndexOf(LOGTypes, _settings.LogType);
        }
    }
}