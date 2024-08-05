using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    public class PopupDisplaysEditorPanel : EditorWindow
    {
        private static string[] _displayNames;
        private static bool[] _displayNameChanged;
        
        private static Vector2 scrollPosition;

        public static void Initialize()
        {
            LoadEnumNames();
        }
        
        public static void OnGUIInternall()
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
                else
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Delete",
                                $"Are you sure you want to delete the display '{_displayNames[i]}'? This will delete the display and its settings scripts.",
                                "Delete", "Cancel"))
                        {
                            DeleteDisplay(i);
                            DeleteDisplayAndSettingsFiles(newEnumName);
                        }
                    }
                }
                
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
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

            GUI.enabled = _displayNameChanged.Any(changed => changed);
            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                SaveDisplayChanges();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static void LoadEnumNames()
        {
            _displayNames = Assembly.GetAssembly(typeof(IDisplay)).GetTypes()
                .Where(t => typeof(IDisplay).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => t.Name).ToArray();
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
            if (displayName == null)
                return string.Empty;

            displayName = Regex.Replace(displayName, @"[\s-]+", ""); // Remove spaces and dashes
            displayName = Regex.Replace(displayName, "_+", ""); // Remove consecutive underscores

            return !Regex.IsMatch(displayName, @"^[a-zA-Z]+$") ? null : displayName;
        }
        
        private static void SaveDisplayChanges()
        {
            List<string> cleanDisplayNames = new List<string>();

            foreach (string display in _displayNames)
            {
                if (string.IsNullOrEmpty(display))
                    continue;

                string displayName = TypeHelper.RemoveDisplaySuffix(display);
                cleanDisplayNames.Add(displayName);

                string displayClass = displayName + "Display";
        
                if (TypeHelper.GetTypeByName(displayClass) == null)
                {
                    CreateDisplayAndSettingsFiles(displayName);
                }
            }

            AssetDatabase.Refresh();
            APSCodeGenerator.Execute(cleanDisplayNames.ToArray());
        }
        
        private static void DeleteDisplayAndSettingsFiles(string displayName)
        {
            string displayFolderPath = Path.Combine(FileSearcher.DisplaysFolderPath, displayName);
            if (Directory.Exists(displayFolderPath))
            {
                DirectoryInfo di = new DirectoryInfo(displayFolderPath);
                di.Delete(true);
            }

            SaveDisplayChanges();
        }
        
        private static void CreateDisplayAndSettingsFiles(string displayName)
        {
            string displayFolderPath = Path.Combine(FileSearcher.DisplaysFolderPath, displayName + "Display");
            if (!Directory.Exists(displayFolderPath))
            {
                Directory.CreateDirectory(displayFolderPath);
            }

            string displayPath = Path.Combine(displayFolderPath, displayName + "Display.generated.cs");
            string settingsPath = Path.Combine(displayFolderPath, displayName + "Settings.generated.cs");

            File.WriteAllText(displayPath, GenerateDisplayClass(displayName + "Display", displayName + "Settings"));
            File.WriteAllText(settingsPath, GenerateSettingsClass(displayName + "Settings"));
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