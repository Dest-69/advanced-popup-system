using System;
using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// The APS <b>Order</b> tab. Authors the front-to-back order of popup <b>prefabs</b> inside a canvas — drag a popup up
    /// to draw it above the others, down to put it behind them — and persists it to the consumer's
    /// <see cref="PopupOrderConfig"/> asset (see <see cref="PopupOrderConfigStore"/>). Clicking a row selects and pings its
    /// prefab in the Project window.
    /// <para>
    /// Rows are prefabs, not classes: two prefabs of one popup class hold two slots and can be ordered against each other,
    /// and a class with no prefab never shows up. Everything a row draws (name, type, layer) is cached in the entry by
    /// whoever discovered the prefab, so drawing the list loads no assets.
    /// </para>
    /// <para>
    /// <b>The list maintains itself.</b> Discovery, the one-time upgrade of a pre-prefab catalog, and dropping rows whose
    /// prefab is gone all happen on their own (see <see cref="RescanPrefabs"/> and <see cref="PopupOrderPostprocessor"/>) —
    /// this panel has no cleanup buttons on purpose. <b>Rescan Prefabs</b> in the footer is only the manual override.
    /// </para>
    /// <para>
    /// Popups only ever compete <b>within their layer's canvas</b> (the Layers tab picks the canvas + its sorting order), so
    /// the list is filtered by layer — the Layers tab links straight into a specific layer's view
    /// (<see cref="PopupSystemEditor.ShowOrder(string)"/>). The stored order stays a single sequence across all layers: a
    /// total order needs no cross-layer tie-break, so a canvas shared by two layers still sorts deterministically. A drag
    /// inside a filtered view rearranges those popups within the slots they already occupy, leaving every other layer's
    /// relative order untouched.
    /// </para>
    /// </summary>
    public class PopupOrderEditorPanel
    {
        // The whole catalog in stored (global) order; _view is the filtered subset the list draws and reorders.
        private static List<PopupOrderConfig.Entry> _rows;
        private static readonly List<PopupOrderConfig.Entry> _view = new List<PopupOrderConfig.Entry>();
        // Global positions the visible entries occupy, ascending — a reorder writes _view back into exactly these slots.
        private static readonly List<int> _slots = new List<int>();

        private static PopupOrderConfig _config;
        private static ReorderableList _list;

        // Rows whose prefab isn't in the project — transient, until the pass that cleans them up runs. Keyed by entry
        // instance, not by index: a drag reorders _view, and index-parallel state would go stale.
        private static readonly HashSet<PopupOrderConfig.Entry> _missing = new HashSet<PopupOrderConfig.Entry>();

        // Layer filter: null = all layers, "" = the Unassigned group, otherwise a PopupLayerEnum member name.
        private static string _filterLayer;
        private static string[] _filterOptions;
        private static string[] _filterLayers;

        private static bool _changed;
        // A scan is already queued for the next editor tick — don't queue one per repaint.
        private static bool _scanQueued;

        private static bool _autoSave;
        private static Vector2 _scrollPosition;

        // Shared with the Layers tab on purpose — one Auto-Save preference for the whole window.
        private const string AutoSaveKey = "APS_AutoSaveEnabled";
        private const float RankWidth = 26f;
        private const float IconWidth = 18f;
        private const float RowHeight = 20f;

        private static GUIContent _prefabIcon;
        private static GUIContent PrefabIcon => _prefabIcon ??= EditorGUIUtility.IconContent("Prefab Icon");

        public static void Initialize()
        {
            LoadState();
            _autoSave = PlayerPrefs.GetInt(AutoSaveKey, 1) == 1;
        }

        /// <summary> Opens the tab focused on one layer's popups (used by the Layers tab's per-row Order button). </summary>
        public static void FocusLayer(string layerName)
        {
            if (_config == null || _rows == null)
                LoadState();

            _filterLayer = layerName;
            RebuildView();
        }

        public static void OnGUIInternal()
        {
            if (_config == null || _rows == null || _list == null)
                LoadState();

            // Refresh the catalog when the tab is open and something changed under it — deferred out of the GUI pass: it
            // shows a progress bar and rebuilds the row set, neither of which belongs inside an open layout group.
            if (PopupOrderConfigStore.IsScanNeeded())
                QueueScan();

            DrawHeader();
            DrawFilter();

            GUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, APSEditorStyles.ScrollViewStyle);

            if (_view.Count == 0)
            {
                GUILayout.Label(_filterLayer == null
                        ? "No popup prefabs found in the project."
                        : "No popup prefab is on this layer.",
                    APSEditorStyles.WarpedTextStyle);
            }
            else
            {
                GUILayout.Label("Front", EditorStyles.miniLabel);
                _list.DoLayoutList();
                GUILayout.Label("Back", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            DrawFooter();
        }

        #region Rows

        private static void DrawHeader()
        {
            EditorGUILayout.HelpBox(
                "Higher = drawn in front, among the popups sharing a canvas. Across layers the canvas Sorting Order " +
                "decides instead. Click a row to select its prefab.",
                MessageType.Info);
            GUILayout.Space(5);
        }

        /// <summary>Layer filter — the canvas boundary is the layer, so ordering is normally reviewed one layer at a time.</summary>
        private static void DrawFilter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Layer", GUILayout.Width(40));

            int current = Array.IndexOf(_filterLayers, _filterLayer);
            if (current < 0) current = 0;

            int picked = EditorGUILayout.Popup(current, _filterOptions);
            if (picked != current)
            {
                _filterLayer = _filterLayers[picked];
                RebuildView();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        private static void BuildList()
        {
            _list = new ReorderableList(_view, typeof(PopupOrderConfig.Entry), true, false, false, false)
            {
                elementHeight = RowHeight,
                headerHeight = 0f,
                footerHeight = 0f,
                drawElementCallback = DrawRow,
                onSelectCallback = PingRow,
                onReorderCallback = _ => ApplyViewOrder()
            };
        }

        /// <summary>Draws one row: its position in the current view and the prefab it ranks.</summary>
        private static void DrawRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _view.Count) return;

            PopupOrderConfig.Entry entry = _view[index];
            bool missing = _missing.Contains(entry);

            Rect row = new Rect(rect.x, rect.y + 1f, rect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(new Rect(row.x, row.y, RankWidth, row.height), (index + 1).ToString(), EditorStyles.miniLabel);
            GUI.Label(new Rect(row.x + RankWidth, row.y, IconWidth, row.height), PrefabIcon);

            float labelX = row.x + RankWidth + IconWidth + 2f;
            Rect labelRect = new Rect(labelX, row.y, row.xMax - labelX - 4f, row.height);

            GUI.Label(labelRect, LabelOf(entry, missing), missing ? APSEditorStyles.WarningTextStyle : EditorStyles.label);
        }

        /// <summary> Row text: the prefab's name, then the class it is, then the layer when the view spans all of them. </summary>
        private static GUIContent LabelOf(PopupOrderConfig.Entry entry, bool missing)
        {
            string typeName = entry?.TypeName ?? string.Empty;
            int dot = typeName.LastIndexOf('.');
            string shortType = dot >= 0 ? typeName.Substring(dot + 1) : typeName;

            string name = string.IsNullOrEmpty(entry?.PrefabName) ? shortType : entry.PrefabName;
            string type = name == shortType ? string.Empty : $"   ({shortType})";
            // Only worth showing the layer when the view isn't already scoped to one.
            string layer = _filterLayer == null && !string.IsNullOrEmpty(entry?.Layer) ? $"   [{entry.Layer}]" : string.Empty;

            if (missing)
                return new GUIContent($"{name}  (no prefab){type}{layer}", typeName);

            return new GUIContent(name + type + layer,
                $"{typeName}\n{AssetDatabase.GUIDToAssetPath(entry.PrefabGuid)}");
        }

        /// <summary> Selecting a row reveals its prefab in the Project window — the one place the asset is actually loaded. </summary>
        private static void PingRow(ReorderableList list)
        {
            int index = list?.index ?? -1;
            if (index < 0 || index >= _view.Count) return;

            PopupOrderConfig.Entry entry = _view[index];
            if (entry == null || entry.IsTypeOnly) return;

            string path = AssetDatabase.GUIDToAssetPath(entry.PrefabGuid);
            if (string.IsNullOrEmpty(path)) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Rescan Prefabs", GUILayout.Width(120)))
                QueueScan();

            GUILayout.FlexibleSpace();

            GUILayout.Label("Auto-Save", GUILayout.ExpandWidth(false));
            string toggleLabel = EditorGUIUtility.isProSkin ? (_autoSave ? "[x]" : "[ ]") : "";
            bool newAutoSave = GUILayout.Toggle(_autoSave, toggleLabel, APSEditorStyles.ToggleStyle);
            if (newAutoSave != _autoSave)
            {
                _autoSave = newAutoSave;
                PlayerPrefs.SetInt(AutoSaveKey, _autoSave ? 1 : 0);
                PlayerPrefs.Save();
                if (_autoSave && _changed)
                    SaveChanges();
            }

            GUI.enabled = _changed && !_autoSave;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                SaveChanges();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_autoSave && _changed)
                SaveChanges();
        }

        #endregion

        #region View / filter

        /// <summary>
        /// Rebuilds the filtered view and the global slots it occupies. Also refreshes the filter options from the current
        /// <see cref="PopupLayerEnum"/> — the Layers panel regenerates that enum, so the set is never cached across reloads.
        /// </summary>
        private static void RebuildView()
        {
            // Resolved once per rebuild: GUIDToAssetPath loads nothing, but a repaint would still run it per row per frame.
            _missing.Clear();
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null && PopupOrderConfigStore.IsMissing(_rows[i]))
                    _missing.Add(_rows[i]);

            var layers = new List<string> { null, PopupOrderConfigStore.UnassignedLayer };
            var options = new List<string> { "All layers", "Unassigned" };
            foreach (string name in Enum.GetNames(typeof(PopupLayerEnum)))
            {
                if (name == nameof(PopupLayerEnum.None)) continue;
                layers.Add(name);
                options.Add(name);
            }
            _filterLayers = layers.ToArray();
            _filterOptions = options.ToArray();

            if (_filterLayer != null && Array.IndexOf(_filterLayers, _filterLayer) < 0)
                _filterLayer = null; // the layer was renamed or deleted meanwhile

            _view.Clear();
            _slots.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                PopupOrderConfig.Entry entry = _rows[i];
                if (entry == null) continue;
                if (_filterLayer != null && (entry.Layer ?? string.Empty) != _filterLayer) continue;
                _view.Add(entry);
                _slots.Add(i);
            }

            BuildList();
        }

        /// <summary>
        /// Writes a drag inside the filtered view back into the catalog: the visible entries, in their new relative order,
        /// go into the same global slots they occupied before. Entries of other layers keep both their slots and their
        /// order, so arranging one layer can never disturb another.
        /// </summary>
        private static void ApplyViewOrder()
        {
            for (int i = 0; i < _slots.Count && i < _view.Count; i++)
                _rows[_slots[i]] = _view[i];

            _changed = true;
        }

        #endregion

        #region Persistence

        /// <summary> Runs the scan on the next editor tick — never from inside an open IMGUI layout group. </summary>
        private static void QueueScan()
        {
            if (_scanQueued) return;
            _scanQueued = true;
            EditorApplication.delayCall += RescanPrefabs;
        }

        /// <summary>
        /// Re-reads the project's popup prefabs into the catalog, then clears the "scan needed" flag so it runs once per
        /// batch of prefab changes rather than on every repaint or recompile. Unsaved drag edits are flushed first — the
        /// scan works on the stored order and reloads the working copy afterwards. Always runs deferred (see the call site).
        /// <para>
        /// It does <b>not</b> ask. The pass opens every prefab in the project, so it shows a cancelable progress bar, but a
        /// confirm dialog for something the tool needs in order to be correct is just a chore — and the signal that raises
        /// it is deliberately rare (a new catalog, a pre-prefab catalog to upgrade, or a bulk import nobody could inspect).
        /// </para>
        /// </summary>
        private static void RescanPrefabs()
        {
            EditorApplication.delayCall -= RescanPrefabs;
            _scanQueued = false;

            try
            {
                if (_config == null) LoadState();
                if (_changed) SaveChanges();

                if (PopupOrderConfigStore.ScanPrefabs(_config))
                {
                    PopupOrderConfigStore.Save(_config);
                    LoadState();
                }
            }
            finally
            {
                // Even a cancelled or failed scan clears the flag, or the panel would rescan on every repaint.
                PopupOrderConfigStore.ClearScanNeeded();
            }

            // The panel is static and holds no window reference; repaint so the refreshed list shows immediately
            // without waiting for the next mouse move (RepaintAllViews, not GetWindow — never steal focus).
            InternalEditorUtility.RepaintAllViews();
        }

        private static void LoadState()
        {
            _config = PopupOrderConfigStore.LoadOrCreate();

            // A catalog still holding the pre-prefab (per-class) layout upgrades itself: the scan expands every class row
            // into its prefabs in place, so no authored position moves.
            if (PopupOrderConfigStore.NeedsUpgrade(_config))
                PopupOrderConfigStore.MarkScanNeeded();

            _rows = new List<PopupOrderConfig.Entry>(_config.Order);
            RebuildView();

            _changed = false;
        }

        private static void SaveChanges()
        {
            if (_config == null || _rows == null) return;

            _config.Order = new List<PopupOrderConfig.Entry>(_rows);
            PopupOrderConfigStore.Save(_config);
            _changed = false;
        }

        #endregion
    }
}
