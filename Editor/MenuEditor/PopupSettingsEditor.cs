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

            DrawMaintenance();
        }

        /// <summary>
        /// Actions that rebuild APS data on demand. Everything an optional integration contributes comes through the
        /// <see cref="APSEditorTools"/> seam, so a project without that integration shows nothing here instead of a
        /// button that can't work.
        /// </summary>
        private static void DrawMaintenance()
        {
            if (APSEditorTools.RegenerateAddressableIndex == null) return;

            GUILayout.Space(10);
            EditorGUILayoutExtensions.DrawHorizontalLine();
            GUILayout.Label("Maintenance", EditorStyles.miniLabel);

            if (GUILayout.Button(new GUIContent("Regenerate Addressable Index",
                    "Rescans every prefab in the project and rebuilds the Addressable popup group + index. " +
                    "Prefab saves keep it in sync on their own — use this after a bulk import or if the index looks stale.")))
                APSEditorTools.RegenerateAddressableIndex.Invoke();
        }

        private static void SaveSettings()
        {
            _settings.InspectorView = (InspectorEnum)_inspectorViewIndex;
            _settings.LogType = LOGTypes[_logTypeIndex];
            _settings.AutoSwitchInputModule = _autoSwitchInputModule;

            SettingsManager.SaveSettings();
        }

        private static void LoadSettings()
        {
            _settings = SettingsManager.LoadSettings();
            
            _inspectorViewIndex = (byte)_settings.InspectorView;
            _autoSwitchInputModule = _settings.AutoSwitchInputModule;
            _logTypeIndex = Mathf.Clamp(Array.IndexOf(LOGTypes, _settings.LogType), 0, LOGTypes.Length - 1);
        }
    }
}