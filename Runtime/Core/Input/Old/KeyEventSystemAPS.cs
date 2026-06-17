using System;
using System.Collections.Generic;
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
    public static class KeyEventSystemAPS
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
            {
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
            }
        }
#endif
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            IsEnabled = SettingsManager.Settings.KeyEventSystemEnabled;
            
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
            
            if (!UnityEngine.Input.anyKeyDown) return;
            
            KeyCode pressedKey = GetPressedKey();
            if (pressedKey == default) return;

            var allPopups = AdvancedPopupSystem.AllPopups;
            for (int i = 0; i < allPopups.Count; i++)
            {
                var popup = allPopups[i];
                if (popup == null) continue;
                
                var showSettings = popup.KeyBindingShowSettings;
                if (!popup.IsBeVisible &&
                    (showSettings.AnyHotKey || (showSettings.HotKeys != null && showSettings.HotKeys.Contains(pressedKey))) && 
                    (showSettings.Layers == default || showSettings.Layers.HasFlag(AdvancedPopupSystem.ActiveLayer)) &&
                    (showSettings.Popups == null || showSettings.Popups.Count == 0 || HasActivePopup(showSettings.Popups)))
                {
                    if (AreParentsVisible(popup))
                    {
                        popup.Show();
                        showSettings.OnTrigger?.Invoke();
                        break;
                    }
                }
                
                var hideSettings = popup.KeyBindingHideSettings;
                if (popup.IsBeVisible &&
                    (hideSettings.AnyHotKey || (hideSettings.HotKeys != null && hideSettings.HotKeys.Contains(pressedKey))) && 
                    (hideSettings.Layers == default || hideSettings.Layers.HasFlag(AdvancedPopupSystem.ActiveLayer)) &&
                    (hideSettings.Popups == null || hideSettings.Popups.Count == 0 || HasActivePopup(hideSettings.Popups)))
                {
                    popup.Hide();
                    hideSettings.OnTrigger?.Invoke();
                    break;
                }
            }
        }

        private static bool HasActivePopup(List<IAdvancedPopup> requiredPopups)
        {
            for (int i = 0; i < requiredPopups.Count; i++)
            {
                if (requiredPopups[i] != null && AdvancedPopupSystem.ActivePopups.Contains(requiredPopups[i]))
                    return true;
            }
            return false;
        }

        private static bool AreParentsVisible(IAdvancedPopup popup)
        {
            Transform parentTransform = popup.transform.parent;

            while (parentTransform != null)
            {
                IAdvancedPopup parentPopup = parentTransform.GetComponent<IAdvancedPopup>();
                if (parentPopup != null)
                {
                    if (!parentPopup.IsBeVisible) return false;
                    parentTransform = parentPopup.transform.parent;
                }
                else
                {
                    parentTransform = parentTransform.parent;
                }
            }

            return true;
        }
        
        private static KeyCode GetPressedKey()
        {
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                if (UnityEngine.Input.GetKeyDown(key))
                    return key;
            
            return default;
        }
    }
}