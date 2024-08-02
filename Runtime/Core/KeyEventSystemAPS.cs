using System;
using System.Linq;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace AdvancedPS.Core
{
    public static class KeyEventSystemAPS
    {
        public static bool IsEnabled = true;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            IsEnabled = SettingsManager.Settings.KeyEventSystemEnabled;
            
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var updateSubsystemIndex = Array.FindIndex(playerLoop.subSystemList, subSystem => subSystem.type == typeof(Update));

            if (updateSubsystemIndex == -1)
            {
                Debug.LogError("[KeyEventSystemAPS] - Update subsystem not found.");
                return;
            }

            PlayerLoopSystem updateSubsystem = playerLoop.subSystemList[updateSubsystemIndex];
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
            
            Debug.Log("[KeyEventSystemAPS] - initialized successfully");
        }

        private static void Update()
        {
            if (!IsEnabled || !Input.anyKeyDown) return;
            foreach (var popup in AdvancedPopupSystem.AllPopups)
            {
                if (!popup.IsBeVisible && (popup.AnyHotKeyShow || popup.HotKeyShow.Any(Input.GetKeyDown)))
                {
                    if (AreParentsVisible(popup))
                    {
                        popup.Show();
                        break;
                    }
                }
                
                if (popup.IsBeVisible && (popup.AnyHotKeyHide || popup.HotKeyHide.Any(Input.GetKeyDown)))
                {
                    popup.Hide();
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
    }
}