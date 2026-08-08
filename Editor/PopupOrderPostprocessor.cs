using System;
using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Core.Utils;
using UnityEditor;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps the order catalog's <b>prefab rows</b> fresh — a saved popup prefab joins it (already stamped with its
    /// identity, named and layer-tagged), a renamed or re-layered one is refreshed, and a prefab that stopped being a
    /// popup drops out — so the APS <b>Order</b> panel mirrors the project without ever scanning it (see
    /// <see cref="PopupOrderConfigStore"/>). Incremental by design, in the shape <see cref="AddressablePopupPostprocessor"/>
    /// established: the import callback only records changed <c>.prefab</c> paths (string checks, no loads), and the
    /// deferred pass does one asset load per changed prefab.
    /// <para>
    /// <b>Cold path creates nothing</b> — with no catalog asset in the project (the panel was never opened) this does
    /// nothing at all, and it saves only when something actually changed. Stamping a prefab re-imports it and so runs this
    /// once more; the second pass finds the stamp already correct and stops there.
    /// </para>
    /// <para>
    /// <b>Deletions clean up after themselves</b> — a row whose prefab is gone is dropped, so the catalog stays "the popup
    /// prefabs in this project" without anyone pressing a button. Guarded by the same bulk cap: a VCS checkout deleting
    /// half the project must not drop rows that are about to come back.
    /// </para>
    /// </summary>
    internal sealed class PopupOrderPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Above this many changed prefabs the pass is skipped entirely. Such a batch is not someone editing a popup —
        /// it is a VCS checkout, a Library rebuild, "Reimport All" or an imported asset package — and inspecting it would
        /// turn "one asset load per changed prefab" into loading every prefab in the project. Skipping costs nothing but
        /// a catalog that lags behind until the next single save or the panel's rescan, which it then offers by itself
        /// (and the skip is logged, never silent).
        /// </summary>
        private const int BulkPrefabCap = 64;

        private static readonly HashSet<string> PendingPrefabPaths = new HashSet<string>();
        // How many prefabs the batch deleted — the trigger for the (load-free) cleanup pass, and its bulk guard.
        private static int _deletedPrefabs;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Collect(imported);
            Collect(moved);
            _deletedPrefabs += Count(deleted);
            if (PendingPrefabPaths.Count == 0 && _deletedPrefabs == 0) return;

            // Note: the "scan needed" signal is NOT raised here. Any prefab import would look like a reason, including
            // ones APS itself writes during first-time setup (APS_DefaultCanvas.prefab) — which produced a second,
            // pointless scan right after the first. The deferred pass below already loads each changed prefab, so it
            // raises the flag only once it has seen an actual popup.
            EditorApplication.delayCall -= DeferredSync;
            EditorApplication.delayCall += DeferredSync;
        }

        private static void Collect(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
                if (IsPrefab(paths[i]))
                    PendingPrefabPaths.Add(paths[i]);
        }

        private static int Count(string[] paths)
        {
            int count = 0;
            for (int i = 0; i < paths.Length; i++)
                if (IsPrefab(paths[i])) count++;
            return count;
        }

        private static bool IsPrefab(string path) => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        private static void DeferredSync()
        {
            EditorApplication.delayCall -= DeferredSync;

            var paths = new List<string>(PendingPrefabPaths);
            PendingPrefabPaths.Clear();
            int deleted = _deletedPrefabs;
            _deletedPrefabs = 0;

            // Cold path: no catalog yet → nothing to keep in sync, and we must not bring the asset into existence.
            // Checked before any prefab is loaded, so a project that never opened the panel pays only the string checks.
            PopupOrderConfig config = PopupOrderConfigStore.Load();
            if (config == null) return;

            if (paths.Count > BulkPrefabCap || deleted > BulkPrefabCap)
            {
                // Contents unknown (we deliberately don't open them), so assume popups were among them and let the
                // Order panel re-read the project next time it is open.
                PopupOrderConfigStore.MarkScanNeeded();
                APLogger.Log($"<color=green>[APS Order]</color> {paths.Count + deleted} prefabs changed at once — skipping the incremental catalog refresh instead of loading them all; APS ▸ Order re-reads them when you open it.");
                return;
            }

            bool changed = false;
            for (int i = 0; i < paths.Count; i++)
            {
                // A popup prefab new to the catalog gets its row (at the back) right here, so no scan is ever "needed"
                // just because a popup was added — saving its prefab is enough.
                changed |= PopupOrderConfigStore.SyncPrefab(config, paths[i]);
            }

            // A deleted popup prefab takes its row with it, so nobody has to press a cleanup button. Costs a GUID lookup
            // per row and no asset load — and it is capped above, because a VCS checkout deleting half the project would
            // otherwise drop rows that are about to come back.
            if (deleted > 0)
                changed |= PopupOrderConfigStore.PruneMissing(config);

            if (changed)
                PopupOrderConfigStore.Save(config);
        }
    }
}
