using UnityEngine;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// A stateless driver for one interactive feature (drag, resize, …). Resolved by flag from
    /// <see cref="AdvancedPS.Core.PopupFeatureRegistry"/> and invoked by <see cref="AdvancedPS.Core.PopupInteractionSystem"/>.
    /// Keep implementations stateless — a single shared instance is reused across all popups, and per-gesture data
    /// lives in <see cref="GestureState"/>.
    /// </summary>
    public interface IPopupFeatureHandler
    {
        /// <summary>
        /// Try to grab <paramref name="popup"/> at the pointer. Return true and fill <paramref name="state"/> when a
        /// gesture starts; return false to let the next feature/popup try.
        /// </summary>
        /// <param name="popup"> Candidate popup (already known to be visible with this feature enabled). </param>
        /// <param name="pointerScreen"> Pointer position in screen space. </param>
        /// <param name="canvas"> Canvas the popup lives under. </param>
        /// <param name="camera"> UI camera (null for Screen Space - Overlay). </param>
        /// <param name="state"> Filled with the grab data when the method returns true. </param>
        bool TryBegin(IAdvancedPopup popup, Vector2 pointerScreen, Canvas canvas, Camera camera, out GestureState state);

        /// <summary> Advance the gesture with the current pointer position. </summary>
        void Update(Vector2 pointerScreen, ref GestureState state);

        /// <summary> Gesture finished (pointer released or cancelled). Optional cleanup. </summary>
        void End(ref GestureState state);
    }
}
