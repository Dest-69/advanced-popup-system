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
    /// Editor tooling for the Addressable popup index. Scans popup prefabs flagged <c>Addressable</c>, keeps them in a
    /// dedicated Addressables group, and writes the <see cref="AddressablePopupIndexAsset"/> ScriptableObject — the
    /// catalog the runtime reads to load popups on demand. Because it writes an <b>asset</b> (not C#), flagging a popup
    /// Addressable no longer recompiles scripts or reloads the domain. Compiled only under <c>APS_ADDRESSABLES</c>.
    /// Triggered from the menu and automatically when a popup prefab changes (see <see cref="AddressablePopupPostprocessor"/>).
    /// </summary>
    public static class AddressablePopupIndexGenerator
    {
        private const string GroupName = "Advanced Popup System";
        private const string ResourcesFolder = "Assets/Resources";
        private static string AssetPath => $"{ResourcesFolder}/{AddressablePopupIndexAsset.ResourceName}.asset";

        /// <summary> Guards against re-entrancy while the generator rewrites assets. </summary>
        internal static bool IsGenerating;

        [MenuItem("Tools/Advanced Popup System/Regenerate Addressable Index")]
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
                    // Deterministic address = type full name (survives prefab moves; unique per popup type).
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.SetAddress(typeName, false);
                    keptGuids.Add(guid);

                    entries.Add(new PopupEntryData
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
                    });
                }

                // Drop popups that were un-flagged (still in our group but no longer Addressable). Collect first —
                // RemoveAssetEntry mutates group.entries.
                var stale = new List<AddressableAssetEntry>();
                foreach (AddressableAssetEntry e in group.entries)
                    if (!keptGuids.Contains(e.guid))
                        stale.Add(e);
                foreach (AddressableAssetEntry e in stale)
                    settings.RemoveAssetEntry(e.guid, false);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

                WriteIndexAsset(entries);
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
        /// Writes the scanned popups into the <see cref="AddressablePopupIndexAsset"/> in the consumer's Resources
        /// (creating it on first run). Idempotent: rewrites (and reimports the asset) only when the catalog actually
        /// changed — writing an asset never triggers a script recompile or a domain reload.
        /// </summary>
        private static void WriteIndexAsset(List<PopupEntryData> entries)
        {
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
                return;

            asset.Entries = built;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            APLogger.Log($"<color=green>[APS Addressables]</color> Regenerated popup index ({built.Length} popup(s)).");
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
