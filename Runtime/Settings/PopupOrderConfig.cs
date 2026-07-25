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
    /// Popup types are listed <b>front-most first</b> and keyed by type <see cref="System.Type.FullName"/> — the same
    /// string key the Addressable index uses, and for the same reason: user popups live in the consumer assembly, which
    /// references APS and not the other way round.
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
        /// One popup type's slot in the catalog. <see cref="Layer"/> is <b>editor grouping metadata only</b> — the layer
        /// the popup was last seen on, so the Order panel can show one layer at a time (popups only ever compete inside
        /// their layer's canvas). The runtime ignores it and ranks by position alone, which keeps the order a single total
        /// order — so even a canvas shared by two layers (<c>RegisterLayerCanvas</c> with a mask) sorts deterministically,
        /// and a stale tag can never change behaviour.
        /// </summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Popup type full name.")]
            public string TypeName;

            [Tooltip("Layer this popup was last seen on — grouping for the Order panel only; the runtime ignores it.")]
            public string Layer;
        }

        [Tooltip("Popup types, front-most first (index 0 is drawn above the rest of its canvas). " +
                 "Managed by the APS Order panel.")]
        public List<Entry> Order = new List<Entry>();

        /// <summary> Type name → index in <see cref="Order"/>, built on first query (see <see cref="GetRank"/>). </summary>
        private Dictionary<string, int> _ranks;

        /// <summary>
        /// The rank of a popup type: its index in <see cref="Order"/> (lower = closer to the viewer), or
        /// <see cref="UnrankedRank"/> when the catalog does not list it. Backed by a lazily built lookup, so the hot
        /// path (one call per canvas child per show) costs a dictionary hit, not a list scan.
        /// </summary>
        public int GetRank(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return UnrankedRank;

            if (_ranks == null)
            {
                _ranks = new Dictionary<string, int>(Order?.Count ?? 0);
                if (Order != null)
                {
                    for (int i = 0; i < Order.Count; i++)
                    {
                        string name = Order[i]?.TypeName;
                        // First occurrence wins — a duplicate left by a hand edit must not shadow the front-most slot.
                        if (!string.IsNullOrEmpty(name) && !_ranks.ContainsKey(name))
                            _ranks[name] = i;
                    }
                }
            }

            return _ranks.TryGetValue(typeName, out int rank) ? rank : UnrankedRank;
        }

        /// <summary>
        /// Drops the cached rank lookup so the next <see cref="GetRank"/> rebuilds it. Call after editing
        /// <see cref="Order"/> in code; the editor's Order panel calls it on save.
        /// </summary>
        public void InvalidateRanks()
        {
            _ranks = null;
        }

        private void OnValidate()
        {
            // A hand edit in the Inspector goes straight to the list — keep the lookup from serving stale ranks.
            _ranks = null;
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
