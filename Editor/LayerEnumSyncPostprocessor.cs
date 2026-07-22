using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps the shipped <c>PopupLayerEnum.generated.cs</c> (in the package, <c>Runtime/Generated/Layers/</c>) in sync
    /// with the external layer store (<c>ProjectSettings/APS_Layers.json</c>) — see <see cref="LayerCatalog"/>.
    ///
    /// When a package update overwrites the enum with the shipped default, this restores the consumer's layers from the
    /// store before their scripts recompile (<see cref="OnPostprocessAllAssets"/> runs on the already-loaded editor
    /// assembly). The <see cref="InitializeOnLoadMethod"/> pass is a secondary safety net after each domain reload. Both
    /// require the package to be writable (embedded / imported into <c>Assets</c>); on a read-only UPM install the
    /// shipped default stands — customizing layers there needs a writable install.
    /// </summary>
    internal class LayerEnumSyncPostprocessor : AssetPostprocessor
    {
        private const string EnumRelativePath = "Runtime/Generated/Layers/PopupLayerEnum.generated.cs";

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
