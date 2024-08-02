using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    [CustomEditor(typeof(IAdvancedPopup), true)]
    public class IAdvancedPopupEditor : UnityEditor.Editor
    {
        private PopupSettings Settings;
        
        private static string imagesPath;
        private static Texture2D popupIcon;
        private static Texture2D popupBunner;
        private Dictionary<Type, List<Type>> cachedTypes;

        private bool isSettings;
        private bool isShowSettings;
        private bool isHideSettings;
        private const string IsSettingsKey = "APS_AutoSaveEnabled";
        
        private void OnEnable()
        {
            if (Settings == null)
            {
                Settings = SettingsManager.Settings;
            }
            
            if (popupIcon == null)
            {
                imagesPath = FileSearcher.ImagesFolderPath;
                popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(imagesPath + "AP_LogoBlack32.png");
            }

            if (popupBunner == null)
            {
                popupBunner = AssetDatabase.LoadAssetAtPath<Texture2D>(imagesPath + "AP_Banner.png");
            }

            isShowSettings = true;
            
            if (!PlayerPrefs.HasKey(IsSettingsKey))
            {
                PlayerPrefs.SetInt(IsSettingsKey, 1);
                isSettings = true;
            }
            else
            {
                isSettings = PlayerPrefs.GetInt(IsSettingsKey) == 1;
            }
            
            //CacheTypes();
        }
        
        private void CacheTypes()
        {
            cachedTypes = new Dictionary<Type, List<Type>>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract)
                    {
                        foreach (var baseType in type.GetInterfaces().Concat(new[] { type.BaseType }).Where(t => t != null))
                        {
                            if (!cachedTypes.ContainsKey(baseType))
                            {
                                cachedTypes[baseType] = new List<Type>();
                            }
                            cachedTypes[baseType].Add(type);
                        }
                    }
                }
            }
        }
        
        public override void OnInspectorGUI()
        {
            if (Settings.CustomIconsEnabled)
                OnHeaderGUI();
            
            serializedObject.Update();
            
            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);
            EditorGUILayout.BeginHorizontal();
            SerializedProperty popupLayerProperty = serializedObject.FindProperty("PopupLayer");
            EditorGUILayout.PropertyField(popupLayerProperty, new GUIContent("Popup Layer"));

            if (GUILayout.Button("Edit Layers", new GUILayoutOption[] { GUILayout.Width(100), GUILayout.ExpandHeight(true) }))
            {
                PopupSystemEditor.ShowLayers();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = false;
            isSettings = false;
            if (GUILayout.Button(isSettings ? "Hide settings" : "Show settings", GUILayout.Width(100)))
            {
                isSettings = !isSettings;
                PlayerPrefs.SetInt(IsSettingsKey, isSettings ? 1 : 0);
            }
            if (GUILayout.Button(new GUIContent("Preview", 
                        EditorGUIUtility.IconContent("console.warnicon.sml").image, 
                        "-Experimental-\nPreview show & hide animation in editor."), APSEditorStyles.ExperimentalButtonStyle))
            {
                ExperimentalShowHide();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            DrawPopupSettings();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("General Settings");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            
            DrawBoolPropertiesInGrid(new string[] { "AutoHideOnInit", "ManualInit"});
            DrawDeepPopupsProperty();
            EditorGUILayout.EndVertical();

            DrawDefaultInspectorExcept(new string[]
            {
                "PopupLayer", "m_Script", "DeepPopups", "inspectorShowDisplay", "inspectorHideDisplay",
                "HotKeyShow", "HotKeyHide", "cachedShowSettings", "cachedHideSettings", "AnyHotKeyHide",
                "AnyHotKeyShow", "AutoHideOnInit", "ManualInit"
            });

            serializedObject.ApplyModifiedProperties();
        }

        private async void ExperimentalShowHide()
        {
            var targetType = target.GetType();
            var methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var initMethod = methods.FirstOrDefault(m => m.Name == "Init");
            var showMethod = methods.FirstOrDefault(m => m.Name == "ShowAsync" && !m.IsGenericMethod);
            var hideMethod = methods.FirstOrDefault(m => m.Name == "HideAsync" && !m.IsGenericMethod);
            
            if (initMethod == null || showMethod == null || hideMethod == null)
            {
                Debug.LogError("Not found methods Init/ShowAsync/HideAsync - preview was safely aborted.");
                return;
            }
            
            var popup = (IAdvancedPopup)target;
            bool wasActive = popup.gameObject.activeSelf;
            bool wasVisible = popup.GetComponent<CanvasGroup>().alpha != 0 && popup.transform.localScale != Vector3.zero;
            
            if (!wasActive)
                popup.gameObject.SetActive(true);

            initMethod.Invoke(target, null);

            if (wasVisible)
            {
                popup.IsBeVisible = true;
                popup.IsVisible = true;
                await (Task)hideMethod.Invoke(target, new object[] { default, null });
                await (Task)showMethod.Invoke(target, new object[] { default, null });
            }
            else
            {
                popup.IsBeVisible = false;
                popup.IsVisible = false;
                await (Task)showMethod.Invoke(target, new object[] { default, null });
                await (Task)hideMethod.Invoke(target, new object[] { default, null });
            }
            
            if (!wasActive)
                popup.gameObject.SetActive(false);
        }

        private void DrawPopupSettings()
        {
            IAdvancedPopup popup = (IAdvancedPopup)target;
            
            float width = Screen.width / 4.3f;
            EditorGUILayoutExtensions.DrawHorizontalLine();
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(width));
            // Show Settings Button
            GUI.enabled = isHideSettings;
            if (GUILayout.Button("Show Settings"))
            {
                isShowSettings = true;
                isHideSettings = false;
            }
            GUILayout.EndVertical();

            EditorGUILayoutExtensions.DrawVerticalLine();

            GUILayout.BeginVertical(GUILayout.Width(width));
            GUI.enabled = isShowSettings;
            // Hide Settings Button
            if (GUILayout.Button("Hide Settings"))
            {
                isShowSettings = false;
                isHideSettings = true;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            
            GUI.enabled = true;
            if (isShowSettings)
            {
                EditorGUILayout.BeginHorizontal();
                popup.AnyHotKeyShow = EditorGUILayout.Toggle(popup.AnyHotKeyShow, GUILayout.Width(15));
                EditorGUILayout.LabelField("Show by any key");
                EditorGUILayout.EndHorizontal();
                if (!popup.AnyHotKeyShow)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty hotKeyShow = serializedObject.FindProperty("HotKeyShow");
                    EditorGUILayout.PropertyField(hotKeyShow, true);
                    EditorGUI.indentLevel--;
                }
            }
            else if (isHideSettings)
            {
                EditorGUILayout.BeginHorizontal();
                popup.AnyHotKeyHide = EditorGUILayout.Toggle(popup.AnyHotKeyHide, GUILayout.Width(15));
                EditorGUILayout.LabelField("Hide by any key");
                EditorGUILayout.EndHorizontal();
                if (!popup.AnyHotKeyHide)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty hotKeyHide = serializedObject.FindProperty("HotKeyHide");
                    EditorGUILayout.PropertyField(hotKeyHide, true);
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayoutExtensions.DrawHorizontalLine();
        }

        private void DrawSettingsColumn(string hotKeyProperty, string dropdownProperty, string settingsProperty)
        {
            GUILayout.BeginVertical();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            EditorGUILayout.BeginVertical(APSEditorStyles.BackgroundStyle);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(hotKeyProperty));

            if (isSettings)
            {
                GUILayout.Space(5);
                DrawTypeDropdown(dropdownProperty, "Display:", typeof(IDisplay), settingsProperty);
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
                EditorGUILayout.BeginVertical(APSEditorStyles.BackgroundStyle);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(settingsProperty),
                    new GUIContent("Display Settings"), true);
                GUILayout.Space(15);
            }

            EditorGUILayout.EndVertical();
            GUILayout.EndVertical();
        }
        
        private void DrawMiniSettingsColumn(float width)
        {
            GUILayout.BeginVertical();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            EditorGUILayout.BeginVertical(APSEditorStyles.BackgroundStyle, GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Hided", GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        private void DrawTypeDropdown(string propertyName, string label, Type baseType, string settingsProperty)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property {propertyName} not found.");
                return;
            }

            if (!cachedTypes.TryGetValue(baseType, out var types) || types.Count == 0)
            {
                Debug.LogError($"No types found inheriting from {baseType}.");
                return;
            }

            var typeNames = types.Select(t => t.Name).ToList();

            // Ensure property has a valid default value if it's null or empty
            if (string.IsNullOrEmpty(property.stringValue) || types.All(t => t.FullName != property.stringValue))
            {
                property.stringValue = types[0].FullName;
            }
            
            int selectedIndex = types.FindIndex(t => t.FullName == property.stringValue);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(50));
            EditorGUILayout.Popup(selectedIndex, typeNames.ToArray());
            EditorGUILayout.EndHorizontal();
        }
        
        private void UpdateCachedSettings(IAdvancedPopup popup, string displayProperty, string settingsProperty)
        {
            SerializedProperty displayProp = serializedObject.FindProperty(displayProperty);
            SerializedProperty settingsProp = serializedObject.FindProperty(settingsProperty);

            Type displayType = Type.GetType(displayProp.stringValue);
            if (displayType != null)
            {
                Type settingsType = TypeHelper.GetTypeByName(TypeHelper.RemoveDisplaySuffix(displayType.Name) + "Settings");
                settingsProp.managedReferenceValue = Activator.CreateInstance(settingsType);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawDefaultInspectorExcept(string[] propertyNamesToExclude)
        {
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);
            do
            {
                if (Array.IndexOf(propertyNamesToExclude, property.name) < 0)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            while (property.NextVisible(false));
        }
        
        private void DrawDeepPopupsProperty()
        {
            SerializedProperty deepPopupsProperty = serializedObject.FindProperty("DeepPopups");
            if (deepPopupsProperty != null)
            {
                EditorGUILayout.PropertyField(deepPopupsProperty, true);
            }
        }
        
        private void DrawBoolPropertiesInGrid(string[] names)
        {
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);

            do
            {
                if (!names.Any(s => s.Equals(property.name))) continue;
                
                EditorGUILayout.BeginHorizontal();
                property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(15));
                EditorGUILayout.LabelField(property.displayName, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }
            while (property.NextVisible(false));
        }
 
        protected override void OnHeaderGUI()
        {
            if (popupIcon != null)
            {
                float availableWidth = Screen.width;
                float aspectRatio = (float)popupBunner.width / popupBunner.height;
                float bannerHeight = availableWidth / aspectRatio - 75;
                
                var rectBanner = EditorGUILayout.GetControlRect(false, bannerHeight, GUILayout.ExpandWidth(true));
                rectBanner.y -= 5;
                rectBanner.height += 30;
                
                GUI.DrawTexture(rectBanner, popupBunner, ScaleMode.ScaleToFit);
                
                var rectIcon = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                rectIcon.y -= 26 + bannerHeight;
                rectIcon.x = 18;
                rectIcon.xMax = 36;
                EditorGUI.DrawPreviewTexture(rectIcon, popupIcon);
            }
            else
            {
                base.OnHeaderGUI();
            }
        }
    }
}