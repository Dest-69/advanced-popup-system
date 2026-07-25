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
        private const string UnlockKey = "APS_LayersUnlocked";
        private const float OrderWidth = 44f;
        private const float DeleteWidth = 24f;
        private const float OrderButtonWidth = 52f;

        private static GUIContent _deleteIcon;
        private static GUIContent DeleteIcon =>
            _deleteIcon ??= new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image, "Delete this layer");

        private static readonly GUIContent OrderLabel = new GUIContent(
            "Order",
            "Open APS ▸ Order filtered to this layer — which of its popups is drawn in front of which on this canvas.");

        private static readonly GUIContent EscapeLabel = new GUIContent(
            "Back closes all popups:",
            "This layer is one screen: a single \"back\" (AdvancedPopupSystem.EscapeStep) closes every open popup of the " +
            "layer at once.\nOff — \"back\" closes them one at a time, newest first.");

        private static readonly GUIContent CanvasLabel = new GUIContent(
            "Canvas",
            "Canvas prefab this layer's popups are instantiated under. Required — clearing it resets to the APS default " +
            "canvas (Assets/AdvancedPopupSystem/APS_DefaultCanvas.prefab), which you can edit to control the default for all layers.");

        public static void Initialize()
        {
            LoadState();
            autoSave = PlayerPrefs.GetInt(AutoSaveKey, 1) == 1;
        }

        public static void OnGUIInternal()
        {
            if (_enumNames == null || _config == null)
                LoadState();

            DrawCustomizationHeader();

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
            using (new EditorGUI.DisabledScope(!Unlocked))
            {
                if (!HasEmptyLayer())
                {
                    if (GUILayout.Button("+", APSEditorStyles.BoldButtonStyle, GUILayout.Height(18)))
                        AddLayer();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayoutExtensions.DrawHorizontalLine();
            DrawFooter();
        }

        #region Customization lock

        /// <summary>
        /// Layer add/rename/delete regenerate the compiled <see cref="PopupLayerEnum"/>, so they are locked behind an
        /// explicit opt-in (canvas order/prefab, a consumer-side asset, stay editable). On a read-only Package Manager
        /// install the enum can't be written in place, so unlocking offers to <see cref="FileSearcher.EmbedPackage"/>
        /// (make the package writable) first.
        /// </summary>
        private static bool Unlocked
        {
            get => PlayerPrefs.GetInt(UnlockKey, 0) == 1;
            set { PlayerPrefs.SetInt(UnlockKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private static void DrawCustomizationHeader()
        {
            bool writable = FileSearcher.IsPackageWritable;

            // Same idiom as the Settings tab: a label + a [x]/[ ] toggle (the plain toggle glyph doesn't render in the
            // pro skin, so the state is shown as text).
            GUILayout.BeginHorizontal();
            GUILayout.Label("Customization:", GUILayout.ExpandWidth(false));
            string toggleLabel = EditorGUIUtility.isProSkin ? (Unlocked ? "[x]" : "[ ]") : "";
            bool want = GUILayout.Toggle(Unlocked, toggleLabel, APSEditorStyles.ToggleStyle);
            if (want != Unlocked)
                SetUnlocked(want, writable);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (!Unlocked)
                EditorGUILayout.HelpBox(
                    writable
                        ? "Layer editing is locked. Enable Customization to add, rename or delete layers."
                        : "Installed read-only via Package Manager. Enable Customization to embed the package and edit layers.",
                    MessageType.Info);
            else if (!writable)
                EditorGUILayout.HelpBox(
                    "Embedding… once Unity finishes recompiling, layer edits will save into the embedded package.",
                    MessageType.Warning);
            GUILayout.Space(5);
        }

        private static void SetUnlocked(bool want, bool writable)
        {
            if (want && !writable)
            {
                bool embed = EditorUtility.DisplayDialog(
                    "Unlock layer editing",
                    "Editing layers needs a writable copy of Advanced Popup System.\n\n" +
                    "Embed the package into your project now? It moves into Packages/ and will no longer auto-update " +
                    "via Package Manager (remove the embedded copy later to return to the registry version).",
                    "Embed & unlock", "Cancel");
                if (!embed) return;
                FileSearcher.EmbedPackage();
            }
            Unlocked = want;
        }

        #endregion

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

            // Name + delete regenerate the enum, so they are gated by the Customization lock; the sorting order and
            // canvas prefab (consumer-side asset) stay editable regardless.
            using (new EditorGUI.DisabledScope(!Unlocked))
            {
                string newName = EditorGUILayout.DelayedTextField(name);
                if (newName != name)
                    RenameLayer(i, newName);
            }

            // Straight into this layer's front-to-back order — popups only compete inside their own layer's canvas.
            // Outside the Customization lock: it edits a consumer-side asset, like the canvas fields.
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(name)))
            {
                if (GUILayout.Button(OrderLabel, GUILayout.Width(OrderButtonWidth), GUILayout.Height(18)))
                    PopupSystemEditor.ShowOrder(name);
            }

            using (new EditorGUI.DisabledScope(!Unlocked))
            {
                if (GUILayout.Button(DeleteIcon, GUILayout.Width(DeleteWidth), GUILayout.Height(18)))
                    deleteRequested = true;
            }
            GUILayout.EndHorizontal();

            // Row 2: canvas prefab. Required — every layer routes to a canvas; clearing the field snaps it back to the
            // consumer's default canvas prefab so it can never be left empty.
            using (new EditorGUI.DisabledScope(entry == null))
            {
                var shownCanvas = entry?.CanvasPrefab;
                var newCanvas = (Canvas)EditorGUILayout.ObjectField(CanvasLabel, shownCanvas, typeof(Canvas), false);
                if (entry != null && newCanvas != entry.CanvasPrefab)
                {
                    entry.CanvasPrefab = newCanvas != null ? newCanvas : LayerCanvasConfigStore.DefaultCanvas();
                    _configChanged = true;
                }

                // Row 3: escape grouping — the layer is a screen, so one "back" closes all of its popups. Same
                // label + [x]/[ ] idiom as the panel's other toggles (the plain toggle glyph is invisible in the pro skin).
                bool shownEscape = entry?.EscapeClosesLayer ?? false;
                GUILayout.BeginHorizontal();
                GUILayout.Label(EscapeLabel, GUILayout.ExpandWidth(false));
                string escapeToggleLabel = EditorGUIUtility.isProSkin ? (shownEscape ? "[x]" : "[ ]") : "";
                bool newEscape = GUILayout.Toggle(shownEscape, escapeToggleLabel, APSEditorStyles.ToggleStyle);
                GUILayout.EndHorizontal();
                if (entry != null && newEscape != entry.EscapeClosesLayer)
                {
                    entry.EscapeClosesLayer = newEscape;
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
                APLogger.LogWarning($"Invalid layer name '{raw}'. Use Latin letters, digits and underscore only; it can't start with a digit.");
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
            // Append an empty "being named" row. Deliberately do NOT set _namesChanged: an unnamed row is not part of
            // the persisted set (SaveChanges strips empties), and with Auto-Save on it would make DrawFooter run
            // SaveChanges + LoadState this same OnGUI pass, wiping the row before it can be typed into — the "+" button
            // would look like a no-op. RenameLayer dirties the set once the row gets a valid name.
            System.Array.Resize(ref _enumNames, _enumNames.Length + 1);
            _enumNames[_enumNames.Length - 1] = string.Empty;
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
                // Canvas is mandatory — seed a new layer with the default so its row is populated immediately, before
                // the next save's Reconcile would back-fill it anyway.
                entry = new LayerCanvasConfig.Entry { Layer = name, CanvasPrefab = LayerCanvasConfigStore.DefaultCanvas() };
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
            // Enum members may contain digits (UI2, LAYER1) but must not start with one, and stay ASCII — matches C#
            // identifier rules and LayerCatalog.Sanitize (which must agree, or a name the panel accepts is stripped on save).
            return !Regex.IsMatch(enumName, @"^[A-Z_][A-Z0-9_]*$") ? null : enumName;
        }

        #endregion
    }
}
