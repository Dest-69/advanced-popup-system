using System;
using UnityEditor;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Auto-syncs the Addressable popup index when a prefab changes, so toggling a popup's <c>Addressable</c> flag on
    /// its prefab updates the Addressables group + index asset with no manual step. Also regenerates when a scene
    /// asset is <b>moved/renamed or deleted</b>: the index bakes popup scene GUIDs to asset paths for runtime matching,
    /// so a changed scene path must be re-resolved (a plain scene save re-imports the <c>.unity</c> without changing its
    /// path, so it is ignored — no churn). Deferred via <see cref="EditorApplication.delayCall"/> to run outside the
    /// import callback; the generator is idempotent (no rewrite when nothing changed). Compiled only under APS_ADDRESSABLES.
    /// </summary>
    internal sealed class AddressablePopupPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (AddressablePopupIndexGenerator.IsGenerating) return;

            bool touchesPrefab = TouchesExt(imported, ".prefab") || TouchesExt(deleted, ".prefab") || TouchesExt(moved, ".prefab");
            // Scene rename/move/delete changes the path we bake from popup scene GUIDs — refresh so runtime matching stays valid.
            bool sceneMovedOrDeleted = TouchesExt(moved, ".unity") || TouchesExt(deleted, ".unity");
            if (!touchesPrefab && !sceneMovedOrDeleted) return;

            EditorApplication.delayCall -= DeferredRegenerate;
            EditorApplication.delayCall += DeferredRegenerate;
        }

        private static void DeferredRegenerate()
        {
            EditorApplication.delayCall -= DeferredRegenerate;
            AddressablePopupIndexGenerator.Regenerate();
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
