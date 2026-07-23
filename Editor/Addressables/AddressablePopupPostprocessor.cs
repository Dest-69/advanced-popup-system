using System;
using System.Collections.Generic;
using UnityEditor;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Auto-syncs the Addressable popup index when popup prefabs change, so toggling a popup's <c>Addressable</c> flag
    /// on its prefab updates the Addressables group + index asset with no manual step. Incremental by design: the
    /// import callback only records the changed paths (string checks, no loads), and the deferred
    /// <see cref="AddressablePopupIndexGenerator.SyncChanged"/> pass inspects just those prefabs — an unrelated prefab
    /// save or a project open costs one component lookup per changed prefab and touches no Addressables state. The
    /// full project rescan lives behind the menu item only (<see cref="AddressablePopupIndexGenerator.Regenerate"/>).
    /// Scene assets matter only when <b>moved/renamed or deleted</b>: the index bakes popup scene GUIDs to asset paths
    /// for runtime matching, so a changed scene path must be re-resolved (a plain scene save re-imports the
    /// <c>.unity</c> without changing its path, so it is ignored — no churn). Deferred via
    /// <see cref="EditorApplication.delayCall"/> to run outside the import callback; the generator is idempotent
    /// (no rewrite when nothing changed). Compiled only under APS_ADDRESSABLES.
    /// </summary>
    internal sealed class AddressablePopupPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingPrefabPaths = new HashSet<string>();
        private static bool _prunePending;  // a prefab was deleted → the group may hold entries with dead GUIDs
        private static bool _rebakePending; // a scene moved/was deleted → baked scene paths may be stale

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (AddressablePopupIndexGenerator.IsGenerating) return;

            CollectPrefabs(imported);
            CollectPrefabs(moved);
            _prunePending |= TouchesExt(deleted, ".prefab");
            // Scene rename/move/delete changes the path we bake from popup scene GUIDs — refresh so runtime matching stays valid.
            _rebakePending |= TouchesExt(moved, ".unity") || TouchesExt(deleted, ".unity");

            if (PendingPrefabPaths.Count == 0 && !_prunePending && !_rebakePending) return;

            EditorApplication.delayCall -= DeferredSync;
            EditorApplication.delayCall += DeferredSync;
        }

        private static void DeferredSync()
        {
            EditorApplication.delayCall -= DeferredSync;

            var paths = new List<string>(PendingPrefabPaths);
            bool prune = _prunePending;
            bool rebake = _rebakePending;
            PendingPrefabPaths.Clear();
            _prunePending = _rebakePending = false;

            AddressablePopupIndexGenerator.SyncChanged(paths, prune, rebake);
        }

        private static void CollectPrefabs(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
                if (paths[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    PendingPrefabPaths.Add(paths[i]);
        }

        private static bool TouchesExt(string[] paths, string ext)
        {
            for (int i = 0; i < paths.Length; i++)
                if (paths[i].EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
