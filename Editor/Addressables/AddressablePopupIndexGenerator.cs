using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Editor tooling for the Addressable popup index. Keeps popup prefabs flagged <c>Addressable</c> in a dedicated
    /// Addressables group and writes the <see cref="AddressablePopupIndexAsset"/> ScriptableObject — the catalog the
    /// runtime reads to load popups on demand. Because it writes an <b>asset</b> (not C#), flagging a popup Addressable
    /// never recompiles scripts or reloads the domain. Two entry points: <see cref="Regenerate"/> (menu; full project
    /// scan) and <see cref="SyncChanged"/> (auto via <see cref="AddressablePopupPostprocessor"/>; inspects only the
    /// changed prefabs, so routine asset churn costs nothing). Compiled only under <c>APS_ADDRESSABLES</c>.
    /// </summary>
    public static class AddressablePopupIndexGenerator
    {
        private const string GroupName = "Advanced Popup System";
        private const string ResourcesFolder = "Assets/Resources";
        private static string AssetPath => $"{ResourcesFolder}/{AddressablePopupIndexAsset.ResourceName}.asset";

        /// <summary> Guards against re-entrancy while the generator rewrites assets. </summary>
        internal static bool IsGenerating;

        /// <summary>
        /// Above this many changed prefabs in one batch, <see cref="SyncChanged"/> stops inspecting them one by one —
        /// see the comment at its bulk check for why and what the fallback is.
        /// </summary>
        private const int BulkPrefabCap = 64;

        /// <summary> Reused empty list so the bulk path skips the per-prefab loop without allocating. </summary>
        private static readonly List<string> EmptyPaths = new List<string>();

        /// <summary>
        /// Publishes <see cref="Regenerate"/> to the APS window's Settings tab through the
        /// <see cref="APSEditorTools"/> seam — the main editor assembly can't reference this optional one, so the
        /// integration registers itself (same shape as the runtime resolver). No separate top-level menu item: every APS
        /// tool lives in the APS window.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterEditorTool()
        {
            APSEditorTools.RegenerateAddressableIndex = Regenerate;
        }

        public static void Regenerate()
        {
            if (IsGenerating) return;
            IsGenerating = true;
            try
            {
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    APLogger.LogError("<color=red>[APS Addressables]</color> Could not obtain AddressableAssetSettings.");
                    return;
                }

                AddressableAssetGroup group = GetOrCreateGroup(settings);

                bool groupChanged = false;
                var entries = new List<PopupEntryData>();
                var keptGuids = new HashSet<string>();
                var seenTypeNames = new HashSet<string>();

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    var popup = go.GetComponent<IAdvancedPopup>();
                    if (popup == null || !popup.Addressable) continue;

                    string typeName = popup.GetType().FullName;
                    // The index is keyed by type full name — two Addressable prefabs of the same type would collide.
                    // Give each Addressable popup a distinct AdvancedPopup subclass; skip (and warn about) duplicates.
                    if (!seenTypeNames.Add(typeName))
                    {
                        APLogger.LogWarning($"<color=orange>[APS Addressables]</color> Duplicate Addressable popup type '{typeName}' at '{path}' — each Addressable popup needs a distinct AdvancedPopup subclass. Skipping this one.");
                        continue;
                    }

                    groupChanged |= EnsureEntry(settings, group, guid, typeName);
                    keptGuids.Add(guid);
                    entries.Add(BuildEntry(popup, typeName));
                }

                // Drop popups that were un-flagged (still in our group but no longer Addressable). Collect first —
                // RemoveAssetEntry mutates group.entries.
                var stale = new List<AddressableAssetEntry>();
                foreach (AddressableAssetEntry e in group.entries)
                    if (!keptGuids.Contains(e.guid))
                        stale.Add(e);
                foreach (AddressableAssetEntry e in stale)
                    settings.RemoveAssetEntry(e.guid, false);
                groupChanged |= stale.Count > 0;

                FlushChanges(settings, groupChanged, entries);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        /// <summary>
        /// Incremental auto-sync used by <see cref="AddressablePopupPostprocessor"/>. Inspects only the given changed
        /// prefabs (one component lookup each) instead of scanning the project, keeps their group entries in step, and
        /// re-bakes the index from the group's membership — the small set of Addressable popups — only when the catalog
        /// could actually differ. Unrelated prefab churn therefore triggers no Addressables/settings/index work at all,
        /// and nothing (settings, group, index asset) is created unless a popup flagged Addressable is really present.
        /// </summary>
        /// <param name="changedPrefabPaths">Paths of prefabs that were imported or moved.</param>
        /// <param name="prefabsDeleted">A prefab was deleted — the group may hold entries with dead GUIDs.</param>
        /// <param name="scenesMoved">A scene was moved/renamed or deleted — baked scene paths may be stale.</param>
        internal static void SyncChanged(List<string> changedPrefabPaths, bool prefabsDeleted, bool scenesMoved)
        {
            if (IsGenerating) return;
            IsGenerating = true;
            try
            {
                // Existing settings only — a random prefab import must not conjure Addressables into the project.
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
                AddressableAssetGroup group = settings != null ? settings.FindGroup(GroupName) : null;

                bool groupChanged = false;
                bool popupTouched = false; // an Addressable popup's own data may have changed → the index needs a re-bake

                // A batch this large is not someone editing a popup — it is a VCS checkout, a Library rebuild,
                // "Reimport All" or an imported asset package. Inspecting it would turn "one component lookup per changed
                // prefab" into loading every prefab in the project, so skip the per-prefab pass and only re-bake from the
                // group's own (small) membership: the index still matches what is grouped, and a popup whose Addressable
                // flag changed inside such a batch is picked up by its next save or the menu item's full rescan.
                bool bulk = changedPrefabPaths.Count > BulkPrefabCap;
                if (bulk && group != null)
                {
                    // Only worth saying when APS Addressables is actually in use — with no group there is nothing to skip.
                    APLogger.Log($"<color=green>[APS Addressables]</color> {changedPrefabPaths.Count} prefabs changed at once — skipping the per-prefab index sync instead of loading them all. Use APS ▸ Settings ▸ Regenerate Addressable Index if a popup's Addressable flag changed in that batch.");
                    popupTouched = group.entries.Count > 0;
                }

                foreach (string path in bulk ? EmptyPaths : changedPrefabPaths)
                {
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    IAdvancedPopup popup = go != null ? go.GetComponent<IAdvancedPopup>() : null;

                    if (popup != null && popup.Addressable)
                    {
                        // The first Addressable popup is the one moment settings/group may come into existence.
                        if (settings == null)
                        {
                            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                            if (settings == null)
                            {
                                APLogger.LogError("<color=red>[APS Addressables]</color> Could not obtain AddressableAssetSettings.");
                                return;
                            }
                        }
                        if (group == null)
                            group = GetOrCreateGroup(settings);

                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (string.IsNullOrEmpty(guid)) continue;

                        string typeName = popup.GetType().FullName;
                        if (HasDuplicateType(group, guid, typeName))
                        {
                            APLogger.LogWarning($"<color=orange>[APS Addressables]</color> Duplicate Addressable popup type '{typeName}' at '{path}' — each Addressable popup needs a distinct AdvancedPopup subclass. Skipping this one.");
                            continue;
                        }

                        groupChanged |= EnsureEntry(settings, group, guid, typeName);
                        popupTouched = true;
                    }
                    else if (group != null)
                    {
                        // Not a popup (anymore) or un-flagged → drop it from our group if it was there.
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        AddressableAssetEntry entry = string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid);
                        if (entry != null && entry.parentGroup == group)
                        {
                            settings.RemoveAssetEntry(guid, false);
                            groupChanged = true;
                        }
                    }
                }

                if (group == null) return; // no Addressable popups in play — nothing below can matter

                // A deleted prefab leaves an entry whose GUID no longer resolves to a path; prune those.
                if (prefabsDeleted)
                {
                    var dead = new List<AddressableAssetEntry>();
                    foreach (AddressableAssetEntry e in group.entries)
                        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(e.guid)))
                            dead.Add(e);
                    foreach (AddressableAssetEntry e in dead)
                        settings.RemoveAssetEntry(e.guid, false);
                    groupChanged |= dead.Count > 0;
                }

                if (groupChanged || popupTouched || (scenesMoved && group.entries.Count > 0))
                    FlushChanges(settings, groupChanged, CollectGroupEntries(group));
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group == null)
            {
                group = settings.CreateGroup(GroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }
            return group;
        }

        /// <summary>
        /// Puts <paramref name="guid"/> into the group under the type-name address, touching the settings only when
        /// the entry is missing, parked in another group, or mis-addressed. Returns whether anything actually moved —
        /// the callers use it to skip dirtying/saving the Addressables settings on a no-op pass.
        /// </summary>
        private static bool EnsureEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string guid, string typeName)
        {
            bool changed = false;
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null || entry.parentGroup != group)
            {
                // Deterministic address = type full name (survives prefab moves; unique per popup type).
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                changed = true;
            }
            if (entry.address != typeName)
            {
                entry.SetAddress(typeName, false);
                changed = true;
            }
            return changed;
        }

        /// <summary> True when another (still existing) prefab already owns this type-name address in the group. </summary>
        private static bool HasDuplicateType(AddressableAssetGroup group, string guid, string typeName)
        {
            foreach (AddressableAssetEntry e in group.entries)
                if (e.guid != guid && e.address == typeName && !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(e.guid)))
                    return true;
            return false;
        }

        /// <summary>
        /// Builds index entries from the group's current membership — the incremental path's source of truth. Loads
        /// only the few Addressable popup prefabs, never the whole project. Mirrors the full scan's duplicate-type
        /// skip so both paths converge on the same catalog.
        /// </summary>
        private static List<PopupEntryData> CollectGroupEntries(AddressableAssetGroup group)
        {
            var entries = new List<PopupEntryData>();
            var seenTypeNames = new HashSet<string>();
            foreach (AddressableAssetEntry e in group.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(e.guid);
                if (string.IsNullOrEmpty(path)) continue;

                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var popup = go != null ? go.GetComponent<IAdvancedPopup>() : null;
                if (popup == null || !popup.Addressable) continue;

                string typeName = popup.GetType().FullName;
                if (!seenTypeNames.Add(typeName)) continue;

                entries.Add(BuildEntry(popup, typeName));
            }
            return entries;
        }

        private static PopupEntryData BuildEntry(IAdvancedPopup popup, string typeName)
        {
            // Layers are canvas-bound — one layer per popup. Legacy multi-flag prefabs still bake (any-of matching,
            // canvas from the lowest bit), but the author should fix the prefab, so surface it at bake time.
            int layerMask = (int)popup.PopupLayer;
            if ((layerMask & (layerMask - 1)) != 0)
                Debug.LogWarning($"[APS] Addressable popup '{typeName}' carries several layers ({popup.PopupLayer}) — layers are canvas-bound, one layer per popup. Pick a single layer on the prefab.");
            return new PopupEntryData
            {
                TypeName = typeName,
                Address = typeName,
                Layer = popup.PopupLayer,
                LoadMode = popup.AddressableLoadMode,
                // Bake stable scene GUIDs → current asset paths; the runtime matches SceneManager scene.path.
                // Resolving here keeps the popup's stored identity reorder/rename-proof while the runtime stays
                // Addressables/AssetDatabase-free. Empty PreloadScenePaths = "every scene" (the Everyone default).
                PreloadScenePaths = GuidsToScenePaths(popup.PreloadSceneGuids),
                UnloadScenePaths = GuidsToScenePaths(popup.UnloadSceneGuids)
            };
        }

        /// <summary>
        /// Marks the Addressables settings dirty only when the group actually changed, then writes the index. A group
        /// change whose index text did not move still saves, so the group files persist to disk either way.
        /// </summary>
        private static void FlushChanges(AddressableAssetSettings settings, bool groupChanged, List<PopupEntryData> entries)
        {
            if (groupChanged)
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

            bool wroteIndex = WriteIndexAsset(entries);
            if (groupChanged && !wroteIndex)
                AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Writes the scanned popups into the <see cref="AddressablePopupIndexAsset"/> in the consumer's Resources
        /// (creating it on first run). Idempotent: rewrites (and reimports the asset) only when the catalog actually
        /// changed — writing an asset never triggers a script recompile or a domain reload. Returns whether it wrote.
        /// </summary>
        private static bool WriteIndexAsset(List<PopupEntryData> entries)
        {
            // Deterministic order so the full-scan and incremental paths never fight over entry order.
            entries.Sort((a, b) => string.CompareOrdinal(a.TypeName, b.TypeName));

            AddressablePopupIndexAsset asset = LoadOrCreateAsset();

            var built = new AddressablePopupIndexAsset.Entry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                PopupEntryData e = entries[i];
                built[i] = new AddressablePopupIndexAsset.Entry
                {
                    TypeName = e.TypeName,
                    Address = e.Address,
                    Layer = e.Layer,
                    LoadMode = e.LoadMode,
                    PreloadScenePaths = e.PreloadScenePaths,
                    UnloadScenePaths = e.UnloadScenePaths
                };
            }

            if (EntriesEqual(asset.Entries, built))
                return false;

            asset.Entries = built;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            APLogger.Log($"<color=green>[APS Addressables]</color> Regenerated popup index ({built.Length} popup(s)).");
            return true;
        }

        /// <summary>
        /// The index asset, creating an empty one in <c>Assets/Resources/</c> if none exists yet. Prefers the canonical
        /// path but also honors a relocated asset found anywhere in a Resources folder (mirrors <c>LayerCanvasConfig</c>).
        /// </summary>
        private static AddressablePopupIndexAsset LoadOrCreateAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AddressablePopupIndexAsset>(AssetPath);
            if (asset == null)
                asset = Resources.Load<AddressablePopupIndexAsset>(AddressablePopupIndexAsset.ResourceName); // relocated?
            if (asset != null)
                return asset;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            asset = ScriptableObject.CreateInstance<AddressablePopupIndexAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static bool EntriesEqual(AddressablePopupIndexAsset.Entry[] a, AddressablePopupIndexAsset.Entry[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                AddressablePopupIndexAsset.Entry x = a[i], y = b[i];
                if (x == null || x.TypeName != y.TypeName || x.Address != y.Address || x.Layer != y.Layer ||
                    x.LoadMode != y.LoadMode ||
                    !PathsEqual(x.PreloadScenePaths, y.PreloadScenePaths) ||
                    !PathsEqual(x.UnloadScenePaths, y.UnloadScenePaths))
                    return false;
            }
            return true;
        }

        private static bool PathsEqual(string[] a, string[] b)
        {
            a ??= System.Array.Empty<string>();
            b ??= System.Array.Empty<string>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private struct PopupEntryData
        {
            public string TypeName;
            public string Address;
            public PopupLayerEnum Layer;
            public LoadMode LoadMode;
            public string[] PreloadScenePaths;
            public string[] UnloadScenePaths;
        }

        /// <summary>
        /// Resolves stored scene GUIDs to their current asset paths (skipping any that no longer resolve), de-duplicated,
        /// preserving order. Baked into the index so the runtime matches <c>scene.path</c> without touching AssetDatabase.
        /// </summary>
        private static string[] GuidsToScenePaths(List<string> guids)
        {
            if (guids == null || guids.Count == 0) return System.Array.Empty<string>();
            var paths = new List<string>(guids.Count);
            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                    paths.Add(path);
            }
            return paths.ToArray();
        }
    }
}
