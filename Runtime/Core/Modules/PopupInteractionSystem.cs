using AdvancedPS.Core.System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdvancedPS.Core
{
    /// <summary>
    /// Central, backend-agnostic gesture coordinator. Fed each frame by PointerEventSystemAPS (New/Old input) through
    /// <see cref="Tick"/>; runs at most one gesture at a time. Holds no per-scene collections — only the single
    /// in-flight gesture, reset on play-mode exit. Hit-testing happens only on the press edge; while idle a frame costs
    /// one bool check, and an active gesture is one handler call — zero per-frame allocations.
    /// </summary>
    public static class PopupInteractionSystem
    {
        private static bool _active;
        private static bool _wasPressed;
        private static IPopupFeatureHandler _handler;
        private static GestureState _state;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorInit()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Reset();
        }
#endif

        /// <summary> Aborts any in-flight gesture and clears state. Called on play-mode exit (leak guard). </summary>
        public static void Reset()
        {
            _active = false;
            _wasPressed = false;
            _handler = null;
            _state = default;
        }

        /// <summary>
        /// Drive interactions for this frame.
        /// </summary>
        /// <param name="pointerScreen"> Pointer position in screen space. </param>
        /// <param name="isPressed"> Whether the primary pointer button / touch is currently held. </param>
        public static void Tick(Vector2 pointerScreen, bool isPressed)
        {
            bool pressedThisFrame = isPressed && !_wasPressed;
            _wasPressed = isPressed;

            if (_active)
            {
                IAdvancedPopup popup = _state.Popup;
                // End the gesture if released, or if the popup was hidden/destroyed mid-drag.
                if (!isPressed || popup == null || _state.Rect == null || !popup.IsVisible)
                {
                    EndGesture();
                    return;
                }

                _handler.Update(pointerScreen, ref _state);
                return;
            }

            if (pressedThisFrame)
                TryBeginGesture(pointerScreen);
        }

        private static void TryBeginGesture(Vector2 pointerScreen)
        {
            var popups = AdvancedPopupSystem.ActivePopups;
            var entries = PopupFeatureRegistry.Entries;

            // Most-recently-shown first: ActivePopups is a recency stack (append on show), so the last entry is the
            // top of the visual/escape stack — the popup a user expects to grab.
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = popups[i];
                if (popup == null || !popup.IsVisible) continue;

                PopupModules modules = popup.Modules;
                if (modules == null || !modules.HasAny) continue;

                RectTransform root = popup.RootTransform;
                if (root == null) continue;
                Canvas canvas = root.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

                for (int e = 0; e < entries.Count; e++)
                {
                    PopupFeatureRegistry.Entry entry = entries[e];
                    if ((modules.Features & entry.Flag) == 0) continue;

                    if (entry.Handler.TryBegin(popup, pointerScreen, canvas, camera, out _state))
                    {
                        _handler = entry.Handler;
                        _active = true;
                        return;
                    }
                }
            }
        }

        private static void EndGesture()
        {
            if (_active && _handler != null)
                _handler.End(ref _state);

            _active = false;
            _handler = null;
            _state = default;
        }
    }
}
