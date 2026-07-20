using System;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Interactive features that can be enabled per popup via <see cref="IAdvancedPopup.Modules"/>.
    /// A bit-flag set — combine features freely. The central pointer system (PointerEventSystemAPS →
    /// <see cref="PopupInteractionSystem"/>) reads these flags each frame and drives the matching handler
    /// from <see cref="PopupFeatureRegistry"/>. Adding a built-in feature = add a flag here + register its handler.
    /// </summary>
    [Flags]
    public enum PopupFeatureEnum
    {
        /// <summary> No interactive features. </summary>
        None = 0,
        /// <summary> Popup can be moved by dragging its drag zone (see <see cref="DragConfig"/>). </summary>
        Draggable = 1 << 0,
        /// <summary> Popup can be resized by dragging grips (see <see cref="ResizeConfig"/>). </summary>
        Resizable = 1 << 1,
    }
}
