using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    public class PopupDisplaysEditorPanel : EditorWindow
    {
        private static bool autoSave;
        private static string[] _displayNames;
        // Parallel to _displayNames: true for the package's built-in displays (Fade/Scale/Slide/DoTween), which are
        // read-only — only custom displays (in the consumer project) can be added, renamed or deleted.
        private static bool[] _isBuiltin;
        private static bool[] _displayNameChanged;
        
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

            Array.Resize(ref _displayNameChanged, _displayNames.Length);
            for (int i = 0; i < _displayNames.Length; i++)
            {
                GUILayout.BeginHorizontal();

                if (_isBuiltin != null && i < _isBuiltin.Length && _isBuiltin[i])
                {
                    // Built-in display: ships read-only inside the package — show it, but no rename / delete.
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(_displayNames[i]);
                    GUILayout.Label("built-in", EditorStyles.miniLabel, GUILayout.Width(56));
                    GUILayout.EndHorizontal();
                    continue;
                }

                string newEnumName = EditorGUILayout.DelayedTextField(_displayNames[i]);
                if (newEnumName != _displayNames[i])
                {
                    newEnumName = ValidateAndFormatDisplayName(newEnumName);
                    if (newEnumName != null)
                    {
                        _displayNames[i] = TypeHelper.RemoveDisplaySuffix(newEnumName) + "Display";
                        _displayNameChanged[i] = true;
                    }
                    else
                    {
                        APLogger.LogWarning("Invalid display name.");
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
                    var currentName = _displayNames[i];
                    if (string.IsNullOrEmpty(currentName) || EditorUtility.DisplayDialog("Confirm Delete",
                            $"Are you sure you want to delete the display '{_displayNames[i]}'? This will delete the display and its settings scripts.",
                            "Delete", "Cancel"))
                    {
                        DeleteDisplay(i);
                        if (autoSave) 
                            DeleteDisplayAndSettingsFiles(currentName);
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (_displayNames.All(s => !string.IsNullOrEmpty(s)))
            {
                if (GUILayout.Button("+", APSEditorStyles.BoldButtonStyle,GUILayout.Height(15)))
                { 
                    AddDisplay(string.Empty);
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
                    SaveDisplayChanges();
            }

            bool anyChanged = _displayNameChanged.Any(c => c);
            
            GUI.enabled = anyChanged && !autoSave;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                SaveDisplayChanges();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            
            if (_displayNameChanged != null && anyChanged && autoSave)
            {
                SaveDisplayChanges();
                Array.Clear(_displayNameChanged, 0, _displayNameChanged.Length);
            }
        }

        private static void LoadEnumNames()
        {
            var names = new List<string>();
            var builtin = new List<bool>();

            // Built-in displays ship inside the package (read-only); custom displays live in the consumer project so a
            // read-only UPM install can still author them and a package update never clobbers them.
            CollectDisplayFolders(FileSearcher.BuiltinDisplaysFolderPath, names, builtin, true);
            CollectDisplayFolders(FileSearcher.CustomDisplaysFolderPath, names, builtin, false);

            _displayNames = names.ToArray();
            _isBuiltin = builtin.ToArray();
            _displayNameChanged = new bool[_displayNames.Length];
        }

        private static void CollectDisplayFolders(string root, List<string> names, List<bool> builtin, bool isBuiltin)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string n = Path.GetFileName(dir);
                if (!n.EndsWith("Display") || names.Contains(n)) continue;
                names.Add(n);
                builtin.Add(isBuiltin);
            }
        }

        private static void AddDisplay(string enumName)
        {
            if (enumName != null && !_displayNames.Contains(enumName))
            {
                Array.Resize(ref _displayNames, _displayNames.Length + 1);
                _displayNames[^1] = enumName;
                Array.Resize(ref _isBuiltin, _displayNames.Length);
                _isBuiltin[^1] = false; // a freshly added row is always a custom display
                Array.Resize(ref _displayNameChanged, _displayNames.Length);
            }
        }

        private static void DeleteDisplay(int index)
        {
            _displayNames = _displayNames.Where((_, i) => i != index).ToArray();
            _isBuiltin = _isBuiltin.Where((_, i) => i != index).ToArray();
            _displayNameChanged = _displayNameChanged.Where((_, i) => i != index).ToArray();
        }

        private static string ValidateAndFormatDisplayName(string displayName)
        {
            if (displayName == null) return null;

            displayName = Regex.Replace(displayName, @"[\s-]+", ""); // Remove spaces and dashes
            displayName = Regex.Replace(displayName, "_+", ""); // Remove consecutive underscores

            if (displayName.Length == 0) return string.Empty;
            return !Regex.IsMatch(displayName, @"^[a-zA-Z]+$") ? null : displayName;
        }
        
        private static void SaveDisplayChanges()
        {
            // Only custom rows are generated — built-ins already ship in the package and must never be re-created in the
            // consumer folder (that would shadow them with an empty duplicate).
            var desired = new List<string>();
            for (int i = 0; i < _displayNames.Length; i++)
            {
                if (_isBuiltin != null && i < _isBuiltin.Length && _isBuiltin[i]) continue;
                if (string.IsNullOrEmpty(_displayNames[i])) continue;
                string b = TypeHelper.RemoveDisplaySuffix(_displayNames[i]);
                if (!desired.Contains(b)) desired.Add(b);
            }
            if (desired.Count == 0) return;

            string root = FileSearcher.CustomDisplaysFolderPath;
            if (string.IsNullOrEmpty(root))
            {
                APLogger.LogWarning("Cannot create a display: the generated displays folder is unavailable.");
                return;
            }

            foreach (var baseName in desired)
            {
                var className = baseName + "Display";
                var folder    = Path.Combine(root, className);
                if (!Directory.Exists(folder)) CreateDisplayAndSettingsFiles(baseName);
            }
            AssetDatabase.Refresh();

            LoadEnumNames();
        }

        private static void DeleteDisplayAndSettingsFiles(string displayName)
        {
            string root = FileSearcher.CustomDisplaysFolderPath;
            if (string.IsNullOrEmpty(root)) return;

            var baseName  = TypeHelper.RemoveDisplaySuffix(displayName);
            var folderFs  = Path.Combine(root, baseName + "Display");
            var assetPath = FileSearcher.ToAssetPath(folderFs);

            if (AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            SaveDisplayChanges();
        }

        private static void CreateDisplayAndSettingsFiles(string displayName)
        {
            var baseName = TypeHelper.RemoveDisplaySuffix(displayName);
            string root = FileSearcher.CustomDisplaysFolderPath;
            if (string.IsNullOrEmpty(root))
            {
                APLogger.LogWarning("Cannot create a display: the generated displays folder is unavailable.");
                return;
            }
            var displayFolderPath = Path.Combine(root, baseName + "Display");

            string fullDisplayName = $"{baseName}Display";;
            string fullSettingsName = $"{baseName}Settings";
            
            if (!Directory.Exists(displayFolderPath))
                Directory.CreateDirectory(displayFolderPath);

            string displayPath = Path.Combine(displayFolderPath, $"{fullDisplayName}.generated.cs");
            string settingsPath = Path.Combine(displayFolderPath, $"{fullSettingsName}.generated.cs");

            File.WriteAllText(displayPath, GenerateDisplayClass(fullDisplayName, fullSettingsName));
            File.WriteAllText(settingsPath, GenerateSettingsClass(fullDisplayName, fullSettingsName));
            
            AssetDatabase.ImportAsset(FileSearcher.ToAssetPath(displayPath));
            AssetDatabase.ImportAsset(FileSearcher.ToAssetPath(settingsPath));
        }

        private static string GenerateDisplayClass(string className, string settingsName)
        {
            return $@"
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{{
    public class {className} : DisplayBase<{settingsName}>
    {{
        /// <summary>
        /// Logic for instant popup show.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public override void ShowInstantlyMethod(RectTransform transform, {settingsName} settings)
        {{
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            /* Your code here */

            transform.localScale = Vector3.one;
            SetCanvasGroupState(canvasGroup, true);
            
            settings.OnAnimationEnd?.Invoke();
        }}

        /// <summary>
        /// Logic for instant popup hide.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public override void HideInstantlyMethod(RectTransform transform, {settingsName} settings)
        {{
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            /* Your code here */

            transform.localScale = Vector3.zero;
            SetCanvasGroupState(canvasGroup, false);
            
            settings.OnAnimationEnd?.Invoke();
        }}

        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name=""cancellationToken""></param>
        /// <returns></returns>
        public override Task ShowMethod(RectTransform transform, {settingsName} settings, CancellationToken cancellationToken)
        {{
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            settings.OnAnimationStart?.Invoke();

            /* Your code here */
            
            settings.OnAnimationEnd?.Invoke();

            return Task.CompletedTask;
        }}
        
        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name=""cancellationToken""></param>
        /// <returns></returns> 
        public override Task HideMethod(RectTransform transform, {settingsName} settings, CancellationToken cancellationToken)
        {{
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            settings.OnAnimationStart?.Invoke();

            /* Your code here */
            
            settings.OnAnimationEnd?.Invoke();

            return Task.CompletedTask;
        }}

        /// <summary>
        /// Checks if operation already cancelled.
        /// </summary>
        /// <param name=""cancellationToken""></param>
        /// <returns></returns>
        private bool OperationCancelled(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested || !Application.isPlaying;

        /// <summary>
        /// Get the CanvasGroup component from the transform.
        /// </summary>
        /// <param name=""transform"">The transform of the popup.</param>
        /// <returns>The CanvasGroup component if it exists, null otherwise.</returns>
        private static CanvasGroup GetCanvasGroup(Component transform)
        {{
            CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {{
                APLogger.LogWarning($""CanvasGroup component missing on {{transform.name}}"");
            }}
            return canvasGroup;
        }}

        /// <summary>
        /// Set the state of the CanvasGroup.
        /// </summary>
        /// <param name=""canvasGroup"">The CanvasGroup component of the popup.</param>
        /// <param name=""state"">The desired state (true for visible, false for hidden).</param>
        private static void SetCanvasGroupState(CanvasGroup canvasGroup, bool state)
        {{
            canvasGroup.alpha = state ? 1 : 0;
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }}
    }}
}}
";
        }

        private static string GenerateSettingsClass(string displayName, string settingsName)
        {
            return $@"
using System;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{{
    [Serializable]
    public class {settingsName} : BaseSettings<{displayName}>
    {{
        
    }}
}}
";
        }
    }
}