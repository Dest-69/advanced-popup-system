using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps the consumer-side <c>PopupLayerEnum.generated.cs</c> (now under
    /// <c>Assets/AdvancedPopupSystem/Generated/Layers/</c>) in sync with the external layer store
    /// (<c>ProjectSettings/APS_Layers.json</c>) — see <see cref="LayerCatalog"/>.
    ///
    /// The enum lives in the consumer project (not the package), so a package update no longer touches it; on a fresh
    /// read-only install it is seeded by the dependency-free <c>AdvancedPS.Bootstrap</c> assembly (which runs even while
    /// core is still red). This postprocessor then reconciles the file with the store whenever it is (re)imported, and
    /// the <see cref="InitializeOnLoadMethod"/> pass is a secondary safety net after each domain reload.
    /// </summary>
    internal class LayerEnumSyncPostprocessor : AssetPostprocessor
    {
        private const string EnumRelativePath = "AdvancedPopupSystem/Generated/Layers/PopupLayerEnum.generated.cs";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (LayerCatalog.SuppressReconcile) return;

            foreach (string assetPath in importedAssets)
            {
                if (!assetPath.Replace('\\', '/').EndsWith(EnumRelativePath, StringComparison.Ordinal))
                    continue;

                string fsPath = AssetPathToFsPath(assetPath);
                // Inside a postprocessor: write only, do NOT re-import — the compile that follows this import batch
                // reads the on-disk content we just wrote, and skipping the import avoids postprocessor reentrancy.
                LayerCatalog.Reconcile(fsPath, allowImport: false);
                break;
            }
        }

        [InitializeOnLoadMethod]
        private static void SyncOnLoad()
        {
            // Deferred so the AssetDatabase has settled after the domain reload.
            EditorApplication.delayCall += () =>
            {
                if (!LayerCatalog.SuppressReconcile)
                    LayerCatalog.Reconcile();
            };
        }

        private static string AssetPathToFsPath(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets")) return null;
            return Path.Combine(Application.dataPath, p.Substring("Assets".Length).TrimStart('/')).Replace('\\', '/');
        }
    }
}
