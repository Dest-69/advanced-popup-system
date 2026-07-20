using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdvancedPS.Core;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    public class PopupLayerEditorPanel
    {
        private static string[] _enumNames;
        private static bool[] _enumNameChanged;
        private static bool autoSave;
        private static Vector2 scrollPosition;

        private const string AutoSaveKey = "APS_AutoSaveEnabled";

        public static void Initialize()
        {
            LoadEnumNames();
            autoSave = PlayerPrefs.GetInt(AutoSaveKey, 1) == 1;
        }

        public static void OnGUIInternal()
        {
            GUILayout.BeginVertical();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, APSEditorStyles.ScrollViewStyle);

            Array.Resize(ref _enumNameChanged, _enumNames.Length);
            for (int i = 0; i < _enumNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                
                string newEnumName = EditorGUILayout.DelayedTextField(_enumNames[i]);
                if (newEnumName != _enumNames[i])
                {
                    newEnumName = ValidateAndFormatEnumName(newEnumName);
                    if (newEnumName != null)
                    {
                        _enumNames[i] = newEnumName;
                        _enumNameChanged[i] = true;
                    }
                    else
                    {
                        APLogger.LogWarning("Invalid enum name.");
                    }
                }

                if (string.IsNullOrEmpty(newEnumName))
                {
                    if (GUILayout.Button("Done", GUILayout.Width(60)))
                    {
                        GUI.FocusControl(null);
                    }
                }
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    DeleteEnum(i);
                    if (autoSave) SaveEnumChanges();
                    GUILayout.EndHorizontal();
                    break;
                }
                
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (_enumNames.All(s => !string.IsNullOrEmpty(s)))
            {
                if (GUILayout.Button("+", APSEditorStyles.BoldButtonStyle,GUILayout.Height(15)))
                { 
                    AddEnum(string.Empty);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.Label("Auto-Save", GUILayout.ExpandWidth(false));
            string toggleLabel = EditorGUIUtility.isProSkin ? (autoSave ? "[x]" : "[ ]") : "";
            bool newAutoSave = GUILayout.Toggle(autoSave, toggleLabel, APSEditorStyles.ToggleStyle);
            if (newAutoSave != autoSave)
            {
                autoSave = newAutoSave;
                PlayerPrefs.SetInt(AutoSaveKey, autoSave ? 1 : 0);
                PlayerPrefs.Save();

                if (autoSave)
                    SaveEnumChanges();
            }

            bool anyChanged = _enumNameChanged.Any(c => c);
            
            GUI.enabled = anyChanged && !autoSave;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                SaveEnumChanges();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_enumNameChanged != null && anyChanged && autoSave)
            {
                SaveEnumChanges();
                Array.Clear(_enumNameChanged, 0, _enumNameChanged.Length);
            }
        }

        private static void LoadEnumNames()
        {
            // Source of truth is the external store (outside the package); fall back to the compiled enum on first run.
            string[] layers = LayerCatalog.LoadNames() ?? LayerCatalog.CurrentEnumNames();
            _enumNames = new[] { "None" }.Concat(layers).ToArray();
            _enumNameChanged = new bool[_enumNames.Length];
        }

        private static void AddEnum(string enumName)
        {
            if (enumName != null && !_enumNames.Contains(enumName))
            {
                Array.Resize(ref _enumNames, _enumNames.Length + 1);
                _enumNames[_enumNames.Length - 1] = enumName;
                Array.Resize(ref _enumNameChanged, _enumNames.Length);
            }
        }

        private static void DeleteEnum(int index)
        {
            _enumNames = _enumNames.Where((_, i) => i != index).ToArray();
            _enumNameChanged = _enumNameChanged.Where((_, i) => i != index).ToArray();
        }

        private static string ValidateAndFormatEnumName(string enumName)
        {
            if (enumName == null) return null;
            enumName = Regex.Replace(enumName, @"[\s-]+", "_"); // Convert spaces and dashes to underscores
            enumName = Regex.Replace(enumName, "_+", "_"); // Remove consecutive underscores
            enumName = enumName.ToUpper(); // Convert to uppercase
            if (enumName.Length == 0) return string.Empty;
            return !Regex.IsMatch(enumName, @"^[A-Z_]+$") ? null : enumName;
        }

        private static void SaveEnumChanges()
        {
            // Persist to the external store (source of truth, outside the package), then regenerate the enum file
            // from it via the single codegen path. The store is what survives a package update (see LayerCatalog).
            LayerCatalog.SaveNames(_enumNames.Skip(1));
            LayerCatalog.Reconcile();

            LoadEnumNames();
        }
    }
}