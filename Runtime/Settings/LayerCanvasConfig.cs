using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Per-layer canvas routing configured from the APS <b>Layers</b> tool. For every layer it holds a
    /// <see cref="Entry.SortingOrder"/> and a <see cref="Entry.CanvasPrefab"/> (kept set by the Layers panel, which
    /// seeds it with the consumer's default canvas — a cleared prefab still falls back to an auto-created overlay); at runtime
    /// <see cref="AdvancedPopupSystem.GetCanvasForLayer"/> uses this to give each layer its own canvas (the assigned
    /// prefab, or an auto-created default) at that sort order, so system-instantiated popups (Addressable loads /
    /// <see cref="AdvancedPopupSystem.SpawnAsync{T}"/>) of different layers land on independent canvases instead of the
    /// shared <see cref="AdvancedPopupSystem.Root"/>.
    /// <para>
    /// The single asset lives in the consumer's <c>Assets/Resources/APS_LayerCanvasConfig.asset</c> — <b>outside</b> the
    /// package, so a <c>.unitypackage</c> update never clobbers it (the same rule as <c>AP_Settings.json</c>). It is
    /// created and kept in sync with the layer list by the Layers panel; you normally edit it there, not by hand. A
    /// runtime <see cref="AdvancedPopupSystem.RegisterLayerCanvas"/> still overrides whatever this configures.
    /// </para>
    /// </summary>
    public class LayerCanvasConfig : ScriptableObject
    {
        /// <summary> Resources name of the consumer's config asset (loaded lazily by <see cref="Loaded"/>). </summary>
        public const string ResourceName = "APS_LayerCanvasConfig";

        /// <summary> Canvas routing for one layer, keyed by the layer's <see cref="PopupLayerEnum"/> name. </summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Layer name — must match a PopupLayerEnum member (managed by the Layers panel).")]
            public string Layer;

            [Tooltip("sortingOrder applied to this layer's canvas. Overrides the value baked into an assigned prefab. " +
                     "Also drives the display order of the Layers panel.")]
            public int SortingOrder;

            [Tooltip("Canvas prefab instantiated for this layer's popups. Managed by the Layers panel, which keeps it " +
                     "set (seeded with APS_DefaultCanvas). If it is ever cleared, the system auto-creates a plain " +
                     "overlay canvas at SortingOrder as a fallback.")]
            public Canvas CanvasPrefab;
        }

        [Tooltip("One entry per layer. Managed by the APS Layers panel.")]
        public List<Entry> Entries = new List<Entry>();

        /// <summary>
        /// The entry for a single <see cref="PopupLayerEnum"/> flag, or null when the layer has no configured entry.
        /// Expects <paramref name="flag"/> to be a single bit — callers split multi-flag masks before calling.
        /// </summary>
        public Entry GetEntry(PopupLayerEnum flag)
        {
            if (Entries == null) return null;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry e = Entries[i];
                if (e != null && TryParseLayer(e.Layer, out PopupLayerEnum f) && f == flag)
                    return e;
            }
            return null;
        }

        /// <summary>
        /// Parses a layer name to its single <see cref="PopupLayerEnum"/> flag. False for empty, unknown (stale entry
        /// whose layer was renamed/removed) or <c>None</c> names.
        /// </summary>
        public static bool TryParseLayer(string name, out PopupLayerEnum flag)
        {
            flag = PopupLayerEnum.None;
            if (string.IsNullOrEmpty(name)) return false;
            return Enum.TryParse(name, out flag) && flag != PopupLayerEnum.None;
        }

        #region Loaded
        private static LayerCanvasConfig _loaded;
        private static bool _loadedResolved;

        /// <summary>
        /// The consumer's config asset, loaded once from Resources ("<see cref="ResourceName"/>") and cached. Null when
        /// no asset exists yet (a fresh project before the Layers panel created it) — then every layer falls back to
        /// <see cref="AdvancedPopupSystem.Root"/>.
        /// </summary>
        public static LayerCanvasConfig Loaded
        {
            get
            {
                if (!_loadedResolved)
                {
                    _loaded = Resources.Load<LayerCanvasConfig>(ResourceName);
                    _loadedResolved = true;
                }
                return _loaded;
            }
        }

        /// <summary> Drops the cached asset so it is reloaded on next use. Called on play-mode exit (leak guard). </summary>
        internal static void ClearCache()
        {
            _loaded = null;
            _loadedResolved = false;
        }
        #endregion
    }
}
