using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps the shipped <c>PopupLayerEnum.generated.cs</c> in sync with the external layer store
    /// (<c>ProjectSettings/APS_Layers.json</c>) so updating the package never wipes a consumer's custom layers
    /// (see <see cref="LayerCatalog"/>).
    ///
    /// The load-bearing hook is <see cref="OnPostprocessAllAssets"/>: it runs on the <i>currently loaded</i> editor
    /// assemblies <b>before</b> the freshly imported scripts recompile. When a package update overwrites the enum
    /// with the shipped default, this restores the consumer's layers from the store first — so their own code
    /// (which references <c>PopupLayerEnum.SHOP</c> etc.) still compiles instead of breaking. The
    /// <see cref="InitializeOnLoadMethod"/> pass is a secondary safety net after each domain reload.
    /// </summary>
    internal class LayerEnumSyncPostprocessor : AssetPostprocessor
    {
        private const string EnumRelativePath = "Runtime/Generated/PopupLayerEnum.generated.cs";

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
