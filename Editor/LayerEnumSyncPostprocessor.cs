using System;
using AdvancedPS.Core.Utils;
using UnityEditor;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps the shipped <c>PopupLayerEnum.generated.cs</c> (in the package, <c>Runtime/Generated/Layers/</c>) in sync
    /// with the external layer store (<c>ProjectSettings/APS_Layers.json</c>) — see <see cref="LayerCatalog"/>.
    ///
    /// When a package update overwrites the enum with the shipped default, this restores the consumer's layers from the
    /// store before their scripts recompile (<see cref="OnPostprocessAllAssets"/> runs on the already-loaded editor
    /// assembly). The <see cref="InitializeOnLoadMethod"/> pass is a secondary safety net after each domain reload. Both
    /// need the enum file to be writable on disk; where it is not, the shipped default stands and customizing layers
    /// needs an embedded install.
    /// <para>
    /// Path resolution goes through <see cref="FileSearcher.ToFsPath"/>, which handles <c>Packages/…</c> as well as
    /// <c>Assets/…</c>. It used to be a local helper that returned null for anything outside <c>Assets</c> — so on a UPM
    /// install (where the enum lives under <c>Packages/</c>) this pass silently did nothing, which is precisely the case
    /// it exists for. It only ever ran in a project with APS copied into <c>Assets/</c>. Found 2026-08-08 by a consumer
    /// whose layers vanished from the enum after an update.
    /// </para>
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

                string fsPath;
                try { fsPath = FileSearcher.ToFsPath(assetPath); }
                catch { break; } // not under Assets or the package — nothing this pass can heal

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
    }
}
