using System;
using System.Collections.Generic;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Keeps each popup type's <b>layer tag</b> in the order catalog fresh, so the APS <b>Order</b> panel can group popups
    /// by layer without ever scanning the project (see <see cref="PopupOrderConfigStore"/>). Incremental by design, in the
    /// shape <see cref="AddressablePopupPostprocessor"/> established: the import callback only records changed
    /// <c>.prefab</c> paths (string checks, no loads), and the deferred pass does one component lookup per changed prefab.
    /// <para>
    /// <b>Cold path creates nothing</b> — with no catalog asset in the project (the panel was never opened) this does
    /// nothing at all, and it saves only when a tag actually changed. The tag is grouping metadata: being stale or missing
    /// changes no runtime behaviour, which is why this may safely be a best-effort, prefab-only source (a scene-authored
    /// popup gets its tag from the inspector instead).
    /// </para>
    /// </summary>
    internal sealed class PopupOrderPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Above this many changed prefabs the pass is skipped entirely. Such a batch is not someone editing a popup —
        /// it is a VCS checkout, a Library rebuild, "Reimport All" or an imported asset package — and inspecting it would
        /// turn "one component lookup per changed prefab" into loading every prefab in the project. Tags are grouping
        /// metadata, so skipping costs nothing but a stale grouping until the next single save or the panel's
        /// <c>Rescan Layers</c> button (and the skip is logged, never silent).
        /// </summary>
        private const int BulkPrefabCap = 64;

        private static readonly HashSet<string> PendingPrefabPaths = new HashSet<string>();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Collect(imported);
            Collect(moved);
            if (PendingPrefabPaths.Count == 0) return;

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
                if (paths[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    PendingPrefabPaths.Add(paths[i]);
        }

        private static void DeferredSync()
        {
            EditorApplication.delayCall -= DeferredSync;

            var paths = new List<string>(PendingPrefabPaths);
            PendingPrefabPaths.Clear();

            // Cold path: no catalog yet → nothing to keep in sync, and we must not bring the asset into existence.
            // Checked before any prefab is loaded, so a project that never opened the panel pays only the string checks.
            PopupOrderConfig config = PopupOrderConfigStore.Load();
            if (config == null) return;

            if (paths.Count > BulkPrefabCap)
            {
                // Contents unknown (we deliberately don't open them), so assume popups were among them and let the
                // Order panel offer a scan next time it is opened.
                PopupOrderConfigStore.MarkScanNeeded();
                APLogger.Log($"<color=green>[APS Order]</color> {paths.Count} prefabs changed at once — skipping the incremental layer-tag refresh instead of loading them all; APS ▸ Order offers to re-read them when you open it.");
                return;
            }

            bool changed = false;
            for (int i = 0; i < paths.Count; i++)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (go == null || !go.TryGetComponent(out IAdvancedPopup popup)) continue;

                // createIfMissing: a popup type new to the catalog gets its entry (at the back) and its tag right here,
                // so no scan is ever "needed" just because a popup was added — saving its prefab is enough.
                changed |= PopupOrderConfigStore.SetLayer(config, popup.GetType().FullName,
                    PopupOrderConfigStore.LayerNameOf(popup), true);
            }

            if (changed)
                PopupOrderConfigStore.Save(config);
        }
    }
}
