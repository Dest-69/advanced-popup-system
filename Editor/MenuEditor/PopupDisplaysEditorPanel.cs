using System;
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
            var root = FileSearcher.DisplaysFolderPath;
            if (!Directory.Exists(root)) {
                _displayNames = Array.Empty<string>();
                _displayNameChanged = Array.Empty<bool>();
                return;
            }
            _displayNames = Directory.GetDirectories(root)
                .Select(Path.GetFileName)
                .Where(n => n.EndsWith("Display"))
                .ToArray();
            _displayNameChanged = new bool[_displayNames.Length];
        }

        private static void AddDisplay(string enumName)
        {
            if (enumName != null && !_displayNames.Contains(enumName))
            {
                Array.Resize(ref _displayNames, _displayNames.Length + 1);
                _displayNames[^1] = enumName;
                Array.Resize(ref _displayNameChanged, _displayNames.Length);
            }
        }

        private static void DeleteDisplay(int index)
        {
            _displayNames = _displayNames.Where((_, i) => i != index).ToArray();
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
            var desired = _displayNames
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(TypeHelper.RemoveDisplaySuffix)
                .Distinct()
                .ToList();
            if (desired.Count == 0) return;

            foreach (var baseName in desired) 
            {
                var className = baseName + "Display";
                var folder    = Path.Combine(FileSearcher.DisplaysFolderPath, className);
                if (!Directory.Exists(folder)) CreateDisplayAndSettingsFiles(baseName);
            }
            AssetDatabase.Refresh();
            
            APSCodeGenerator.Execute(desired.ToArray());
            LoadEnumNames();
        }
        
        private static void DeleteDisplayAndSettingsFiles(string displayName)
        {
            var baseName  = TypeHelper.RemoveDisplaySuffix(displayName);
            var folderFs  = Path.Combine(FileSearcher.DisplaysFolderPath, baseName + "Display");
            var assetPath = FileSearcher.ToAssetPath(folderFs);

            if (AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.DeleteAsset(assetPath);
            
            SaveDisplayChanges();
        }
        
        private static void CreateDisplayAndSettingsFiles(string displayName)
        {
            var baseName = TypeHelper.RemoveDisplaySuffix(displayName);
            var displayFolderPath = Path.Combine(FileSearcher.DisplaysFolderPath, baseName + "Display");
            
            if (!Directory.Exists(displayFolderPath))
                Directory.CreateDirectory(displayFolderPath);

            string displayPath = Path.Combine(displayFolderPath, baseName + "Display.generated.cs");
            string settingsPath = Path.Combine(displayFolderPath, baseName + "Settings.generated.cs");

            File.WriteAllText(displayPath, GenerateDisplayClass(baseName + "Display", baseName + "Settings"));
            File.WriteAllText(settingsPath, GenerateSettingsClass(baseName + "Settings"));
            
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
    public class {className} : IDisplay
    {{
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name=""cancellationToken""></param>
        /// <returns></returns>
        public Task ShowMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken)
        {{
            {settingsName} settingsLocal = settings as {settingsName};
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            settingsLocal.OnAnimationStart?.Invoke();

            /* Your code here */
            
            settingsLocal.OnAnimationEnd?.Invoke();

            return Task.CompletedTask;
        }}
        
        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name=""transform""> RectTransform of root popup GameObject. </param>
        /// <param name=""settings""> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name=""cancellationToken""></param>
        /// <returns></returns> 
        public Task HideMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken)
        {{
            {settingsName} settingsLocal = settings as {settingsName};
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            settingsLocal.OnAnimationStart?.Invoke();

            /* Your code here */
            
            settingsLocal.OnAnimationEnd?.Invoke();

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
    }}
}}
";
        }

        private static string GenerateSettingsClass(string className)
        {
            return $@"
using System;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{{
    [Serializable]
    public class {className} : BaseSettings
    {{
        /// <summary>
        /// Setting default values.
        /// </summary>
        public {className}()
        {{
        }}
    }}
}}
";
        }
    }
}