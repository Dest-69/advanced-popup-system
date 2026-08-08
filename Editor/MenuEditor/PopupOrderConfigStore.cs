using System;
using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Editor-side load / create / discover / save for the runtime <see cref="PopupOrderConfig"/> asset — the consumer's
    /// front-to-back order of popup <b>prefabs</b> inside a canvas. Like <see cref="LayerCanvasConfigStore"/> the asset
    /// lives in the consumer's <c>Assets/Resources/</c>, outside the package, so an update never clobbers it.
    /// <para>
    /// The catalog is discovered from prefabs, not from types: <see cref="SyncPrefab"/> (one changed prefab, driven by
    /// <see cref="PopupOrderPostprocessor"/>) and <see cref="ScanPrefabs"/> (the explicit full pass). Each discovered
    /// prefab is stamped with its own GUID in <see cref="IAdvancedPopup.OrderKey"/> — that stamp is what lets the runtime
    /// tell two prefabs of one class apart — and the entry caches the prefab's name and layer so the panel can draw a row
    /// without loading anything.
    /// </para>
    /// </summary>
    internal static class PopupOrderConfigStore
    {
        private const string ResourcesFolder = "Assets/Resources";
        private static string AssetPath => $"{ResourcesFolder}/{PopupOrderConfig.ResourceName}.asset";

        /// <summary> Layer name used for entries whose popup carries no layer (<see cref="PopupLayerEnum.None"/>). </summary>
        internal const string UnassignedLayer = "";

        /// <summary> Name of the popup's serialized identity field, stamped by <see cref="EnsureOrderKey"/>. </summary>
        private const string OrderKeyField = "_orderKey";

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
            // Born current: a fresh catalog has nothing to migrate, only prefabs to discover.
            config.SchemaVersion = PopupOrderConfig.CurrentSchemaVersion;
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();

            // An empty catalog knows no prefabs at all — that is the one moment a scan is genuinely due.
            MarkScanNeeded();
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
        /// True while the catalog still holds the pre-prefab (per-type) layout — the panel offers the one-time upgrade
        /// scan then. A version stamp, not "does a type row exist": type rows legitimately survive the upgrade for popups
        /// that live only in a scene, and asking about those forever would be a nag.
        /// </summary>
        internal static bool NeedsUpgrade(PopupOrderConfig config) =>
            config != null && config.SchemaVersion < PopupOrderConfig.CurrentSchemaVersion;

        #region Discovery

        /// <summary> One popup prefab as a scan saw it — everything an entry needs, with no second load. </summary>
        private readonly struct Found
        {
            public readonly string Guid;
            public readonly string Path;
            public readonly string TypeName;
            public readonly string Layer;
            public readonly string Name;

            public Found(string guid, string path, string typeName, string layer, string name)
            {
                Guid = guid;
                Path = path;
                TypeName = typeName;
                Layer = layer;
                Name = name;
            }
        }

        /// <summary>
        /// Re-reads every popup prefab in the project and rebuilds the catalog around them: known prefabs keep their
        /// authored position (their name / layer / type refreshed), a pre-prefab type row is <b>expanded in place</b> into
        /// that type's prefabs, and prefabs nobody listed yet are appended at the back — a new popup never jumps in front
        /// of an authored arrangement. Anything the scan did <b>not</b> find is dropped: the catalog is exactly "the popup
        /// prefabs in this project", kept that way by the tooling rather than by the user pressing cleanup buttons.
        /// <para>
        /// This is the expensive pass — it opens every prefab in the project — so it runs on a signal, not on every
        /// repaint. Cancelling changes nothing: a half-read project would look like "these prefabs no longer exist", and
        /// dropping is now the rule. Returns true when the catalog changed.
        /// </para>
        /// </summary>
        internal static bool ScanPrefabs(PopupOrderConfig config)
        {
            if (config == null)
                return false;
            config.Order ??= new List<PopupOrderConfig.Entry>();

            List<Found> found = CollectPopupPrefabs(out bool cancelled);
            return !cancelled && Rebuild(config, found);
        }

        /// <summary> Loads every prefab once, keeping the ones carrying a popup and stamping each with its own GUID. </summary>
        private static List<Found> CollectPopupPrefabs(out bool cancelled)
        {
            cancelled = false;
            var found = new List<Found>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    // Loading every prefab can take a moment in a big project — show it instead of freezing silently.
                    if ((i & 31) == 0 &&
                        EditorUtility.DisplayCancelableProgressBar("APS ▸ Order", "Reading popup prefabs…", (float)i / guids.Length))
                    {
                        cancelled = true;
                        break;
                    }

                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null || !go.TryGetComponent(out IAdvancedPopup popup)) continue;

                    EnsureOrderKey(go, popup, guids[i], path);
                    found.Add(new Found(guids[i], path, popup.GetType().FullName, LayerNameOf(popup), go.name));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // By path, so the order new prefabs join the catalog in never depends on the asset database's iteration order.
            found.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return found;
        }

        /// <summary> Merges a full scan's result into the catalog, preserving every authored position. </summary>
        private static bool Rebuild(PopupOrderConfig config, List<Found> found)
        {
            var byGuid = new Dictionary<string, Found>(found.Count);
            var byType = new Dictionary<string, List<Found>>();
            for (int i = 0; i < found.Count; i++)
            {
                Found f = found[i];
                byGuid[f.Guid] = f;
                if (string.IsNullOrEmpty(f.TypeName)) continue;
                if (!byType.TryGetValue(f.TypeName, out List<Found> list))
                    byType[f.TypeName] = list = new List<Found>();
                list.Add(f);
            }

            var ordered = new List<PopupOrderConfig.Entry>(config.Order.Count + found.Count);
            var placed = new HashSet<string>();
            bool changed = false;

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || (string.IsNullOrEmpty(entry.PrefabGuid) && string.IsNullOrEmpty(entry.TypeName)))
                {
                    changed = true; // a blank row left by a hand edit
                    continue;
                }

                if (!entry.IsTypeOnly)
                {
                    // Dropped when the scan didn't find it: the prefab is gone, moved out of the project, or no longer
                    // carries a popup. Also drops a duplicate left by a hand edit (placed.Add fails the second time).
                    if (!placed.Add(entry.PrefabGuid) || !byGuid.TryGetValue(entry.PrefabGuid, out Found live))
                    {
                        changed = true;
                        continue;
                    }
                    changed |= Apply(entry, live);
                    ordered.Add(entry);
                    continue;
                }

                // A type row: the pre-prefab layout, or a popup that lives only in a scene. Where the project has
                // prefabs of that type, they take over this exact slot — that is the whole migration, and it moves
                // nothing the user arranged. Where it has none, the row goes: a class with no prefab is exactly the
                // phantom this rework removed, and the runtime still ranks a scene-only popup by its class fallback.
                changed = true;
                if (!byType.TryGetValue(entry.TypeName, out List<Found> prefabs))
                    continue;

                for (int p = 0; p < prefabs.Count; p++)
                {
                    Found f = prefabs[p];
                    if (!placed.Add(f.Guid)) continue;
                    ordered.Add(NewEntry(f));
                }
            }

            for (int i = 0; i < found.Count; i++)
            {
                Found f = found[i];
                if (!placed.Add(f.Guid)) continue;
                ordered.Add(NewEntry(f));
                changed = true;
            }

            config.Order = ordered;

            if (config.SchemaVersion != PopupOrderConfig.CurrentSchemaVersion)
            {
                config.SchemaVersion = PopupOrderConfig.CurrentSchemaVersion;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Brings one changed prefab into the catalog — the incremental counterpart of <see cref="ScanPrefabs"/>, one
        /// asset load and no project walk. A prefab that stopped being a popup drops out; a brand-new one is appended at
        /// the back; a prefab whose type still sits in the catalog as a pre-prefab type row <b>takes that row over</b>, so
        /// an authored slot survives the upgrade even without a full scan. Returns true when the catalog changed.
        /// </summary>
        internal static bool SyncPrefab(PopupOrderConfig config, string assetPath)
        {
            if (config?.Order == null || string.IsNullOrEmpty(assetPath))
                return false;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return false;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null)
                return false; // deleted — PruneMissing / the panel handle that

            if (!go.TryGetComponent(out IAdvancedPopup popup))
                return RemoveByGuid(config, guid);

            EnsureOrderKey(go, popup, guid, assetPath);
            var live = new Found(guid, assetPath, popup.GetType().FullName, LayerNameOf(popup), go.name);

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || entry.PrefabGuid != guid) continue;
                return Apply(entry, live);
            }

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || !entry.IsTypeOnly || entry.TypeName != live.TypeName) continue;
                Apply(entry, live);
                return true;
            }

            config.Order.Add(NewEntry(live));
            return true;
        }

        private static PopupOrderConfig.Entry NewEntry(Found f) => new PopupOrderConfig.Entry
        {
            PrefabGuid = f.Guid,
            TypeName = f.TypeName,
            Layer = f.Layer,
            PrefabName = f.Name
        };

        /// <summary> Refreshes an entry's cached prefab metadata. True when anything actually differed. </summary>
        private static bool Apply(PopupOrderConfig.Entry entry, Found f)
        {
            if (entry.PrefabGuid == f.Guid && entry.TypeName == f.TypeName &&
                entry.Layer == f.Layer && entry.PrefabName == f.Name)
                return false;

            entry.PrefabGuid = f.Guid;
            entry.TypeName = f.TypeName;
            entry.Layer = f.Layer;
            entry.PrefabName = f.Name;
            return true;
        }

        /// <summary>
        /// Stamps the popup with its own prefab's GUID, the identity the runtime ranks on. Written once per prefab and
        /// only when it differs, so re-imports converge instead of looping: a <b>prefab variant</b> inherits its base's
        /// stamp, which is exactly the mismatch this overwrites, and that is what keeps a variant orderable on its own.
        /// Skipped for immutable assets (a popup shipped inside a package) — such a popup simply falls back to its type's
        /// slot.
        /// </summary>
        private static void EnsureOrderKey(GameObject prefabRoot, IAdvancedPopup popup, string guid, string assetPath)
        {
            if (popup == null || popup.OrderKey == guid)
                return;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return;

            var serialized = new SerializedObject(popup);
            SerializedProperty property = serialized.FindProperty(OrderKeyField);
            if (property == null)
                return;

            property.stringValue = guid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabRoot);
            AssetDatabase.SaveAssetIfDirty(prefabRoot);
        }

        #endregion

        #region Edits

        /// <summary>
        /// Writes the layer tag of the entry belonging to <paramref name="popup"/> — by prefab key when it has one, else
        /// by type. The popup inspector calls this so changing a layer regroups the row without waiting for a prefab
        /// save. It never creates a row: the catalog is discovered from prefabs, and a row the discovery didn't make
        /// would only be dropped again. Returns true when something changed.
        /// </summary>
        internal static bool SetLayer(PopupOrderConfig config, IAdvancedPopup popup, string layerName)
        {
            if (config?.Order == null || popup == null)
                return false;

            string key = popup.OrderKey;
            string typeName = popup.GetType().FullName;

            if (!string.IsNullOrEmpty(key))
            {
                for (int i = 0; i < config.Order.Count; i++)
                {
                    PopupOrderConfig.Entry entry = config.Order[i];
                    if (entry == null || entry.PrefabGuid != key) continue;
                    if (entry.Layer == layerName) return false;
                    entry.Layer = layerName;
                    return true;
                }
            }

            for (int i = 0; i < config.Order.Count; i++)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || !entry.IsTypeOnly || entry.TypeName != typeName) continue;
                if (entry.Layer == layerName) return false;
                entry.Layer = layerName;
                return true;
            }

            return false;
        }

        /// <summary> Drops the entry of a prefab that is no longer a popup. </summary>
        private static bool RemoveByGuid(PopupOrderConfig config, string guid)
        {
            for (int i = config.Order.Count - 1; i >= 0; i--)
            {
                PopupOrderConfig.Entry entry = config.Order[i];
                if (entry == null || entry.PrefabGuid != guid) continue;
                config.Order.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when an entry no longer points at a prefab in the project — a deleted asset, or a type row left over from
        /// the pre-prefab catalog. Costs two AssetDatabase lookups and <b>no asset load</b>; the panel resolves every row
        /// through it once per rebuild, and <see cref="PruneMissing"/> uses it to clean up after a deletion.
        /// <para>
        /// The existence test is <see cref="AssetDatabase.GetMainAssetTypeAtPath"/>, not "the GUID resolves to a path":
        /// Unity keeps serving a deleted asset's last known path from <see cref="AssetDatabase.GUIDToAssetPath"/> (and
        /// even round-trips it back through <c>AssetPathToGUID</c>), so a path alone proves nothing. Verified against a
        /// just-deleted prefab.
        /// </para>
        /// </summary>
        internal static bool IsMissing(PopupOrderConfig.Entry entry)
        {
            if (entry == null || entry.IsTypeOnly)
                return true;

            string path = AssetDatabase.GUIDToAssetPath(entry.PrefabGuid);
            return string.IsNullOrEmpty(path) || AssetDatabase.GetMainAssetTypeAtPath(path) == null;
        }

        /// <summary>
        /// Drops every row whose prefab is no longer in the project. Costs a GUID lookup per row and no asset load, so
        /// the postprocessor can run it whenever a popup prefab is deleted — the catalog cleans itself instead of showing
        /// dead rows with a Remove button.
        /// </summary>
        internal static bool PruneMissing(PopupOrderConfig config)
        {
            if (config?.Order == null)
                return false;

            bool changed = false;
            for (int i = config.Order.Count - 1; i >= 0; i--)
            {
                if (!IsMissing(config.Order[i])) continue;
                config.Order.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        #endregion

        #region Scan flag
        /// <summary>
        /// "Popup prefabs changed since the last scan" — set by <see cref="PopupOrderPostprocessor"/> when it cannot
        /// inspect a batch itself, by <see cref="LoadOrCreate"/> when the catalog is first created, and by the panel when
        /// it finds a pre-prefab catalog to upgrade. Consumed by the Order panel, which runs the scan.
        /// <para>
        /// Defaults to <b>false</b>: a scan happens only on a real signal. Defaulting to true meant every first open of a
        /// session scanned for nothing — and during first-time setup the Layers panel creating <c>APS_DefaultCanvas.prefab</c>
        /// looked like "a prefab changed", so a second scan followed on the next open.
        /// </para>
        /// <see cref="SessionState"/> keeps it across domain reloads but resets it with the editor session, so a signal
        /// can't outlive the run that produced it.
        /// </summary>
        private const string ScanNeededKey = "APS_OrderScanNeeded";

        internal static void MarkScanNeeded() => SessionState.SetBool(ScanNeededKey, true);

        internal static bool IsScanNeeded() => SessionState.GetBool(ScanNeededKey, false);

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
