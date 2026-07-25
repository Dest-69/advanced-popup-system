using System;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdvancedPS.Core.Input
{
    /// <summary>
    /// Pointer driver (New Input System). Injects a <see cref="PlayerLoopSystem"/> into the Update loop, reads the
    /// current pointer each frame and forwards it to the backend-agnostic <see cref="PopupInteractionSystem"/>. No
    /// MonoBehaviours are spawned. <see cref="Pointer.current"/> covers mouse, pen and touch.
    /// It also owns <c>AutoSwitchInputModule</c> — the only other startup job APS has under the New Input System,
    /// and this is the only APS system that compiles exclusively against it.
    /// </summary>
    public static class PointerEventSystemAPS
    {
        public static bool IsEnabled = true;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorInitialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            // Runs before the player-loop wiring below (and regardless of its outcome) — swapping the EventSystem's
            // module is about UI input as a whole, not about popup gestures.
            AutoSwitchInputModule();

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var updateSubsystemIndex = Array.FindIndex(playerLoop.subSystemList, subSystem => subSystem.type == typeof(Update));

            if (updateSubsystemIndex == -1)
            {
                APLogger.LogError("<color=red>[PointerEventSystemAPS]</color> - Update subsystem not found.");
                return;
            }

            PlayerLoopSystem updateSubsystem = playerLoop.subSystemList[updateSubsystemIndex];

            for (int i = 0; i < updateSubsystem.subSystemList.Length; i++)
            {
                if (updateSubsystem.subSystemList[i].type == typeof(PointerEventSystemAPS))
                    return;
            }

            PlayerLoopSystem updatedSystem = new PlayerLoopSystem
            {
                type = typeof(PointerEventSystemAPS),
                updateDelegate = Update
            };

            var newUpdateList = new PlayerLoopSystem[updateSubsystem.subSystemList.Length + 1];
            Array.Copy(updateSubsystem.subSystemList, newUpdateList, updateSubsystem.subSystemList.Length);
            newUpdateList[^1] = updatedSystem;
            updateSubsystem.subSystemList = newUpdateList;

            playerLoop.subSystemList[updateSubsystemIndex] = updateSubsystem;
            PlayerLoop.SetPlayerLoop(playerLoop);

            APLogger.Log("<color=green>[PointerEventSystemAPS]</color> - initialized successfully");
        }

        private static void Update()
        {
            if (!IsEnabled) return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 position = pointer.position.ReadValue();
            bool pressed = pointer.press.isPressed;
            PopupInteractionSystem.Tick(position, pressed);
        }

        /// <summary>
        /// Auto-switches EventSystem input module from StandaloneInputModule to InputSystemUIInputModule
        /// if the setting is enabled.
        /// </summary>
        private static void AutoSwitchInputModule()
        {
            if (!SettingsManager.Settings.AutoSwitchInputModule) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            var oldModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (oldModule != null)
                UnityEngine.Object.Destroy(oldModule);

            if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
