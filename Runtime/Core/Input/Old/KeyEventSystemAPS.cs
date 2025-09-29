using System;
using System.Linq;
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
            
            if (updateSubsystem.subSystemList.Any(s => s.type == typeof(KeyEventSystemAPS)))
                return;
            
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
            
            foreach (var popup in AdvancedPopupSystem.AllPopups)
            {
                if (!popup.IsBeVisible &&
                    (popup.KeyBindingShowSettings.AnyHotKey || popup.KeyBindingShowSettings.HotKeys.Contains(pressedKey)) && 
                    (popup.KeyBindingShowSettings.Layers == default || popup.KeyBindingShowSettings.Layers.HasFlag(AdvancedPopupSystem.ActiveLayer)) &&
                    (popup.KeyBindingShowSettings.Popups.Count == 0 || popup.KeyBindingShowSettings.Popups.Any(p => AdvancedPopupSystem.ActivePopups.Contains(p))))
                {
                    if (AreParentsVisible(popup))
                    {
                        popup.Show();
                        popup.KeyBindingShowSettings.OnTrigger?.Invoke();
                        break;
                    }
                }
                
                if (popup.IsBeVisible &&
                    (popup.KeyBindingHideSettings.AnyHotKey || popup.KeyBindingHideSettings.HotKeys.Contains(pressedKey)) && 
                    (popup.KeyBindingHideSettings.Layers == default || popup.KeyBindingHideSettings.Layers.HasFlag(AdvancedPopupSystem.ActiveLayer)) &&
                    (popup.KeyBindingHideSettings.Popups.Count == 0 || popup.KeyBindingHideSettings.Popups.Any(p => AdvancedPopupSystem.ActivePopups.Contains(p))))
                {
                    popup.Hide();
                    popup.KeyBindingHideSettings.OnTrigger?.Invoke();
                    break;
                }
            }
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