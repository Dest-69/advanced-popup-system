using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Draw order of popups <b>inside one canvas</b>, authored in the APS <b>Order</b> tool. Layers pick the canvas and
    /// its <c>sortingOrder</c> (see <see cref="LayerCanvasConfig"/>); this catalog decides who is in front among popups
    /// that share a canvas — the hierarchy sibling order APS applies on show / load / spawn (see
    /// <see cref="AdvancedPopupSystem.ApplyOrder"/>).
    /// <para>
    /// Entries are <b>popup prefabs</b>, listed front-most first and keyed by the prefab's <b>GUID</b> — the same GUID
    /// the popup itself carries in <see cref="AdvancedPS.Core.System.IAdvancedPopup.OrderKey"/>, which is what lets two
    /// prefabs of one class hold different slots. Each entry also records its popup type, and a popup with no key (one
    /// authored straight into a scene) falls back to the front-most entry of its type — so scene popups keep working
    /// with no prefab to key on.
    /// </para>
    /// <para>
    /// The single asset lives in the consumer's <c>Assets/Resources/APS_PopupOrderConfig.asset</c> — <b>outside</b> the
    /// package, so a <c>.unitypackage</c> update never clobbers it (the same rule as <c>AP_Settings.json</c>). It is
    /// created and kept in sync with the project's popup types by the Order panel; you normally edit it there, not by hand.
    /// </para>
    /// </summary>
    public class PopupOrderConfig : ScriptableObject
    {
        /// <summary> Resources name of the consumer's config asset (loaded lazily by <see cref="Loaded"/>). </summary>
        public const string ResourceName = "APS_PopupOrderConfig";

        /// <summary>
        /// Rank of anything the catalog does not list — a popup type absent from it, and any non-popup child of a canvas.
        /// Sorts <b>behind</b> every listed popup; several unranked popups keep show order among themselves.
        /// </summary>
        public const int UnrankedRank = int.MaxValue;

        /// <summary>
        /// One popup prefab's slot in the catalog. <see cref="Layer"/> and <see cref="PrefabName"/> are <b>editor
        /// metadata only</b> — read from the prefab when it was discovered, so the Order panel can group by layer and
        /// draw a row without loading anything. The runtime ignores both and ranks by position alone, which keeps the
        /// order a single total order: even a canvas shared by two layers (<c>RegisterLayerCanvas</c> with a mask) sorts
        /// deterministically, and a stale tag can never change behaviour.
        /// </summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("GUID of the popup prefab this entry ranks. Empty = a type-level entry (a popup authored in a " +
                     "scene, or a row carried over from a pre-prefab catalog).")]
            public string PrefabGuid;

            [Tooltip("Popup type full name — the fallback key for an instance with no prefab of its own.")]
            public string TypeName;

            [Tooltip("Layer of the prefab — grouping for the Order panel only; the runtime ignores it.")]
            public string Layer;

            [Tooltip("Prefab file name — what the Order panel draws, cached so listing costs no asset loads.")]
            public string PrefabName;

            /// <summary> True for a row that ranks a type rather than a concrete prefab (see <see cref="PrefabGuid"/>). </summary>
            public bool IsTypeOnly => string.IsNullOrEmpty(PrefabGuid);
        }

        [Tooltip("Popup prefabs, front-most first (index 0 is drawn above the rest of its canvas). " +
                 "Managed by the APS Order panel.")]
        public List<Entry> Order = new List<Entry>();

        /// <summary>
        /// Layout version of <see cref="Order"/>. <c>0</c> is the original per-<b>type</b> catalog; <c>1</c> keys entries
        /// by prefab GUID. The Order panel offers the one-time upgrade (a prefab scan that expands each type row into its
        /// prefabs, in place, so no authored position moves) while this is below <see cref="CurrentSchemaVersion"/>.
        /// </summary>
        [HideInInspector] public int SchemaVersion;

        /// <summary> Schema this build of APS writes — see <see cref="SchemaVersion"/>. </summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary> Prefab GUID → index in <see cref="Order"/>, built on first query (see <see cref="GetRank"/>). </summary>
        private Dictionary<string, int> _ranksByKey;
        /// <summary> Type name → index of the front-most entry of that type — the fallback for a keyless popup. </summary>
        private Dictionary<string, int> _ranksByType;

        /// <summary>
        /// The rank of one popup: the index of its prefab's entry in <see cref="Order"/> (lower = closer to the viewer),
        /// falling back to the front-most entry of <paramref name="typeName"/> when the popup carries no prefab key, and
        /// to <see cref="UnrankedRank"/> when neither is listed. Backed by lazily built lookups, so the hot path (one
        /// call per canvas child per show) costs a single dictionary hit in both normal cases — a keyed popup resolves
        /// on its key, a keyless one skips that probe entirely.
        /// </summary>
        /// <param name="orderKey">The popup's <see cref="AdvancedPS.Core.System.IAdvancedPopup.OrderKey"/> (may be empty).</param>
        /// <param name="typeName">The popup type's <see cref="System.Type.FullName"/>.</param>
        public int GetRank(string orderKey, string typeName)
        {
            EnsureRanks();

            if (!string.IsNullOrEmpty(orderKey) && _ranksByKey.TryGetValue(orderKey, out int keyed))
                return keyed;

            if (!string.IsNullOrEmpty(typeName) && _ranksByType.TryGetValue(typeName, out int typed))
                return typed;

            return UnrankedRank;
        }

        /// <summary>
        /// Rank by popup type alone — the front-most entry of that type. Kept for callers that have no prefab key at
        /// hand; prefer <see cref="GetRank(string,string)"/>, which honors per-prefab slots.
        /// </summary>
        public int GetRank(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return UnrankedRank;

            EnsureRanks();
            return _ranksByType.TryGetValue(typeName, out int rank) ? rank : UnrankedRank;
        }

        private void EnsureRanks()
        {
            if (_ranksByKey != null)
                return;

            int count = Order?.Count ?? 0;
            _ranksByKey = new Dictionary<string, int>(count);
            _ranksByType = new Dictionary<string, int>(count);
            if (Order == null)
                return;

            for (int i = 0; i < Order.Count; i++)
            {
                Entry entry = Order[i];
                if (entry == null) continue;

                // First occurrence wins in both maps — a duplicate left by a hand edit must not shadow the front-most
                // slot, and the type fallback is deliberately the front-most prefab of that type.
                if (!string.IsNullOrEmpty(entry.PrefabGuid) && !_ranksByKey.ContainsKey(entry.PrefabGuid))
                    _ranksByKey[entry.PrefabGuid] = i;
                if (!string.IsNullOrEmpty(entry.TypeName) && !_ranksByType.ContainsKey(entry.TypeName))
                    _ranksByType[entry.TypeName] = i;
            }
        }

        /// <summary>
        /// Drops the cached rank lookups so the next <see cref="GetRank(string,string)"/> rebuilds them. Call after
        /// editing <see cref="Order"/> in code; the editor's Order panel calls it on save.
        /// </summary>
        public void InvalidateRanks()
        {
            _ranksByKey = null;
            _ranksByType = null;
        }

        private void OnValidate()
        {
            // A hand edit in the Inspector goes straight to the list — keep the lookups from serving stale ranks.
            InvalidateRanks();
        }

        #region Loaded
        private static PopupOrderConfig _loaded;
        private static bool _loadedResolved;

        /// <summary>
        /// The consumer's config asset, loaded once from Resources ("<see cref="ResourceName"/>") and cached. Null when
        /// no asset exists yet (a project that never opened the Order panel) — then every popup is unranked and pure
        /// show order decides, which is the default behaviour.
        /// </summary>
        public static PopupOrderConfig Loaded
        {
            get
            {
                if (!_loadedResolved)
                {
                    _loaded = Resources.Load<PopupOrderConfig>(ResourceName);
                    _loadedResolved = true;
                }
                return _loaded;
            }
        }

        /// <summary> Drops the cached asset so it is reloaded on next use. Called on play-mode exit (leak guard). </summary>
        internal static void ClearCache()
        {
            _loaded?.InvalidateRanks();
            _loaded = null;
            _loadedResolved = false;
        }
        #endregion
    }
}
