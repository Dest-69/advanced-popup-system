using UnityEngine;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// Optional companion to <see cref="IPopupFeatureHandler"/>: a handler that also wants to change the OS cursor when
    /// the pointer hovers one of its interactive zones implements this. It is a separate interface (not extra members on
    /// <see cref="IPopupFeatureHandler"/>) so existing custom handlers keep compiling — cursor feedback is purely
    /// opt-in. Kept stateless like the feature handler; a single shared instance is reused across popups.
    /// <see cref="AdvancedPS.Core.PopupInteractionSystem"/> queries it each idle frame for the topmost feature-popup.
    /// </summary>
    public interface IPopupCursorHandler
    {
        /// <summary>
        /// Report the cursor to show for <paramref name="popup"/> at the current pointer. Return true and fill
        /// <paramref name="cursor"/> when the pointer is over an interactive zone this handler owns; return false to
        /// leave the cursor to the next handler/popup (or the OS default).
        /// </summary>
        /// <param name="popup"> Candidate popup (already known to be visible with this feature enabled). </param>
        /// <param name="pointerScreen"> Pointer position in screen space. </param>
        /// <param name="camera"> UI camera (null for Screen Space - Overlay). </param>
        /// <param name="cursor"> Filled with the cursor to apply when the method returns true. </param>
        bool TryGetCursor(IAdvancedPopup popup, Vector2 pointerScreen, Camera camera, out PopupCursor cursor);
    }

    /// <summary>
    /// A cursor to hand to <see cref="Cursor.SetCursor"/>: the texture plus its hotspot (the click point, in pixels
    /// from the texture's top-left). A null <see cref="Texture"/> means "the OS default cursor".
    /// </summary>
    public readonly struct PopupCursor
    {
        /// <summary> Cursor texture (null = OS default). </summary>
        public readonly Texture2D Texture;
        /// <summary> Click point, in pixels from the texture's top-left corner. </summary>
        public readonly Vector2 Hotspot;

        public PopupCursor(Texture2D texture, Vector2 hotspot)
        {
            Texture = texture;
            Hotspot = hotspot;
        }
    }
}
