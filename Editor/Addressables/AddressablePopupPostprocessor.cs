using System;
using UnityEditor;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Auto-syncs the Addressable popup index when a prefab changes, so toggling a popup's <c>Addressable</c> flag on
    /// its prefab updates the Addressables group + generated index with no manual step. Deferred via
    /// <see cref="EditorApplication.delayCall"/> to run outside the import callback; the generator is idempotent
    /// (no rewrite when nothing changed), so unrelated prefab edits stay cheap. Compiled only under APS_ADDRESSABLES.
    /// </summary>
    internal sealed class AddressablePopupPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (AddressablePopupIndexGenerator.IsGenerating) return;
            if (!TouchesPrefab(imported) && !TouchesPrefab(deleted) && !TouchesPrefab(moved)) return;

            EditorApplication.delayCall -= DeferredRegenerate;
            EditorApplication.delayCall += DeferredRegenerate;
        }

        private static void DeferredRegenerate()
        {
            EditorApplication.delayCall -= DeferredRegenerate;
            AddressablePopupIndexGenerator.Regenerate();
        }

        private static bool TouchesPrefab(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
                if (paths[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
