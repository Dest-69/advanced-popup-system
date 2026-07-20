using System;
using System.Collections.Generic;
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
        private SerializedProperty _modulesProperty;
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
            _modulesProperty = serializedObject.FindProperty("Modules");

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

            DrawModules();
            DrawAddressable();
            DrawPool();

            DrawDefaultInspectorExcept(new []
            {
                "PopupLayer", "m_Script", "DeepPopups", "inspectorShowDisplay", "inspectorHideDisplay",
                "cachedShowSettings", "cachedHideSettings", "AutoHideOnInit", "ManualInit", "KeyBindingShowSettings",
                "KeyBindingHideSettings", "Modules", "Addressable", "AddressableLoadMode", "AddressableHideBehavior"
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

        #region Modules
        /// <summary>
        /// Draws the interactive-modules box: a feature flag enum, plus a config block revealed only for each enabled
        /// feature (mirrors how the key-binding block reveals its hot keys). Resize also gets a "Generate Grips" button.
        /// </summary>
        private void DrawModules()
        {
            if (_modulesProperty == null) return;
            SerializedProperty featuresProp = _modulesProperty.FindPropertyRelative("Features");
            if (featuresProp == null) return;

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Modules");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayoutExtensions.DrawHorizontalLine();

            EditorGUI.showMixedValue = featuresProp.hasMultipleDifferentValues;
            var newVal = (PopupFeatureEnum)EditorGUILayout.EnumFlagsField("Features", (PopupFeatureEnum)featuresProp.intValue);
            EditorGUI.showMixedValue = false;
            featuresProp.intValue = (int)newVal;

            var features = (PopupFeatureEnum)featuresProp.intValue;

            if ((features & PopupFeatureEnum.Draggable) != 0)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                SerializedProperty dragProp = _modulesProperty.FindPropertyRelative("Drag");
                if (dragProp != null)
                    EditorGUILayout.PropertyField(dragProp, new GUIContent("Drag"), true);
            }

            if ((features & PopupFeatureEnum.Resizable) != 0)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                SerializedProperty resizeProp = _modulesProperty.FindPropertyRelative("Resize");
                if (resizeProp != null)
                    EditorGUILayout.PropertyField(resizeProp, new GUIContent("Resize"), true);

                EditorGUI.BeginDisabledGroup(targets.Length != 1);
                if (GUILayout.Button("Generate Grips"))
                    GenerateGrips();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Creates a default 8-grip set (4 corners + 4 edges) under a dedicated "[Grips]" root added as the LAST
        /// sibling (renders on top, one layer to manage) and writes them into Resize.Grips. Positions are meant to be
        /// hand-tweaked afterwards. Grips are plain RectTransforms (hit-tested by rect — no Graphic needed); add your
        /// own Image if you want them visible at runtime.
        /// </summary>
        private void GenerateGrips()
        {
            if (targets.Length != 1)
            {
                APLogger.LogWarning("[APS] Generate Grips works on a single popup at a time.");
                return;
            }

            var popup = (IAdvancedPopup)target;
            var root = popup.transform as RectTransform;
            if (root == null)
            {
                APLogger.LogError("[APS] Popup root has no RectTransform — cannot generate grips.");
                return;
            }

            RectTransform gripsRoot = FindOrCreateGripsRoot(root);

            // Clear previously generated grips under the root so re-running is idempotent.
            for (int i = gripsRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(gripsRoot.GetChild(i).gameObject);

            var defs = new (string name, Vector2 anchor, Vector2 size, ResizeDirection dir)[]
            {
                ("Grip_BottomLeft",  new Vector2(0f, 0f),   new Vector2(24, 24), ResizeDirection.BottomLeft),
                ("Grip_BottomRight", new Vector2(1f, 0f),   new Vector2(24, 24), ResizeDirection.BottomRight),
                ("Grip_TopLeft",     new Vector2(0f, 1f),   new Vector2(24, 24), ResizeDirection.TopLeft),
                ("Grip_TopRight",    new Vector2(1f, 1f),   new Vector2(24, 24), ResizeDirection.TopRight),
                ("Grip_Left",        new Vector2(0f, 0.5f), new Vector2(20, 40), ResizeDirection.Left),
                ("Grip_Right",       new Vector2(1f, 0.5f), new Vector2(20, 40), ResizeDirection.Right),
                ("Grip_Top",         new Vector2(0.5f, 1f), new Vector2(40, 20), ResizeDirection.Top),
                ("Grip_Bottom",      new Vector2(0.5f, 0f), new Vector2(40, 20), ResizeDirection.Bottom),
            };

            var grips = new List<ResizeGrip>(defs.Length);
            foreach (var d in defs)
            {
                var go = new GameObject(d.name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Generate Grips");
                var rt = (RectTransform)go.transform;
                rt.SetParent(gripsRoot, false);
                rt.anchorMin = rt.anchorMax = d.anchor;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = d.size;
                rt.anchoredPosition = Vector2.zero;
                grips.Add(new ResizeGrip { Rect = rt, Direction = d.dir });
            }

            SerializedProperty gripsProp = _modulesProperty.FindPropertyRelative("Resize").FindPropertyRelative("Grips");
            gripsProp.ClearArray();
            gripsProp.arraySize = grips.Count;
            for (int i = 0; i < grips.Count; i++)
            {
                SerializedProperty el = gripsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("Rect").objectReferenceValue = grips[i].Rect;
                el.FindPropertyRelative("Direction").intValue = (int)grips[i].Direction;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(popup);
        }

        private static RectTransform FindOrCreateGripsRoot(RectTransform root)
        {
            Transform existing = root.Find("[Grips]");
            if (existing is RectTransform existingRect)
                return existingRect;

            var go = new GameObject("[Grips]", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Generate Grips");
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            return rt;
        }
        #endregion

        /// <summary>
        /// Draws the Addressable box: the toggle, plus load mode + on-hide behavior revealed only when it is on
        /// (mirrors the Modules box). The toggle is editable ONLY on the prefab asset / in Prefab Mode — a scene
        /// object can't be Addressable, and editing the flag on an instance would desync it from the prefab, so there
        /// it is shown read-only with a short note. The editor auto-adds the prefab to the "Advanced Popup System"
        /// Addressables group and regenerates the index (see AddressablePopupIndexGenerator).
        /// </summary>
        private void DrawAddressable()
        {
            SerializedProperty addrProp = serializedObject.FindProperty("Addressable");
            if (addrProp == null) return;

            bool onPrefabAsset = IsEditingPrefabAsset();

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Addressable");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayoutExtensions.DrawHorizontalLine();

            using (new EditorGUI.DisabledScope(!onPrefabAsset))
                EditorGUILayout.PropertyField(addrProp, new GUIContent("Addressable",
                    "Load this popup's prefab from Addressables on demand instead of placing it in the scene."));

            // Note sits right under the toggle so it reads as "why is this greyed out?".
            if (!onPrefabAsset)
                GUILayout.Label("Only a prefab can be Addressable — edit the prefab asset to change this.",
                    APSEditorStyles.WarpedTextStyle);

            if (addrProp.boolValue)
            {
                using (new EditorGUI.DisabledScope(!onPrefabAsset))
                {
                    SerializedProperty loadModeProp = serializedObject.FindProperty("AddressableLoadMode");
                    if (loadModeProp != null)
                        EditorGUILayout.PropertyField(loadModeProp, new GUIContent("Load Mode"));
#if APS_ADDRESSABLES
                    GUILayout.Label("Auto-added to the 'Advanced Popup System' Addressables group.",
                        APSEditorStyles.WarpedTextStyle);
#else
                    GUILayout.Label("Install the Addressables package to enable on-demand loading.",
                        APSEditorStyles.WarningTextStyle);
#endif
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Pool box — kept separate from the Addressable (loading) box. Governs what an Addressable popup does when
        /// hidden: Deactivate keeps it resident so it is reused on the next show; Despawn releases it so memory can
        /// unload. Shown only for Addressable popups; editable only on the prefab asset (same rule as the toggle).
        /// </summary>
        private void DrawPool()
        {
            SerializedProperty addrProp = serializedObject.FindProperty("Addressable");
            if (addrProp == null || !addrProp.boolValue) return;

            SerializedProperty hideProp = serializedObject.FindProperty("AddressableHideBehavior");
            if (hideProp == null) return;

            bool onPrefabAsset = IsEditingPrefabAsset();

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Pool");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayoutExtensions.DrawHorizontalLine();

            using (new EditorGUI.DisabledScope(!onPrefabAsset))
                EditorGUILayout.PropertyField(hideProp, new GUIContent("On Hide",
                    "Deactivate — keep the popup resident and reuse it on the next show. Despawn — release it so its memory can unload."));

            if (!onPrefabAsset)
                GUILayout.Label("Editable on the prefab asset only.", APSEditorStyles.WarpedTextStyle);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// True when the inspected popup is the prefab asset itself (selected in the Project) or is open in Prefab
        /// Mode — the only contexts where toggling <c>Addressable</c> is valid. A scene object (plain, or a prefab
        /// instance placed in a scene) returns false, so the flag stays read-only there and can't desync from the prefab.
        /// </summary>
        private bool IsEditingPrefabAsset()
        {
            var comp = target as Component;
            if (comp == null) return false;
            if (EditorUtility.IsPersistent(target)) return true; // prefab asset selected in the Project window
            UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.IsPartOfPrefabContents(comp.gameObject); // open in Prefab Mode
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
                        bannerHeight = availableWidth / aspectRatio;
                
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