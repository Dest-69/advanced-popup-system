using System;
using System.Collections.Generic;
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
        private SerializedProperty _inactiveProperty;
        private SerializedProperty _escapePolicyProperty;
        private SerializedProperty _autoHideOnInitProperty;
        private SerializedProperty _manualInitProperty;
        private SerializedProperty _modulesProperty;
        private SerializedProperty _featuresProperty;
        private SerializedProperty _dragProperty;
        private SerializedProperty _resizeProperty;
        private SerializedProperty _closeProperty;
        private SerializedProperty _focusProperty;
        private SerializedProperty _addressableProperty;
        private SerializedProperty _addressableLoadModeProperty;
        private SerializedProperty _preloadSceneGuidsProperty;
        private SerializedProperty _unloadSceneGuidsProperty;
        private SerializedProperty _poolCapacityProperty;

        // Single-select options for the Popup Layer row. Rebuilt every OnEnable — the Layers panel regenerates
        // PopupLayerEnum, so the set must never be cached statically across domain reloads.
        private string[] _layerNames;
        private int[] _layerValues;

        // This popup type's slot in the Order catalog, resolved once per inspector open (see DrawOrder).
        // -1 = the catalog exists but doesn't list this type; _orderTotal 0 = no catalog at all.
        private int _orderRank = -1;
        private int _orderTotal;

        /// <summary>
        /// Fields drawn by hand above (or intentionally hidden), so the default-inspector fallback skips them.
        /// Static — the set never changes, no need to rebuild it every repaint.
        /// </summary>
        private static readonly string[] ExcludedProperties =
        {
            "PopupLayer", "m_Script", "inspectorShowDisplay", "inspectorHideDisplay",
            "cachedShowSettings", "cachedHideSettings", "AutoHideOnInit", "ManualInit",
            "Modules", "Addressable", "AddressableLoadMode", "PoolCapacity",
            "PreloadSceneGuids", "UnloadSceneGuids", "Inactive", "EscapePolicy"
        };
        
        private void OnEnable()
        {
            _settings ??= SettingsManager.Settings;
            
            _imagesPath = FileSearcher.ImagesFolderPath;
            if (!string.IsNullOrEmpty(_imagesPath))
            {
                _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(_imagesPath + "AP_LogoBlack32.png");
                _popupBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(_imagesPath + "AP_Banner.png");
            }
            
            _popupLayerProperty = serializedObject.FindProperty("PopupLayer");
            _layerNames = Enum.GetNames(typeof(PopupLayerEnum));
            var layerValues = (PopupLayerEnum[])Enum.GetValues(typeof(PopupLayerEnum));
            _layerValues = new int[layerValues.Length];
            for (int i = 0; i < layerValues.Length; i++)
                _layerValues[i] = (int)layerValues[i];
            _inactiveProperty = serializedObject.FindProperty("Inactive");
            _escapePolicyProperty = serializedObject.FindProperty("EscapePolicy");
            _autoHideOnInitProperty = serializedObject.FindProperty("AutoHideOnInit");
            _manualInitProperty = serializedObject.FindProperty("ManualInit");
            _modulesProperty = serializedObject.FindProperty("Modules");
            if (_modulesProperty != null)
            {
                _featuresProperty = _modulesProperty.FindPropertyRelative("Features");
                _dragProperty = _modulesProperty.FindPropertyRelative("Drag");
                _resizeProperty = _modulesProperty.FindPropertyRelative("Resize");
                _closeProperty = _modulesProperty.FindPropertyRelative("Close");
                _focusProperty = _modulesProperty.FindPropertyRelative("Focus");
            }
            _addressableProperty = serializedObject.FindProperty("Addressable");
            _addressableLoadModeProperty = serializedObject.FindProperty("AddressableLoadMode");
            _preloadSceneGuidsProperty = serializedObject.FindProperty("PreloadSceneGuids");
            _unloadSceneGuidsProperty = serializedObject.FindProperty("UnloadSceneGuids");
            _poolCapacityProperty = serializedObject.FindProperty("PoolCapacity");

            ResolveOrderSlot();
        }

        /// <summary>
        /// Looks this popup up in the consumer's Order catalog for the read-only Draw Order row — by its own prefab
        /// first, falling back to its type, exactly as the runtime ranks it. Loaded straight from Resources (not through
        /// <see cref="PopupOrderConfig.Loaded"/>) so a catalog created after this editor session started is still found,
        /// and so the inspector never creates the asset.
        /// </summary>
        private void ResolveOrderSlot()
        {
            _orderRank = -1;
            _orderTotal = 0;

            var config = Resources.Load<PopupOrderConfig>(PopupOrderConfig.ResourceName);
            if (config?.Order == null) return;

            _orderTotal = config.Order.Count;
            if (targets.Length != 1 || !(target is IAdvancedPopup popup)) return;

            int rank = config.GetRank(popup.OrderKey, popup.GetType().FullName);
            if (rank != PopupOrderConfig.UnrankedRank)
                _orderRank = rank;
        }
        
        public override void OnInspectorGUI()
        {
            OnHeaderGUI();

            serializedObject.Update();

            // Computed once and shared by the Addressable/Pool boxes (each call walks the prefab stage).
            bool onPrefabAsset = IsEditingPrefabAsset();

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);

            // Inactive — the master "never show this popup" switch, kept at the very top.
            DrawInactive();

            // Popup Layer + a shortcut into the Layers editor.
            DrawPopupLayer();

            // Where this popup sits among the popups sharing its canvas + a shortcut into the Order editor.
            DrawOrder();

            // Escape close behavior — grouped with the layer controls at the top.
            DrawEscapeClose();

            DrawPreview();

            EditorGUILayoutExtensions.DrawSectionHeader("General Settings");
            DrawBoolPropertiesInGrid();
            EditorGUILayout.EndVertical();

            DrawModules();
            DrawAddressable(onPrefabAsset);
            DrawPool(onPrefabAsset);

            DrawDefaultInspectorExcept(ExcludedProperties);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The Inactive toggle, pinned to the very top. Adds a short warning line while set so it is obvious the popup
        /// is being skipped by Show calls.
        /// </summary>
        private void DrawInactive()
        {
            if (_inactiveProperty == null) return;

            EditorGUILayout.PropertyField(_inactiveProperty, new GUIContent("Inactive",
                "Set 'true' to prevent this popup from being shown."));

            if (!_inactiveProperty.hasMultipleDifferentValues && _inactiveProperty.boolValue)
                GUILayout.Label("Inactive — Show calls are ignored while this is on.", APSEditorStyles.WarningTextStyle);

            EditorGUILayoutExtensions.DrawHorizontalLine();
        }

        /// <summary>
        /// Popup Layer row + a shortcut into the Layers editor. Layers are canvas-bound — a popup belongs to exactly
        /// one layer (None keeps it out of layer control), so this is a single-select, not a flags mask. Legacy
        /// multi-flag values get a warning and a one-click fix that keeps the lowest bit — the same layer the runtime
        /// already routes the canvas by.
        /// </summary>
        private void DrawPopupLayer()
        {
            int current = _popupLayerProperty.intValue;
            bool legacyMulti = (current & (current - 1)) != 0;
            int index = Array.IndexOf(_layerValues, current);
            if (index < 0) index = Array.IndexOf(_layerValues, LowestBit(current));

            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = _popupLayerProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("Popup Layer", index, _layerNames);
            if (EditorGUI.EndChangeCheck() && picked >= 0)
            {
                _popupLayerProperty.intValue = _layerValues[picked];
                // Keep the Order tab's per-layer grouping honest for scene-authored popups too (the prefab
                // postprocessor only sees prefab saves).
                RecordOrderLayer(_layerValues[picked] == 0
                    ? PopupOrderConfigStore.UnassignedLayer
                    : _layerNames[picked]);
            }
            EditorGUI.showMixedValue = false;

            if (GUILayout.Button("Edit Layers", GUILayout.Width(100), GUILayout.ExpandHeight(true)))
                PopupSystemEditor.ShowLayers();
            EditorGUILayout.EndHorizontal();

            if (legacyMulti && !_popupLayerProperty.hasMultipleDifferentValues)
            {
                GUILayout.Label($"Several layers are set ({(PopupLayerEnum)current}) — layers are canvas-bound, a popup belongs to exactly one.",
                    APSEditorStyles.WarningTextStyle);
                if (GUILayout.Button($"Keep '{(PopupLayerEnum)LowestBit(current)}'"))
                    _popupLayerProperty.intValue = LowestBit(current);
            }
        }

        /// <summary>Lowest set bit of a layer mask — matches the runtime's canvas tie-break for legacy multi-flag data.</summary>
        private static int LowestBit(int mask) => mask & -mask;

        /// <summary>
        /// Read-only row showing this popup's draw order inside its canvas (front → back, from the APS Order tool) plus a
        /// shortcut to edit it. The order is authored per <b>prefab</b> in one screen, so there is nothing to edit here —
        /// see <see cref="PopupOrderConfig"/>.
        /// </summary>
        private void DrawOrder()
        {
            EditorGUILayout.BeginHorizontal();

            string label;
            if (targets.Length != 1)
                label = "—  (single popup only)";
            else if (_orderTotal == 0)
                label = "Not configured — show order decides";
            else if (_orderRank < 0)
                label = $"Not listed — behind the {_orderTotal} ordered popup{(_orderTotal == 1 ? "" : "s")}";
            else
                label = $"#{_orderRank + 1} of {_orderTotal}  (1 = front)";

            EditorGUILayout.LabelField("Draw Order", label);

            // Deep-link into this popup's own layer — that canvas is the only place its order matters.
            if (GUILayout.Button("Edit Order", GUILayout.Width(100), GUILayout.ExpandHeight(true)))
                PopupSystemEditor.ShowOrder(CurrentLayerName());
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>The selected popup's layer as an Order-catalog tag: the enum member name (lowest bit for legacy masks), or empty for None / a mixed selection.</summary>
        private string CurrentLayerName()
        {
            if (_popupLayerProperty == null || _popupLayerProperty.hasMultipleDifferentValues)
                return PopupOrderConfigStore.UnassignedLayer;

            int mask = _popupLayerProperty.intValue;
            if (mask == 0) return PopupOrderConfigStore.UnassignedLayer;

            int index = Array.IndexOf(_layerValues, LowestBit(mask));
            return index >= 0 ? _layerNames[index] : PopupOrderConfigStore.UnassignedLayer;
        }

        /// <summary>
        /// Records the selected popup(s) → layer tag in the Order catalog, so the Order tab can group by layer with no
        /// project scan. The prefab postprocessor is the other source; a scene-authored popup has only this one. Cold path:
        /// with no catalog asset yet nothing happens — the inspector must never create it. Deferred out of the GUI pass
        /// because it writes and saves an asset.
        /// </summary>
        private void RecordOrderLayer(string layerName)
        {
            var popups = new List<IAdvancedPopup>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] is IAdvancedPopup popup)
                    popups.Add(popup);
            if (popups.Count == 0) return;

            EditorApplication.delayCall += () =>
            {
                PopupOrderConfig config = PopupOrderConfigStore.Load();
                if (config == null) return;

                bool changed = false;
                for (int i = 0; i < popups.Count; i++)
                {
                    // The popup may be gone by the time this runs (scene closed, object deleted).
                    if (popups[i] == null) continue;
                    changed |= PopupOrderConfigStore.SetLayer(config, popups[i], layerName);
                }
                if (changed)
                    PopupOrderConfigStore.Save(config);
            };
        }

        /// <summary>
        /// Edit-mode Preview: plays the cached show animation, holds ~1s, plays hide, then restores the exact
        /// pre-preview state (see <see cref="PopupPreviewDriver"/>). Disabled in play mode (just Show() the popup
        /// there), for multi-selection, and on the prefab asset itself — open Prefab Mode to preview a prefab.
        /// </summary>
        private void DrawPreview()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var popup = target as IAdvancedPopup;
            bool previewing = PopupPreviewDriver.IsPreviewing(popup);
            bool canPreview = popup != null && targets.Length == 1 &&
                              !EditorApplication.isPlayingOrWillChangePlaymode &&
                              !EditorUtility.IsPersistent(target);

            using (new EditorGUI.DisabledScope(!canPreview))
            {
                GUIContent content = previewing
                    ? new GUIContent("Stop", "Stop the preview and restore the popup state.")
                    : new GUIContent("Preview", EditorGUIUtility.IconContent("PlayButton").image,
                        "Preview the show & hide animation without entering play mode.\n" +
                        "The popup state is restored when the preview ends.");
                if (GUILayout.Button(content, APSEditorStyles.ExperimentalButtonStyle))
                {
                    if (previewing) PopupPreviewDriver.Stop();
                    else PopupPreviewDriver.Start(popup);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Escape stack participation — the policy field alone. APS drives no input of its own: the stack is stepped
        /// from code (<c>AdvancedPopupSystem.EscapeStep</c>), so there is no key to configure here.
        /// </summary>
        private void DrawEscapeClose()
        {
            if (_escapePolicyProperty == null) return;

            EditorGUILayout.PropertyField(_escapePolicyProperty, new GUIContent("Escape Policy"));
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
        
        private void DrawBoolPropertiesInGrid()
        {
            // Addressable popups always initialize hidden (see IAdvancedPopup.Init) — the flag does nothing for them,
            // so show it read-only with a note instead of a toggle that looks meaningful but is ignored.
            bool addressable = _addressableProperty != null && _addressableProperty.boolValue;
            using (new EditorGUI.DisabledScope(addressable))
                DrawInlineToggle(_autoHideOnInitProperty);
            if (addressable)
                GUILayout.Label("Ignored — Addressable popups always load hidden (shown via Show()).",
                    APSEditorStyles.WarpedTextStyle);

            // Same story for ManualInit: an Addressable popup is instantiated by the resolver, which never calls Init()
            // itself, so it must always auto-init on Awake (see IAdvancedPopup.Awake). The flag applies to scene popups only.
            using (new EditorGUI.DisabledScope(addressable))
                DrawInlineToggle(_manualInitProperty);
            if (addressable)
                GUILayout.Label("Ignored — Addressable popups always auto-initialize on load.",
                    APSEditorStyles.WarpedTextStyle);
        }

        /// <summary>
        /// A compact "[x] Label" row: a fixed-width toggle followed by the property's display name. Honors multi-object
        /// editing (mixed value) and writes only on an actual edit, so a multi-selection isn't collapsed each repaint.
        /// </summary>
        private static void DrawInlineToggle(SerializedProperty property)
        {
            if (property == null) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(15));
            if (EditorGUI.EndChangeCheck())
                property.boolValue = value;
            EditorGUI.showMixedValue = false;
            EditorGUILayout.LabelField(property.displayName, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
        }

        #region Modules
        /// <summary>Lines up extra controls with the fields inside a foldout (GUILayout buttons/labels ignore indentLevel).</summary>
        private const float FoldoutIndent = 15f;

        /// <summary>
        /// Draws the interactive-modules box: a feature flag enum, plus a config block revealed only for each enabled
        /// feature (mirrors how the key-binding block reveals its hot keys). Extras that belong to a config — Resize's
        /// "Generate Grips" button, Focus's explanation — live <b>inside</b> that config's foldout, so a collapsed
        /// feature stays a single row.
        /// </summary>
        private void DrawModules()
        {
            if (_modulesProperty == null || _featuresProperty == null) return;

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);
            EditorGUILayoutExtensions.DrawSectionHeader("Modules");

            EditorGUI.showMixedValue = _featuresProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var newVal = (PopupFeatureEnum)EditorGUILayout.EnumFlagsField("Features", (PopupFeatureEnum)_featuresProperty.intValue);
            if (EditorGUI.EndChangeCheck())
                _featuresProperty.intValue = (int)newVal;
            EditorGUI.showMixedValue = false;

            var features = (PopupFeatureEnum)_featuresProperty.intValue;

            if ((features & PopupFeatureEnum.Draggable) != 0 && _dragProperty != null)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                EditorGUILayout.PropertyField(_dragProperty, new GUIContent("Drag"), true);
            }

            if ((features & PopupFeatureEnum.Resizable) != 0 && _resizeProperty != null)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                EditorGUILayout.PropertyField(_resizeProperty, new GUIContent("Resize"), true);

                // Inside the Resize foldout: it belongs to that config, and a collapsed block should stay one line.
                if (_resizeProperty.isExpanded)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(FoldoutIndent);
                    EditorGUI.BeginDisabledGroup(targets.Length != 1);
                    if (GUILayout.Button("Generate Grips"))
                        GenerateGrips();
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                }
            }

            if ((features & PopupFeatureEnum.Closable) != 0 && _closeProperty != null)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                EditorGUILayout.PropertyField(_closeProperty, new GUIContent("Close"), true);
            }

            if ((features & PopupFeatureEnum.Focusable) != 0 && _focusProperty != null)
            {
                EditorGUILayoutExtensions.DrawHorizontalLine();
                EditorGUILayout.PropertyField(_focusProperty, new GUIContent("Focus"), true);

                // Same rule as Generate Grips: the explanation lives inside the foldout it explains.
                if (_focusProperty.isExpanded)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(FoldoutIndent);
                    GUILayout.Label("Pressing the popup raises it above the popups it shares a position with in APS ▸ Order.",
                        APSEditorStyles.WarpedTextStyle);
                    EditorGUILayout.EndHorizontal();
                }
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
        private void DrawAddressable(bool onPrefabAsset)
        {
            if (_addressableProperty == null) return;

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);
            EditorGUILayoutExtensions.DrawSectionHeader("Addressable");

            using (new EditorGUI.DisabledScope(!onPrefabAsset))
                EditorGUILayout.PropertyField(_addressableProperty, new GUIContent("Addressable",
                    "Load this popup's prefab from Addressables on demand instead of placing it in the scene."));

            // Note sits right under the toggle so it reads as "why is this greyed out?".
            if (!onPrefabAsset)
                GUILayout.Label("Only a prefab can be Addressable — edit the prefab asset to change this.",
                    APSEditorStyles.WarpedTextStyle);

            if (_addressableProperty.boolValue)
            {
                using (new EditorGUI.DisabledScope(!onPrefabAsset))
                {
                    if (_addressableLoadModeProperty != null)
                        EditorGUILayout.PropertyField(_addressableLoadModeProperty, new GUIContent("Load Mode"));

                    // Preload Scenes only makes sense for Preload popups; Lazy loads purely on demand, so hide it.
                    if (_addressableLoadModeProperty != null &&
                        _addressableLoadModeProperty.enumValueIndex == (int)LoadMode.Preload)
                        DrawPreloadScenes();

                    // Unload applies to any Addressable popup (Lazy or Preload).
                    DrawSceneChecklist(_unloadSceneGuidsProperty, "Unload on entering scenes");
#if !APS_ADDRESSABLES
                    GUILayout.Label("Install the Addressables package to enable on-demand loading.",
                        APSEditorStyles.WarningTextStyle);
#endif
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Preload Scenes control: a checklist of scenes to preload in. Leaving <b>nothing</b> checked = "every scene"
        /// (the Everyone default, shown as a hint); checking scenes narrows it to only those.
        /// </summary>
        private void DrawPreloadScenes()
        {
            DrawSceneChecklist(_preloadSceneGuidsProperty, "Preload in scenes");

            if (_preloadSceneGuidsProperty != null && _preloadSceneGuidsProperty.arraySize == 0 &&
                !serializedObject.isEditingMultipleObjects)
                GUILayout.Label("Nothing checked → preloads in every scene.", APSEditorStyles.WarpedTextStyle);
        }

        /// <summary>
        /// Scene checklist for a <c>List&lt;string&gt;</c> of scene <b>GUIDs</b> (stable identity — reordering Build
        /// Settings never shifts a selection). Rows = the union of Build-Settings scenes and any GUID already stored (so
        /// a scene later dropped from Build Settings stays visible/removable). Toggling a row adds/removes its GUID.
        /// Single-object edit only — multi-select shows a note (per-object lists differ, no meaningful merged view).
        /// </summary>
        private void DrawSceneChecklist(SerializedProperty listProp, string label)
        {
            if (listProp == null) return;

            GUILayout.Label(label, EditorStyles.miniBoldLabel);

            if (serializedObject.isEditingMultipleObjects)
            {
                GUILayout.Label("Select a single popup prefab to edit its scenes.", APSEditorStyles.WarpedTextStyle);
                return;
            }

            // Build the row set: Build-Settings scenes first, then any stored GUID not among them.
            var rows = new List<(string guid, string label)>();
            var seen = new HashSet<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                string guid = s.guid.ToString();
                if (string.IsNullOrEmpty(guid) || !seen.Add(guid)) continue;
                rows.Add((guid, System.IO.Path.GetFileNameWithoutExtension(s.path)));
            }
            for (int i = 0; i < listProp.arraySize; i++)
            {
                string guid = listProp.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrEmpty(guid) || !seen.Add(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = string.IsNullOrEmpty(path) ? "(missing scene)" : System.IO.Path.GetFileNameWithoutExtension(path);
                rows.Add((guid, name + "  — not in Build Settings"));
            }

            if (rows.Count == 0)
            {
                GUILayout.Label("Add scenes to Build Settings to choose.", APSEditorStyles.WarpedTextStyle);
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var row in rows)
            {
                int idx = IndexOfGuid(listProp, row.guid);
                bool on = idx >= 0;
                bool newOn = EditorGUILayout.ToggleLeft(row.label, on);
                if (newOn == on) continue;
                if (newOn)
                {
                    listProp.arraySize++;
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = row.guid;
                }
                else
                {
                    listProp.DeleteArrayElementAtIndex(idx);
                }
            }
            EditorGUI.indentLevel--;
        }

        private static int IndexOfGuid(SerializedProperty listProp, string guid)
        {
            for (int i = 0; i < listProp.arraySize; i++)
                if (listProp.GetArrayElementAtIndex(i).stringValue == guid)
                    return i;
            return -1;
        }

        /// <summary>
        /// Pool box — kept separate from the Addressable (loading) box. A single "Pool Capacity" value unifies the old
        /// on-hide + max-count controls: -1 keep unlimited (resident), 0 despawn on hide (release), 1 keep one, N keep
        /// up to N idle copies. An info box explains the values; a slider (-1..64) picks common ones while the number
        /// field still accepts larger manual values. Shown only for Addressable popups; editable only on the prefab asset.
        /// </summary>
        private void DrawPool(bool onPrefabAsset)
        {
            if (_addressableProperty == null || !_addressableProperty.boolValue) return;
            if (_poolCapacityProperty == null) return;

            EditorGUILayout.BeginVertical(APSEditorStyles.DarkBackgroundStyle);
            EditorGUILayoutExtensions.DrawSectionHeader("Pool");

            EditorGUILayout.HelpBox(
                "Idle copies kept for reuse when a popup is hidden / despawned:\n" +
                "  -1   keep unlimited (never released)\n" +
                "   0   despawn on hide (free memory immediately)\n" +
                "   1   single on/off instance — keep one, no pool\n" +
                "  2+   pool up to N copies, release the extras",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!onPrefabAsset))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Pool Capacity",
                    "-1 = unlimited, 0 = despawn on hide, 1 = keep one, N = keep up to N. Drag the slider or type a value (values above 64 are allowed)."),
                    GUILayout.Width(90));

                int val = _poolCapacityProperty.intValue;
                bool changed = false;
                EditorGUI.showMixedValue = _poolCapacityProperty.hasMultipleDifferentValues;

                // Feed the slider a clamped value so a manual value above the slider range isn't snapped down unless the
                // user actually drags the slider.
                EditorGUI.BeginChangeCheck();
                int sliderVal = Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(val, -1, 64), -1f, 64f));
                if (EditorGUI.EndChangeCheck()) { val = sliderVal; changed = true; }

                // The number field is the source of truth and accepts any value (including > 64).
                EditorGUI.BeginChangeCheck();
                int fieldVal = EditorGUILayout.IntField(val, GUILayout.Width(55));
                if (EditorGUI.EndChangeCheck()) { val = fieldVal; changed = true; }

                EditorGUI.showMixedValue = false;

                // Only write on an actual edit — assigning every repaint would collapse mixed values when
                // multi-editing popups with different capacities.
                if (changed)
                    _poolCapacityProperty.intValue = val < -1 ? -1 : val; // below -1 is meaningless (-1 already means "unlimited")

                EditorGUILayout.EndHorizontal();
            }

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