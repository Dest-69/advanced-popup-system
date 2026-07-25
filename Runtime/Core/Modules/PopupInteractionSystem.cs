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
    /// in-flight gesture, reset on play-mode exit. On the press edge it raises the pressed
    /// <see cref="PopupFeatureEnum.Focusable"/> popup (a non-consuming pass) and then hit-tests to start a gesture;
    /// while idle it runs a light hover pass so the OS cursor reflects the resize zone under the pointer (see
    /// <see cref="IPopupCursorHandler"/>).
    /// </summary>
    public static class PopupInteractionSystem
    {
        /// <summary> Master switch for the hover-cursor feedback (the drag/resize gestures themselves are unaffected). </summary>
        public static bool CursorFeedbackEnabled = true;

        private static bool _active;
        private static bool _wasPressed;
        private static IPopupFeatureHandler _handler;
        private static GestureState _state;

        // Currently applied hover cursor, so we only call Cursor.SetCursor when it actually changes and only revert one
        // that we set (never stomp a cursor owned by the game).
        private static Texture2D _appliedCursor;
        private static Vector2 _appliedHotspot;
        private static bool _ownsCursor;

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

        /// <summary> Aborts any in-flight gesture, reverts the cursor and clears state. Called on play-mode exit (leak guard). </summary>
        public static void Reset()
        {
            _active = false;
            _wasPressed = false;
            _handler = null;
            _state = default;
            ClearCursor();
            ResizeCursorSet.ClearDefaultCache();
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

                // Cursor stays as it was grabbed for the whole gesture (persists via change-detection).
                _handler.Update(pointerScreen, ref _state);
                return;
            }

            if (pressedThisFrame)
            {
                // Raise before grabbing: the focus pass consumes nothing, and running it first means the gesture walk
                // below sees the new recency order — so pressing a window behind another both raises AND grabs it.
                TryRaiseFocus(pointerScreen);
                TryBeginGesture(pointerScreen);
                return;
            }

            UpdateHoverCursor(pointerScreen);
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

                if (!TryResolveCanvas(popup, out Canvas canvas, out Camera camera)) continue;

                for (int e = 0; e < entries.Count; e++)
                {
                    PopupFeatureRegistry.Entry entry = entries[e];
                    if ((modules.Features & entry.Flag) == 0) continue;

                    if (entry.Handler.TryBegin(popup, pointerScreen, canvas, camera, out _state))
                    {
                        _handler = entry.Handler;
                        _active = true;

                        // Show the grabbed zone's cursor right away, even if the press had no prior hover frame.
                        if (CursorFeedbackEnabled && !Application.isMobilePlatform &&
                            entry.Handler is IPopupCursorHandler cursorHandler &&
                            cursorHandler.TryGetCursor(popup, pointerScreen, camera, out PopupCursor cursor))
                            ApplyCursor(cursor.Texture, cursor.Hotspot);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Press-edge pass for <see cref="PopupFeatureEnum.Focusable"/>: raises the topmost visible focusable popup whose
        /// focus zone is under the pointer (<see cref="AdvancedPopupSystem.BringToFront"/> — hierarchy slot + recency
        /// stack). Deliberately **not** a <see cref="IPopupFeatureHandler"/>: a handler owns the press, and focusing must
        /// leave the press to whatever the user actually aimed at (a button, a drag zone). One popup per press — the
        /// walk stops at the first hit because BringToFront reorders the list it iterates.
        /// </summary>
        private static void TryRaiseFocus(Vector2 pointerScreen)
        {
            var popups = AdvancedPopupSystem.ActivePopups;

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = popups[i];
                if (popup == null || !popup.IsVisible) continue;

                PopupModules modules = popup.Modules;
                if (modules == null || (modules.Features & PopupFeatureEnum.Focusable) == 0) continue;

                if (!TryResolveCanvas(popup, out Canvas _, out Camera camera)) continue;

                // No zone configured → the popup's own rect is the focus area.
                RectTransform zone = modules.Focus?.FocusZone != null ? modules.Focus.FocusZone : popup.RootTransform;
                if (zone == null) continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(zone, pointerScreen, camera)) continue;

                AdvancedPopupSystem.BringToFront(popup);
                return;
            }
        }

        /// <summary>
        /// The canvas a popup renders in plus the camera to hit-test against (null for a Screen Space - Overlay canvas,
        /// as RectTransformUtility expects). False when the popup has no rect or no canvas above it.
        /// </summary>
        private static bool TryResolveCanvas(IAdvancedPopup popup, out Canvas canvas, out Camera camera)
        {
            canvas = null;
            camera = null;

            RectTransform root = popup.RootTransform;
            if (root == null) return false;

            canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return true;
        }

        private static void EndGesture()
        {
            if (_active && _handler != null)
                _handler.End(ref _state);

            _active = false;
            _handler = null;
            _state = default;
            // Cursor is re-evaluated by the next idle hover pass (keep it if still over a grip, else revert).
        }

        #region Hover cursor
        /// <summary>
        /// Idle-frame pass: sets the OS cursor to the resize zone under the pointer (topmost feature-popup first,
        /// mirroring the grab order), or reverts to the default when the pointer is over none. Skipped on platforms
        /// without a system cursor and when the feature is switched off.
        /// </summary>
        private static void UpdateHoverCursor(Vector2 pointerScreen)
        {
            if (!CursorFeedbackEnabled || Application.isMobilePlatform)
            {
                ClearCursor();
                return;
            }

            var popups = AdvancedPopupSystem.ActivePopups;
            var entries = PopupFeatureRegistry.Entries;

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = popups[i];
                if (popup == null || !popup.IsVisible) continue;

                PopupModules modules = popup.Modules;
                if (modules == null || !modules.HasAny) continue;

                if (!TryResolveCanvas(popup, out Canvas _, out Camera camera)) continue;

                for (int e = 0; e < entries.Count; e++)
                {
                    PopupFeatureRegistry.Entry entry = entries[e];
                    if ((modules.Features & entry.Flag) == 0) continue;
                    if (!(entry.Handler is IPopupCursorHandler cursorHandler)) continue;

                    if (cursorHandler.TryGetCursor(popup, pointerScreen, camera, out PopupCursor cursor))
                    {
                        ApplyCursor(cursor.Texture, cursor.Hotspot);
                        return;
                    }
                }
            }

            ClearCursor();
        }

        private static void ApplyCursor(Texture2D texture, Vector2 hotspot)
        {
            if (_ownsCursor && texture == _appliedCursor && hotspot == _appliedHotspot) return;

            // ForceSoftware, not Auto: a hardware cursor is rescaled by the OS to the system cursor size (so it looks
            // big and ignores the texture's dimensions). The software cursor is drawn by Unity at the texture's exact
            // pixel size — predictable and crisp. Requires the texture to be Read/Write enabled + uncompressed (the
            // shipped cursors and ResizeCursorSet defaults are set up that way).
            Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware);
            _appliedCursor = texture;
            _appliedHotspot = hotspot;
            _ownsCursor = true;
        }

        private static void ClearCursor()
        {
            if (!_ownsCursor) return;

            Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
            _appliedCursor = null;
            _appliedHotspot = Vector2.zero;
            _ownsCursor = false;
        }
        #endregion
    }
}
