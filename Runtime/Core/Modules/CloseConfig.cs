using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Per-popup close settings. Shown in the inspector only when <see cref="PopupFeatureEnum.Closable"/> is enabled.
    /// The referenced button is wired to hide the popup while it is shown and unwired on hide by the base popup
    /// (see AdvancedPopup.Subscribe / OnCloseButtonPress).
    /// </summary>
    [Serializable]
    public class CloseConfig
    {
        [Tooltip("Button that hides the popup when clicked. Wired while the popup is shown, unwired on hide. Can be null.")]
        public Button CloseButton;
    }
}
