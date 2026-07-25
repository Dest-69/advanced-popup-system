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
    /// The APS <b>Order</b> tab. Authors the front-to-back order of popup types inside a canvas — drag a popup up to draw
    /// it above the others, down to put it behind them — and persists it to the consumer's <see cref="PopupOrderConfig"/>
    /// asset (see <see cref="PopupOrderConfigStore"/>).
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
        // Types that actually exist in the project — anything else in _rows is a stale entry (renamed / deleted class).
        private static HashSet<string> _known;
        private static ReorderableList _list;

        // Layer filter: null = all layers, "" = the Unassigned group, otherwise a PopupLayerEnum member name.
        private static string _filterLayer;
        private static string[] _filterOptions;
        private static string[] _filterLayers;

        private static bool _changed;
        private static int _removeIndex = -1;
        // A scan is already queued for the next editor tick — don't queue one per repaint.
        private static bool _scanQueued;

        private static bool _autoSave;
        private static Vector2 _scrollPosition;

        // Shared with the Layers tab on purpose — one Auto-Save preference for the whole window.
        private const string AutoSaveKey = "APS_AutoSaveEnabled";
        private const float RankWidth = 26f;
        private const float DeleteWidth = 24f;
        private const float RowHeight = 20f;

        private static GUIContent _deleteIcon;
        private static GUIContent DeleteIcon =>
            _deleteIcon ??= new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image,
                "Remove this entry — its popup type no longer exists in the project");

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

            // Refresh the layer grouping when the tab is opened after prefabs changed — deferred out of the GUI pass:
            // it shows a progress bar and rebuilds the row set, neither of which belongs inside an open layout group.
            if (PopupOrderConfigStore.IsScanNeeded() && !_scanQueued)
            {
                _scanQueued = true;
                EditorApplication.delayCall += RescanLayers;
            }

            DrawHeader();
            DrawFilter();

            GUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, APSEditorStyles.ScrollViewStyle);

            if (_view.Count == 0)
            {
                GUILayout.Label(_filterLayer == null
                        ? "No popup types found in the project yet."
                        : "No popup is tagged with this layer yet — open a popup's inspector or press Rescan Layers.",
                    APSEditorStyles.WarpedTextStyle);
            }
            else
            {
                GUILayout.Label("Front — drawn above the others in the same canvas", EditorStyles.miniLabel);
                _list.DoLayoutList();
                GUILayout.Label("Back", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Applied after the list is drawn — mutating the list inside a draw callback breaks its own bookkeeping.
            if (_removeIndex >= 0)
            {
                if (_removeIndex < _view.Count)
                {
                    _rows.Remove(_view[_removeIndex]);
                    _changed = true;
                    RebuildView();
                }
                _removeIndex = -1;
            }

            DrawStaleNotice();

            GUILayout.FlexibleSpace();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            DrawFooter();
        }

        #region Rows

        private static void DrawHeader()
        {
            EditorGUILayout.HelpBox(
                "Order inside one canvas: the popup listed higher is drawn in front. Popups of different layers live on " +
                "different canvases — their Sorting Order in the Layers tab decides, not this list.\n" +
                "Popups sharing a position (and any type left at the bottom) keep show order: the last one shown is on top.",
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
                onReorderCallback = _ => ApplyViewOrder()
            };
        }

        /// <summary>Draws one popup type row: its position in the current view, the type name, and a remove button for a stale entry.</summary>
        private static void DrawRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _view.Count) return;

            PopupOrderConfig.Entry entry = _view[index];
            string fullName = entry?.TypeName ?? string.Empty;
            bool stale = _known != null && !_known.Contains(fullName);

            Rect row = new Rect(rect.x, rect.y + 1f, rect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(new Rect(row.x, row.y, RankWidth, row.height), (index + 1).ToString(), EditorStyles.miniLabel);

            Rect deleteRect = new Rect(row.xMax - DeleteWidth, row.y, DeleteWidth, row.height);
            float labelWidth = (stale ? deleteRect.x : row.xMax) - (row.x + RankWidth) - 4f;
            Rect labelRect = new Rect(row.x + RankWidth, row.y, labelWidth, row.height);

            int dot = fullName.LastIndexOf('.');
            string shortName = dot >= 0 ? fullName.Substring(dot + 1) : fullName;
            // Only worth showing the layer when the view isn't already scoped to one.
            string suffix = _filterLayer == null && !string.IsNullOrEmpty(entry?.Layer) ? $"   [{entry.Layer}]" : string.Empty;

            GUI.Label(labelRect,
                new GUIContent(stale ? $"{shortName}  (missing){suffix}" : shortName + suffix, fullName),
                stale ? APSEditorStyles.WarningTextStyle : EditorStyles.label);

            if (stale && GUI.Button(deleteRect, DeleteIcon))
                _removeIndex = index;
        }

        private static void DrawStaleNotice()
        {
            if (_known == null || _rows == null) return;

            int stale = 0;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null && !_known.Contains(_rows[i].TypeName))
                    stale++;
            if (stale == 0) return;

            EditorGUILayout.HelpBox(
                $"{stale} entr{(stale == 1 ? "y" : "ies")} point at a popup type that no longer exists. Kept in case a " +
                "class is mid-rename or its assembly failed to compile — remove them when the rename is done.",
                MessageType.Warning);

            if (GUILayout.Button("Remove missing types"))
            {
                for (int i = _rows.Count - 1; i >= 0; i--)
                    if (_rows[i] == null || !_known.Contains(_rows[i].TypeName))
                        _rows.RemoveAt(i);
                _changed = true;
                RebuildView();
            }
        }

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();
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

        /// <summary>
        /// Re-reads the popup prefabs to refresh the layer grouping, then clears the "scan needed" flag so it runs once per
        /// batch of prefab changes rather than on every repaint or recompile. Unsaved drag edits are flushed first — the
        /// scan only rewrites tags, but it reloads the working copy afterwards. Always runs deferred (see the call site).
        /// </summary>
        private static void RescanLayers()
        {
            EditorApplication.delayCall -= RescanLayers;
            _scanQueued = false;

            try
            {
                // Ask first — the scan opens every prefab in the project, so it is the user's call, not a surprise
                // freeze. Declining clears the flag too: the offer returns when popup prefabs change again.
                if (!EditorUtility.DisplayDialog("Advanced Popup System",
                        "Popup prefabs changed — re-read them to refresh the layer grouping in APS ▸ Order?\n\n" +
                        "This opens every prefab in the project once, so it can take a moment in a large project. " +
                        "It only affects how this list is grouped; the order itself is untouched.",
                        "Rescan", "Not now"))
                    return;

                if (_config == null) LoadState();
                if (_changed) SaveChanges();

                if (PopupOrderConfigStore.ScanPrefabLayers(_config))
                {
                    PopupOrderConfigStore.Save(_config);
                    LoadState();
                }
            }
            finally
            {
                // Even a declined, cancelled or failed scan must clear the flag, or the panel would ask on every repaint.
                PopupOrderConfigStore.ClearScanNeeded();
            }

            // The panel is static and holds no window reference; repaint so the refreshed grouping shows immediately
            // without waiting for the next mouse move (RepaintAllViews, not GetWindow — never steal focus).
            InternalEditorUtility.RepaintAllViews();
        }

        private static void LoadState()
        {
            _config = PopupOrderConfigStore.LoadOrCreate();

            List<string> discovered = PopupOrderConfigStore.DiscoverPopupTypes();
            _known = new HashSet<string>(discovered);

            // New popup types join the catalog on open, so the list always mirrors the project.
            if (PopupOrderConfigStore.Reconcile(_config, discovered))
                PopupOrderConfigStore.Save(_config);

            _rows = new List<PopupOrderConfig.Entry>(_config.Order);
            RebuildView();

            _changed = false;
            _removeIndex = -1;
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
