using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(IAdvancedPopup), true)]
    public class IAdvancedPopupEditor : UnityEditor.Editor
    {
        private PopupSettings _settings;
        
        private static string _imagesPath;
        private static Texture2D _popupIcon;
        private static Texture2D _popupBanner;

        private SerializedProperty _popupLayerProperty;
        private SerializedProperty _deepPopupsProperty;
        private SerializedProperty _keyBindingSettings;
        private SerializedProperty _anyHotKey;
        private SerializedProperty _hotKeys;
        private SerializedProperty _layers;
        private SerializedProperty _popups;
        private SerializedProperty _actions;
        
        private bool _isHideSettings;

        private const string IsHideKeySettings = "APS_HideKeySettingsEnabled";
        
        private void OnEnable()
        {
            _settings ??= SettingsManager.Settings;
            
            _imagesPath = FileSearcher.ImagesFolderPath;
            _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(_imagesPath + "AP_LogoBlack32.png");
            _popupBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(_imagesPath + "AP_Banner.png");
            
            _popupLayerProperty = serializedObject.FindProperty("PopupLayer");
            _deepPopupsProperty = serializedObject.FindProperty("DeepPopups");

            if (!PlayerPrefs.HasKey(IsHideKeySettings))
                PlayerPrefs.SetInt(IsHideKeySettings, 0);
            else
                _isHideSettings = PlayerPrefs.GetInt(IsHideKeySettings) == 1;
            
            _keyBindingSettings = serializedObject.FindProperty(_isHideSettings ? "KeyBindingHideSettings" : "KeyBindingShowSettings");
            _anyHotKey = _keyBindingSettings.FindPropertyRelative("AnyHotKey");
            _hotKeys = _keyBindingSettings.FindPropertyRelative("HotKeys");
            _layers = _keyBindingSettings.FindPropertyRelative("Layers");
            _popups = _keyBindingSettings.FindPropertyRelative("Popups");
            _actions = _keyBindingSettings.FindPropertyRelative("OnTrigger");
        }
        
        public override void OnInspectorGUI()
        {
            OnHeaderGUI();
            
            serializedObject.Update();
            
            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.showMixedValue = _popupLayerProperty.hasMultipleDifferentValues;
            var newVal = (PopupLayerEnum)EditorGUILayout.EnumFlagsField("Popup Layer", (PopupLayerEnum)_popupLayerProperty.intValue);
            EditorGUI.showMixedValue = false;
            _popupLayerProperty.intValue = (int)newVal;
            
            if (GUILayout.Button("Edit Layers", new GUILayoutOption[] { GUILayout.Width(100), GUILayout.ExpandHeight(true) }))
            {
                PopupSystemEditor.ShowLayers();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = false;
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
            
            DrawBoolPropertiesInGrid(new[] { "AutoHideOnInit", "ManualInit"});
            DrawDeepPopupsProperty();
            EditorGUILayout.EndVertical();

            DrawDefaultInspectorExcept(new []
            {
                "PopupLayer", "m_Script", "DeepPopups", "inspectorShowDisplay", "inspectorHideDisplay",
                "cachedShowSettings", "cachedHideSettings", "AutoHideOnInit", "ManualInit", "KeyBindingShowSettings",
                "KeyBindingHideSettings"
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
            float width = EditorGUIUtility.currentViewWidth / 4.3f;
            EditorGUILayoutExtensions.DrawHorizontalLine();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width));
            // Show Settings Button
            GUI.enabled = _isHideSettings;
            GUIStyle buttonStyle = _isHideSettings ? APSEditorStyles.BoldButtonStyle : GUI.skin.button;
            if (GUILayout.Button("Switch to Show Settings", buttonStyle))
            {
                PlayerPrefs.SetInt(IsHideKeySettings, 0);
                _isHideSettings = false;
                BindKeyBindingProps(false);
            }
            GUILayout.EndVertical();

            EditorGUILayoutExtensions.DrawVerticalLine();

            GUILayout.BeginVertical(GUILayout.Width(width));
            GUI.enabled = !_isHideSettings;
            buttonStyle = !_isHideSettings ? APSEditorStyles.BoldButtonStyle : GUI.skin.button;
            // Hide Settings Button
            if (GUILayout.Button("Switch to Hide Settings", buttonStyle))
            {
                PlayerPrefs.SetInt(IsHideKeySettings, 1);
                _isHideSettings = true;
                BindKeyBindingProps(true);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            
            GUI.enabled = true;
            DrawPopupKeyBindingSettings();

            EditorGUILayoutExtensions.DrawHorizontalLine();
        }
        
        private void BindKeyBindingProps(bool hide) 
        {
            _keyBindingSettings = serializedObject.FindProperty(hide ? "KeyBindingHideSettings" : "KeyBindingShowSettings");
            _anyHotKey = _keyBindingSettings.FindPropertyRelative("AnyHotKey");
            _hotKeys   = _keyBindingSettings.FindPropertyRelative("HotKeys");
            _layers    = _keyBindingSettings.FindPropertyRelative("Layers");
            _popups    = _keyBindingSettings.FindPropertyRelative("Popups");
            _actions   = _keyBindingSettings.FindPropertyRelative("OnTrigger");
        }
        
        private void DrawPopupKeyBindingSettings()
        {
            EditorGUILayoutExtensions.DrawHorizontalLine();
            
            string lableName = _isHideSettings ? "Hide" : "Show";
            if (_settings.KeyEventSystemEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{lableName} Key Settings");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Key Event Tracking disabled.", APSEditorStyles.WarningTextStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("To turn it on, toggle 'Key Event Tracking' on, in the APS settings menu.", APSEditorStyles.WarpedTextStyle);
                if (GUILayout.Button("APS settings"))
                    PopupSystemEditor.ShowSettings();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            EditorGUILayoutExtensions.DrawHorizontalLine();
            
            if (!_settings.KeyEventSystemEnabled) return;
            
            _anyHotKey.boolValue = EditorGUILayout.Toggle("Any Hot Key", _anyHotKey.boolValue);

            if (!_anyHotKey.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_hotKeys, new GUIContent("Hot Keys"), true);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.PropertyField(_layers, new GUIContent($"{lableName} if Layer active"));
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_popups, new GUIContent($"{lableName} if Popup active"));
            EditorGUI.indentLevel--;
            EditorGUILayout.PropertyField(_actions, new GUIContent($"Invoke UnityEvent on {lableName} key press"));
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
            if (_deepPopupsProperty != null)
            {
                EditorGUILayout.PropertyField(_deepPopupsProperty, true);
            }
        }
        
        private void DrawBoolPropertiesInGrid(string[] names)
        {
            foreach (string n in names) 
            {
                SerializedProperty p = serializedObject.FindProperty(n);
                if (p == null) continue;
                EditorGUILayout.BeginHorizontal();
                p.boolValue = EditorGUILayout.Toggle(p.boolValue, GUILayout.Width(15));
                EditorGUILayout.LabelField(p.displayName, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }
        }
 
        protected override void OnHeaderGUI()
        {
            switch (_settings.InspectorView)
            {
                case InspectorEnum.UnityInspector:
                    DrawDefaultInspector();
                    break;
                case InspectorEnum.APSOptimized:
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target),
                        typeof(MonoBehaviour), false);
                    EditorGUILayout.Space(5);
                    GUI.enabled = true;
                    break;
                case InspectorEnum.APSInspector:
                {
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target),
                        typeof(MonoBehaviour), false);
                    GUI.enabled = true;
            
                    float availableWidth = EditorGUIUtility.currentViewWidth;
                    float bannerHeight = 0;

                    if (_popupBanner != null)
                    {
                        float aspectRatio = (float)_popupBanner.width / _popupBanner.height;
                        bannerHeight = availableWidth / aspectRatio - 75;
                
                        Rect rectBanner = EditorGUILayout.GetControlRect(false, bannerHeight, GUILayout.ExpandWidth(true));
                        rectBanner.y -= 2;
                        rectBanner.height += 30;

                        GUI.DrawTexture(rectBanner, _popupBanner, ScaleMode.ScaleToFit);
                    }

                    if (_popupIcon != null)
                    {
                        var rectIcon = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        rectIcon.y -= 46 + bannerHeight;
                        rectIcon.x = 18;
                        rectIcon.xMax = 36;
                
                        EditorGUI.DrawPreviewTexture(rectIcon, _popupIcon);
                    }

                    break;
                }
            }
        }
    }
}