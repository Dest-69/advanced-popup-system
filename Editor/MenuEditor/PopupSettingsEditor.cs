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
        
        private static byte _inspectorViewIndex;
        private static bool _keyEventSystemEnabled;
        private static string[] _inspectorViewTypes;
        
        private static int _logTypeIndex;
        private static readonly string[] LOGTypes = { "Error", "Warning", "Info", "None" };
        
        public static void Initialize()
        {
            LoadSettings();
            _inspectorViewTypes = Enum.GetNames(typeof(InspectorEnum));
        }

        public static void OnGUIInternal()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key Event Tracking:", GUILayout.ExpandWidth(false));
            string toggleLable = "";
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
            GUILayout.Label("Inspector view:", GUILayout.ExpandWidth(false));
            int newinspectorViewIndex = EditorGUILayout.Popup(_inspectorViewIndex, _inspectorViewTypes);
            if (newinspectorViewIndex != _inspectorViewIndex)
            {
                _inspectorViewIndex = (byte)newinspectorViewIndex;
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
            _settings.InspectorView = (InspectorEnum)_inspectorViewIndex;
            _settings.LogType = LOGTypes[_logTypeIndex];
            _settings.KeyEventSystemEnabled = _keyEventSystemEnabled;
            
            SettingsManager.SaveSettings();
        }

        private static void LoadSettings()
        {
            _settings = SettingsManager.LoadSettings();
            
            _inspectorViewIndex = (byte)_settings.InspectorView;
            _keyEventSystemEnabled = _settings.KeyEventSystemEnabled;
            _logTypeIndex = Mathf.Clamp(Array.IndexOf(LOGTypes, _settings.LogType), 0, LOGTypes.Length - 1);
        }
    }
}