using System;
using AdvancedPS.Core.System;
using UnityEngine.UI;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Data-driven interactive features for a popup: a bit-flag set of enabled features plus their per-feature config.
    /// Lives as a single field on <see cref="IAdvancedPopup"/> (Modules); the central PointerEventSystemAPS reads it —
    /// no per-feature MonoBehaviours are added at runtime. The inspector reveals a config block only for the features
    /// that are enabled.
    /// </summary>
    [Serializable]
    public class PopupModules
    {
        /// <summary> Enabled interactive features (bit flags). </summary>
        public PopupFeatureEnum Features = PopupFeatureEnum.None;

        /// <summary> Drag settings — used when <see cref="PopupFeatureEnum.Draggable"/> is set. </summary>
        public DragConfig Drag = new DragConfig();

        /// <summary> Resize settings — used when <see cref="PopupFeatureEnum.Resizable"/> is set. </summary>
        public ResizeConfig Resize = new ResizeConfig();

        /// <summary> Close settings — used when <see cref="PopupFeatureEnum.Closable"/> is set. </summary>
        public CloseConfig Close = new CloseConfig();

        /// <summary>
        /// Focus (raise-on-press) settings — used when <see cref="PopupFeatureEnum.Focusable"/> is set. Read directly by
        /// the pointer system after it checks the flag; there is no flag-gated accessor like <see cref="CloseButton"/>
        /// because a null <see cref="FocusConfig.FocusZone"/> already means "the whole popup rect".
        /// </summary>
        public FocusConfig Focus = new FocusConfig();

        /// <summary> True if any interactive feature is enabled. </summary>
        public bool HasAny => Features != PopupFeatureEnum.None;

        /// <summary>
        /// The active close button: the configured <see cref="CloseConfig.CloseButton"/> when
        /// <see cref="PopupFeatureEnum.Closable"/> is enabled, otherwise null. The base popup wires this on show and
        /// unwires it on hide (see AdvancedPopup.Subscribe / Unsubscribe).
        /// </summary>
        public Button CloseButton => (Features & PopupFeatureEnum.Closable) != 0 ? Close.CloseButton : null;
    }
}
