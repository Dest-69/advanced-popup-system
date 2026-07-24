using System;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdvancedPS.Core.Input
{
    /// <summary>
    /// Drives the escape close stack from the keyboard: one PlayerLoop update that turns a key press into
    /// <see cref="AdvancedPopupSystem.EscapeStep(Predicate{KeyCode})"/>.
    /// </summary>
    public static class KeyEventSystemAPS
    {
        /// <summary>
        /// Runtime gate — set false to suppress the escape key temporarily (cutscenes, custom input modes).
        /// Only meaningful while the Escape Close Stack setting is on: with it off the update isn't installed at all.
        /// </summary>
        public static bool IsEnabled = true;

        /// <summary>
        /// Cached probe handed to EscapeStep — a static delegate so the per-frame path allocates nothing.
        /// </summary>
        private static readonly Predicate<KeyCode> KeyPressed = UnityEngine.Input.GetKeyDown;

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
            {
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
            }
        }
#endif
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            // The escape close stack is the only thing this update does, so with it off we never touch the player loop.
            if (!SettingsManager.Settings.EscapeCloseEnabled) return;

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var updateSubsystemIndex = Array.FindIndex(playerLoop.subSystemList, subSystem => subSystem.type == typeof(Update));

            if (updateSubsystemIndex == -1)
            {
                APLogger.LogError("<color=red>[KeyEventSystemAPS]</color> - Update subsystem not found.");
                return;
            }
            
            PlayerLoopSystem updateSubsystem = playerLoop.subSystemList[updateSubsystemIndex];
            
            for (int i = 0; i < updateSubsystem.subSystemList.Length; i++)
            {
                if (updateSubsystem.subSystemList[i].type == typeof(KeyEventSystemAPS))
                    return;
            }
            
            PlayerLoopSystem updatedSystem = new PlayerLoopSystem
            {
                type = typeof(KeyEventSystemAPS),
                updateDelegate = Update
            };

            var newUpdateList = new PlayerLoopSystem[updateSubsystem.subSystemList.Length + 1];
            Array.Copy(updateSubsystem.subSystemList, newUpdateList, updateSubsystem.subSystemList.Length);
            newUpdateList[^1] = updatedSystem;
            updateSubsystem.subSystemList = newUpdateList;

            playerLoop.subSystemList[updateSubsystemIndex] = updateSubsystem;
            PlayerLoop.SetPlayerLoop(playerLoop);
            
            APLogger.Log("<color=green>[KeyEventSystemAPS]</color> - initialized successfully");
        }

        private static void Update()
        {
            if (!IsEnabled) return;

            // anyKeyDown first: the escape walk (and its per-popup GetKeyDown probes) only runs on frames with a real
            // press — which is also what replaced the old full KeyCode[] scan for "which key was it".
            if (!UnityEngine.Input.anyKeyDown) return;

            AdvancedPopupSystem.EscapeStep(KeyPressed);
        }
    }
}