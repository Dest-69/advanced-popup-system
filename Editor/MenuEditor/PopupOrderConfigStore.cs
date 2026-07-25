using System;
using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Editor-side load / create / reconcile / save for the runtime <see cref="PopupOrderConfig"/> asset — the consumer's
    /// front-to-back order of popup types inside a canvas. Like <see cref="LayerCanvasConfigStore"/> the asset lives in
    /// the consumer's <c>Assets/Resources/</c>, outside the package, so an update never clobbers it.
    /// <para>
    /// Also keeps each entry's <see cref="PopupOrderConfig.Entry.Layer"/> tag, which exists purely so the Order panel can
    /// show one layer at a time. Two cheap sources feed it — <see cref="ScanPrefabLayers"/> (explicit, full scan) and
    /// <see cref="SetLayer"/> (called per changed prefab by <see cref="PopupOrderPostprocessor"/>) — and a missing or
    /// stale tag only affects grouping, never the runtime order (see PopupOrderConfig.Entry).
    /// </para>
    /// </summary>
    internal static class PopupOrderConfigStore
    {
        private const string ResourcesFolder = "Assets/Resources";
        private static string AssetPath => $"{ResourcesFolder}/{PopupOrderConfig.ResourceName}.asset";

        /// <summary> Layer name used for entries whose layer isn't known (never scanned) or that carry no layer. </summary>
        internal const string UnassignedLayer = "";

        /// <summary>
        /// The config asset, creating an empty one in <c>Assets/Resources/</c> if none exists yet. Prefers the canonical
        /// path but also honors a relocated asset found anywhere in a Resources folder.
        /// </summary>
        internal static PopupOrderConfig LoadOrCreate()
        {
            PopupOrderConfig config = Load();
            if (config != null)
                return config;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            config = ScriptableObject.CreateInstance<PopupOrderConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// The config asset if it exists, else null — the <b>cold path</b> for callers that must not bring the asset into
        /// existence (the import postprocessor, the popup inspector). Mirrors the Addressable generator's rule: tooling
        /// creates project assets only when the user actually opens the panel.
        /// </summary>
        internal static PopupOrderConfig Load()
        {
            var config = AssetDatabase.LoadAssetAtPath<PopupOrderConfig>(AssetPath);
            return config != null ? config : Resources.Load<PopupOrderConfig>(PopupOrderConfig.ResourceName);
        }

        /// <summary>
        /// Every concrete popup type in the project (package + consumer assemblies), by <c>Type.FullName</c>, sorted
        /// alphabetically so newly discovered types always join the catalog in a deterministic order. Uses
        /// <see cref="TypeCache"/> — an index Unity already maintains, so this costs no assembly scan and no prefab load
        /// (the catalog is keyed by type, not by prefab; see PopupOrderConfig).
        /// </summary>
        internal static List<string> DiscoverPopupTypes()
        {
            var names = new List<string>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IAdvancedPopup>())
            {
                // Abstract bases and open generics (AdvancedPopup<TData>) are never on a GameObject — only their
                // concrete subclasses are, and those come through this same loop.
                if (type == null || type.IsAbstract || type.ContainsGenericParameters) continue;
                if (string.IsNullOrEmpty(type.FullName)) continue;
                names.Add(type.FullName);
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>
        /// Rebuilds <see cref="PopupOrderConfig.Order"/>: the authored order is kept as-is (minus blanks and duplicates),
        /// then popup types not in it yet are appended at the <b>back</b> — a new popup never jumps in front of what the
        /// user ordered. Entries whose type is gone (renamed / deleted class) are <b>kept</b>, so a temporary compile
        /// error or a rename in progress can't silently lose a slot; the panel marks them and offers removal. Returns
        /// true when anything changed — the caller saves then.
        /// </summary>
        internal static bool Reconcile(PopupOrderConfig config, List<string> discovered)
        {
            if (config == null)
                return false;
            config.Order ??= new List<PopupOrderConfig.Entry>();

            var ordered = new List<PopupOrderConfig.Entry>(config.Order.Count + (discovered?.Count ?? 0));
            var seen = new HashSet<string>();
            bool changed = false;

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || string.IsNullOrEmpty(entry.TypeName) || !seen.Add(entry.TypeName))
                {
                    changed = true; // a blank row or a duplicate left by a hand edit
                    continue;
                }
                ordered.Add(entry);
            }

            if (discovered != null)
            {
                for (int i = 0; i < discovered.Count; i++)
                {
                    string name = discovered[i];
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    ordered.Add(new PopupOrderConfig.Entry { TypeName = name, Layer = UnassignedLayer });
                    changed = true;
                }
            }

            config.Order = ordered;
            return changed;
        }

        /// <summary>
        /// Writes the layer tag of one popup type, keeping the entry's position. No-op when the type has no entry (it will
        /// get one, unassigned, on the panel's next reconcile) or the tag already matches. Returns true when it changed.
        /// </summary>
        internal static bool SetLayer(PopupOrderConfig config, string typeName, string layerName)
        {
            if (config?.Order == null || string.IsNullOrEmpty(typeName))
                return false;

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || entry.TypeName != typeName) continue;
                if (entry.Layer == layerName) return false;
                entry.Layer = layerName;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Re-tags every entry from the popup prefabs in the project — a full <c>t:Prefab</c> scan that loads each hit,
        /// like the Addressable index's menu item. Run by the Order panel when it opens and prefabs have changed since the
        /// last scan (see <see cref="IsScanNeeded"/>), never on routine asset churn. A type with no prefab keeps its
        /// current tag: a scene-authored popup is invisible to this scan and would otherwise lose the grouping the
        /// inspector gave it. Returns true when any tag changed.
        /// </summary>
        internal static bool ScanPrefabLayers(PopupOrderConfig config)
        {
            if (config?.Order == null)
                return false;

            bool changed = false;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    // Loading every prefab can take a moment in a big project — show it instead of freezing silently.
                    if ((i & 31) == 0 &&
                        EditorUtility.DisplayCancelableProgressBar("APS ▸ Order", "Reading popup layers…", (float)i / guids.Length))
                        break;

                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null || !go.TryGetComponent(out IAdvancedPopup popup)) continue;

                    changed |= SetLayer(config, popup.GetType().FullName, LayerNameOf(popup));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return changed;
        }

        #region Scan flag
        /// <summary>
        /// "A prefab changed since the last layer scan" — set by <see cref="PopupOrderPostprocessor"/> (string checks
        /// only, no loads) and consumed by the Order panel when it opens. <see cref="SessionState"/> so it survives domain
        /// reloads but resets with the editor session: the panel scans once per session and after real prefab changes,
        /// instead of on every recompile that happens to reopen the window.
        /// </summary>
        private const string ScanNeededKey = "APS_OrderScanNeeded";

        internal static void MarkScanNeeded() => SessionState.SetBool(ScanNeededKey, true);

        /// <summary> True when a scan is due — defaults to true, so the first open in an editor session always scans. </summary>
        internal static bool IsScanNeeded() => SessionState.GetBool(ScanNeededKey, true);

        internal static void ClearScanNeeded() => SessionState.SetBool(ScanNeededKey, false);
        #endregion

        /// <summary> The popup's layer as a tag: the enum member name, or <see cref="UnassignedLayer"/> for None. </summary>
        internal static string LayerNameOf(IAdvancedPopup popup)
        {
            if (popup == null || popup.PopupLayer == PopupLayerEnum.None)
                return UnassignedLayer;
            // Legacy multi-flag data resolves to the lowest bit — the same tie-break the runtime canvas routing uses.
            int mask = (int)popup.PopupLayer;
            return ((PopupLayerEnum)(mask & -mask)).ToString();
        }

        /// <summary>
        /// Flushes in-memory edits to disk and drops the runtime rank lookup built from the old order. Writes <b>only this
        /// asset</b> (<see cref="AssetDatabase.SaveAssetIfDirty"/>, not <c>SaveAssets</c>): the postprocessor and the popup
        /// inspector call this, and neither has any business flushing the user's other unsaved assets.
        /// </summary>
        internal static void Save(PopupOrderConfig config)
        {
            if (config == null)
                return;
            config.InvalidateRanks();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
