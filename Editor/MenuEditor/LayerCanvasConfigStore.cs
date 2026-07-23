using System.Collections.Generic;
using AdvancedPS.Core;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Editor-side load / create / reconcile / save for the runtime <see cref="LayerCanvasConfig"/> asset. The asset is
    /// the consumer's per-layer canvas routing (sorting order + optional canvas prefab); it lives in the consumer's
    /// <c>Assets/Resources/</c> — outside the package, so a <c>.unitypackage</c> update never clobbers it (same rule as
    /// <c>AP_Settings.json</c>). The layer <b>names</b> stay owned by <see cref="LayerCatalog"/> (the enum store); this
    /// config sits beside it, keyed by those names.
    /// </summary>
    internal static class LayerCanvasConfigStore
    {
        private const string ResourcesFolder = "Assets/Resources";
        private static string AssetPath => $"{ResourcesFolder}/{LayerCanvasConfig.ResourceName}.asset";

        /// <summary>
        /// The config asset, creating an empty one in <c>Assets/Resources/</c> if none exists yet. Prefers the canonical
        /// path but also honors a relocated asset found anywhere in a Resources folder.
        /// </summary>
        internal static LayerCanvasConfig LoadOrCreate()
        {
            var config = AssetDatabase.LoadAssetAtPath<LayerCanvasConfig>(AssetPath);
            if (config == null)
                config = Resources.Load<LayerCanvasConfig>(LayerCanvasConfig.ResourceName); // relocated by the user?
            if (config != null)
                return config;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            config = ScriptableObject.CreateInstance<LayerCanvasConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// Rebuilds <see cref="LayerCanvasConfig.Entries"/> to hold exactly one entry per current layer name, in the
        /// given order, preserving each layer's sorting order / prefab and dropping orphans (renamed / deleted layers).
        /// Also back-fills the mandatory canvas: any entry without a prefab (a freshly added layer, or an older config
        /// from before the default existed) gets the consumer's <see cref="DefaultCanvas"/>. Returns true when anything
        /// changed (an entry was added, an orphan pruned, or a canvas back-filled) — the caller saves then.
        /// </summary>
        internal static bool Reconcile(LayerCanvasConfig config, IEnumerable<string> names)
        {
            if (config == null)
                return false;
            config.Entries ??= new List<LayerCanvasConfig.Entry>();

            var ordered = new List<LayerCanvasConfig.Entry>();
            bool added = false;
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;
                LayerCanvasConfig.Entry existing = config.Entries.Find(e => e != null && e.Layer == name);
                if (existing == null)
                {
                    existing = new LayerCanvasConfig.Entry { Layer = name };
                    added = true;
                }
                ordered.Add(existing);
            }

            bool filled = FillMissingCanvases(ordered);

            bool changed = added || filled || ordered.Count != config.Entries.Count;
            config.Entries = ordered;
            return changed;
        }

        /// <summary>
        /// The default canvas prefab assigned to layers with no explicit canvas, created on first use (see
        /// <see cref="DefaultCanvasFactory"/>). May be null if the asset could not be created — the runtime keeps its
        /// own fallback for that case.
        /// </summary>
        internal static Canvas DefaultCanvas() => DefaultCanvasFactory.EnsureDefault();

        /// <summary>
        /// Assigns <see cref="DefaultCanvas"/> to every entry that has no canvas. The default is resolved lazily (only
        /// when there is something to fill), so a project whose layers are all assigned — or that has no layers — never
        /// materializes the prefab. Returns true if any entry was filled.
        /// </summary>
        private static bool FillMissingCanvases(List<LayerCanvasConfig.Entry> entries)
        {
            bool filled = false;
            Canvas fallback = null;
            for (int i = 0; i < entries.Count; i++)
            {
                LayerCanvasConfig.Entry e = entries[i];
                if (e == null || e.CanvasPrefab != null)
                    continue;
                fallback ??= DefaultCanvas();
                if (fallback == null)
                    break; // couldn't create it (logged) — leave null; the runtime auto-creates a fallback canvas.
                e.CanvasPrefab = fallback;
                filled = true;
            }
            return filled;
        }

        /// <summary> Flushes in-memory edits to disk. </summary>
        internal static void Save(LayerCanvasConfig config)
        {
            if (config == null)
                return;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}
