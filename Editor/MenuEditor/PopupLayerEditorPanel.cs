using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AdvancedPS.Core;
using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// The APS <b>Layers</b> tab. Edits the layer set (names → <see cref="LayerCatalog"/>, the enum store) and, side by
    /// side, each layer's canvas routing (sorting order + optional canvas prefab → <see cref="LayerCanvasConfig"/>, the
    /// runtime asset). Rows are shown sorted by sorting order for convenience; the underlying <b>name order is left
    /// untouched</b> because it maps 1:1 to <see cref="PopupLayerEnum"/> bit positions — reordering it would renumber the
    /// flags and corrupt every serialized layer mask.
    /// </summary>
    public class PopupLayerEditorPanel
    {
        // _enumNames[0] is always "None"; [1..] are the layers in canonical (bit) order — never reordered by the view.
        private static string[] _enumNames;
        private static LayerCanvasConfig _config;

        private static bool _namesChanged;
        private static bool _configChanged;

        private static bool autoSave;
        private static Vector2 scrollPosition;

        private const string AutoSaveKey = "APS_AutoSaveEnabled";
        private const float OrderWidth = 44f;
        private const float DeleteWidth = 24f;

        private static GUIContent _deleteIcon;
        private static GUIContent DeleteIcon =>
            _deleteIcon ??= new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image, "Delete this layer");

        public static void Initialize()
        {
            LoadState();
            autoSave = PlayerPrefs.GetInt(AutoSaveKey, 1) == 1;
        }

        public static void OnGUIInternal()
        {
            if (_enumNames == null || _config == null)
                LoadState();

            GUILayout.BeginVertical();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, APSEditorStyles.ScrollViewStyle);

            GUILayout.Label("Layers — sorted by canvas order", EditorStyles.miniLabel);

            int deleteIndex = -1;
            foreach (int i in BuildDisplayOrder())
            {
                if (DrawLayerRow(i))
                    deleteIndex = i;
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (deleteIndex >= 0)
            {
                DeleteLayer(deleteIndex);
                if (autoSave) SaveChanges();
            }

            // Only offer a new layer once every existing one is named (an empty row is a layer still being named).
            if (!HasEmptyLayer())
            {
                if (GUILayout.Button("+", APSEditorStyles.BoldButtonStyle, GUILayout.Height(18)))
                    AddLayer();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            DrawFooter();
        }

        #region Rows

        /// <summary>Canonical layer indices [1..N), ordered by sorting order for display (ties → canonical/bit order).</summary>
        private static List<int> BuildDisplayOrder()
        {
            var indices = new List<int>();
            for (int i = 1; i < _enumNames.Length; i++)
                indices.Add(i);

            indices.Sort((a, b) =>
            {
                int oa = EntryFor(_enumNames[a])?.SortingOrder ?? 0;
                int ob = EntryFor(_enumNames[b])?.SortingOrder ?? 0;
                int cmp = oa.CompareTo(ob);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            return indices;
        }

        /// <summary>Draws one layer card. Returns true when its delete button was pressed.</summary>
        private static bool DrawLayerRow(int i)
        {
            string name = _enumNames[i];
            LayerCanvasConfig.Entry entry = EntryFor(name);
            bool deleteRequested = false;

            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Row 1: sorting order · name · delete
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(entry == null))
            {
                int shownOrder = entry?.SortingOrder ?? 0;
                int newOrder = EditorGUILayout.DelayedIntField(shownOrder, GUILayout.Width(OrderWidth));
                if (entry != null && newOrder != entry.SortingOrder)
                {
                    entry.SortingOrder = newOrder;
                    _configChanged = true;
                }
            }

            string newName = EditorGUILayout.DelayedTextField(name);
            if (newName != name)
                RenameLayer(i, newName);

            if (GUILayout.Button(DeleteIcon, GUILayout.Width(DeleteWidth), GUILayout.Height(18)))
                deleteRequested = true;
            GUILayout.EndHorizontal();

            // Row 2: canvas prefab (leave empty → APS auto-creates a plain overlay canvas at the sorting order)
            using (new EditorGUI.DisabledScope(entry == null))
            {
                var shownCanvas = entry?.CanvasPrefab;
                var newCanvas = (Canvas)EditorGUILayout.ObjectField("Canvas", shownCanvas, typeof(Canvas), false);
                if (entry != null && newCanvas != entry.CanvasPrefab)
                {
                    entry.CanvasPrefab = newCanvas;
                    _configChanged = true;
                }
            }

            GUILayout.EndVertical();
            return deleteRequested;
        }

        private static void DrawFooter()
        {
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
                if (autoSave && (_namesChanged || _configChanged))
                    SaveChanges();
            }

            bool pending = _namesChanged || _configChanged;
            GUI.enabled = pending && !autoSave;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                SaveChanges();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (autoSave && pending)
                SaveChanges();
        }

        #endregion

        #region Mutations

        private static void RenameLayer(int i, string raw)
        {
            string validated = ValidateAndFormatEnumName(raw);
            if (validated == null)
            {
                APLogger.LogWarning("Invalid layer name.");
                return;
            }

            string old = _enumNames[i];
            // Rename the config entry in place so its sorting order / prefab survive the rename; a freshly added
            // (empty) row gets its entry created now that it has a valid name. Blanking a name (validated == "")
            // touches no entry — it is left as an orphan and pruned on save, so a blank+retype restores it.
            if (!string.IsNullOrEmpty(validated))
            {
                LayerCanvasConfig.Entry existing = EntryFor(old);
                if (existing != null)
                    existing.Layer = validated;
                else
                    GetOrCreateEntry(validated);
            }

            _enumNames[i] = validated;
            _namesChanged = true;
            _configChanged = true;
        }

        private static void AddLayer()
        {
            System.Array.Resize(ref _enumNames, _enumNames.Length + 1);
            _enumNames[_enumNames.Length - 1] = string.Empty;
            _namesChanged = true;
        }

        private static void DeleteLayer(int i)
        {
            string name = _enumNames[i];
            _config?.Entries?.RemoveAll(e => e != null && e.Layer == name);
            _enumNames = _enumNames.Where((_, idx) => idx != i).ToArray();
            _namesChanged = true;
            _configChanged = true;
        }

        #endregion

        #region Config entries

        private static LayerCanvasConfig.Entry EntryFor(string name)
        {
            if (_config?.Entries == null || string.IsNullOrEmpty(name))
                return null;
            return _config.Entries.Find(e => e != null && e.Layer == name);
        }

        private static LayerCanvasConfig.Entry GetOrCreateEntry(string name)
        {
            if (_config == null || string.IsNullOrEmpty(name))
                return null;
            _config.Entries ??= new List<LayerCanvasConfig.Entry>();
            LayerCanvasConfig.Entry entry = EntryFor(name);
            if (entry == null)
            {
                entry = new LayerCanvasConfig.Entry { Layer = name };
                _config.Entries.Add(entry);
            }
            return entry;
        }

        private static bool HasEmptyLayer()
        {
            for (int i = 1; i < _enumNames.Length; i++)
                if (string.IsNullOrEmpty(_enumNames[i]))
                    return true;
            return false;
        }

        #endregion

        #region Persistence

        private static void LoadState()
        {
            // Source of truth for names is the external store (outside the package); fall back to the compiled enum.
            string[] layers = LayerCatalog.LoadNames() ?? LayerCatalog.CurrentEnumNames();
            _enumNames = new[] { "None" }.Concat(layers).ToArray();

            _config = LayerCanvasConfigStore.LoadOrCreate();
            if (LayerCanvasConfigStore.Reconcile(_config, layers))
                LayerCanvasConfigStore.Save(_config);

            _namesChanged = false;
            _configChanged = false;
        }

        private static void SaveChanges()
        {
            string[] layers = _enumNames.Skip(1).Where(s => !string.IsNullOrEmpty(s)).ToArray();

            // Persist the canvas config FIRST: a name change triggers the enum codegen below, whose asset import can
            // recompile and domain-reload, which would drop any unsaved in-memory edits to the config asset.
            LayerCanvasConfigStore.Reconcile(_config, layers);
            LayerCanvasConfigStore.Save(_config);

            if (_namesChanged)
            {
                // External store is the source of truth; regenerate the enum projection from it (single codegen path).
                LayerCatalog.SaveNames(layers);
                LayerCatalog.Reconcile();
            }

            bool namesChanged = _namesChanged;
            _namesChanged = false;
            _configChanged = false;

            if (namesChanged)
                LoadState();
        }

        private static string ValidateAndFormatEnumName(string enumName)
        {
            if (enumName == null) return null;
            enumName = Regex.Replace(enumName, @"[\s-]+", "_"); // spaces and dashes → underscores
            enumName = Regex.Replace(enumName, "_+", "_");       // collapse consecutive underscores
            enumName = enumName.ToUpper();
            if (enumName.Length == 0) return string.Empty;
            return !Regex.IsMatch(enumName, @"^[A-Z_]+$") ? null : enumName;
        }

        #endregion
    }
}
