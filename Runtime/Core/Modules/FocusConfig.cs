using System;
using UnityEngine;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Per-popup focus settings. Shown in the inspector only when <see cref="PopupFeatureEnum.Focusable"/> is enabled.
    /// The pointer system raises the popup to the front of its order band when a press lands inside the zone
    /// (see AdvancedPopupSystem.BringToFront) — the press itself is never consumed, so buttons and drag still work.
    /// </summary>
    [Serializable]
    public class FocusConfig
    {
        [Tooltip("Area a press must land in to raise the popup (e.g. a title bar). If null, the whole popup rect raises it.")]
        public RectTransform FocusZone;
    }
}
