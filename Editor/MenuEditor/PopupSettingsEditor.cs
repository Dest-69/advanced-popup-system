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
        private static bool _autoSwitchInputModule;
        private static bool _escapeCloseEnabled;
        private static KeyCode _escapeCloseKey;
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
            // Auto Switch Input Module setting
            GUILayout.BeginHorizontal();
            GUILayout.Label("Auto Switch Input Module:", GUILayout.ExpandWidth(false));
            string switchToggleLabel = "";
            if (EditorGUIUtility.isProSkin)
                switchToggleLabel = _autoSwitchInputModule ? "[x]" : "[ ]";
#if HAS_NEWINPUT
            bool newAutoSwitch = GUILayout.Toggle(_autoSwitchInputModule, switchToggleLabel, APSEditorStyles.ToggleStyle);
            if (newAutoSwitch != _autoSwitchInputModule)
            {
                _autoSwitchInputModule = newAutoSwitch;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
#else
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Toggle(_autoSwitchInputModule, switchToggleLabel, APSEditorStyles.ToggleStyle);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Auto Switch Input Module requires Unity Input System package.", MessageType.Info);
            GUILayout.Space(5);
#endif
            
            // Escape close stack — the only keyboard behavior APS drives, so this toggle also decides whether the
            // key-polling player-loop system is installed at all (see KeyEventSystemAPS.Initialize).
            GUILayout.BeginHorizontal();
            GUILayout.Label("Escape Close Stack:", GUILayout.ExpandWidth(false));
            string escapeToggleLabel = "";
            if (EditorGUIUtility.isProSkin)
                escapeToggleLabel = _escapeCloseEnabled ? "[x]" : "[ ]";
            bool newEscapeCloseEnabled = GUILayout.Toggle(_escapeCloseEnabled, escapeToggleLabel, APSEditorStyles.ToggleStyle);
            if (newEscapeCloseEnabled != _escapeCloseEnabled)
            {
                _escapeCloseEnabled = newEscapeCloseEnabled;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (_escapeCloseEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Escape Close Key:", GUILayout.ExpandWidth(false));
                KeyCode newEscapeCloseKey = (KeyCode)EditorGUILayout.EnumPopup(_escapeCloseKey);
                if (newEscapeCloseKey != _escapeCloseKey)
                {
                    _escapeCloseKey = newEscapeCloseKey;
                    SaveSettings();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("The project-wide default — a popup can override it with its own Close Keys. Turning this off drops the keyboard path entirely; AdvancedPopupSystem.EscapeStep() can still be called manually (a UI \"Back\" button).", MessageType.Info);
                GUILayout.Space(5);
            }

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
            _settings.AutoSwitchInputModule = _autoSwitchInputModule;
            _settings.EscapeCloseEnabled = _escapeCloseEnabled;
            _settings.EscapeCloseKey = _escapeCloseKey;
            
            SettingsManager.SaveSettings();
        }

        private static void LoadSettings()
        {
            _settings = SettingsManager.LoadSettings();
            
            _inspectorViewIndex = (byte)_settings.InspectorView;
            _autoSwitchInputModule = _settings.AutoSwitchInputModule;
            _escapeCloseEnabled = _settings.EscapeCloseEnabled;
            _escapeCloseKey = _settings.EscapeCloseKey;
            _logTypeIndex = Mathf.Clamp(Array.IndexOf(LOGTypes, _settings.LogType), 0, LOGTypes.Length - 1);
        }
    }
}