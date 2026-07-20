using System;
using System.Collections.Generic;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdvancedPS.Core.Input
{
    public static class KeyEventSystemAPS
    {
        public static bool IsEnabled = true;
        
        /// <summary>
        /// Auto-generated map: KeyCode → InputSystem.Key.
        /// Built once from Unity's own enum values + manual overrides for naming differences.
        /// </summary>
        private static readonly Dictionary<KeyCode, Key> KeyCodeToKeyMap = BuildKeyCodeToKeyMap();
        
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
            
            AutoSwitchInputModule();
            
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
            
            var kb = Keyboard.current;
            if (kb == null || !kb.anyKey.wasPressedThisFrame) return;

            // Escape close stack has priority over per-popup bindings; a consumed step eats the whole frame
            // so one press can't also trigger a binding (or an AnyHotKey show).
            PopupSettings settings = SettingsManager.Settings;
            if (settings.EscapeCloseEnabled &&
                KeyCodeToKeyMap.TryGetValue(settings.EscapeCloseKey, out Key escapeKey) &&
                kb[escapeKey].wasPressedThisFrame &&
                AdvancedPopupSystem.EscapeStep())
                return;

            var allPopups = AdvancedPopupSystem.AllPopups;
            for (int i = 0; i < allPopups.Count; i++)
            {
                var popup = allPopups[i];
                if (popup == null) continue;
                
                var showSettings = popup.KeyBindingShowSettings;
                if (!popup.IsBeVisible &&
                    (showSettings.AnyHotKey || IsAnyHotKeyPressed(kb, showSettings.HotKeys)) &&
                    (showSettings.Layers == default || (showSettings.Layers & AdvancedPopupSystem.ActiveLayer) == AdvancedPopupSystem.ActiveLayer) &&
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
                    (hideSettings.AnyHotKey || IsAnyHotKeyPressed(kb, hideSettings.HotKeys)) &&
                    (hideSettings.Layers == default || (hideSettings.Layers & AdvancedPopupSystem.ActiveLayer) == AdvancedPopupSystem.ActiveLayer) &&
                    (hideSettings.Popups == null || hideSettings.Popups.Count == 0 || HasActivePopup(hideSettings.Popups)))
                {
                    popup.Hide();
                    hideSettings.OnTrigger?.Invoke();
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if any of the configured KeyCodes were pressed this frame
        /// using the InputSystem Keyboard API directly.
        /// </summary>
        private static bool IsAnyHotKeyPressed(Keyboard kb, List<KeyCode> hotKeys)
        {
            if (hotKeys == null || hotKeys.Count == 0) return false;
            
            for (int i = 0; i < hotKeys.Count; i++)
            {
                if (KeyCodeToKeyMap.TryGetValue(hotKeys[i], out var key) && kb[key].wasPressedThisFrame)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Checks if any of the required popups are currently active.
        /// </summary>
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

        /// <summary>
        /// Builds a KeyCode → Key mapping using Unity's own enum values.
        /// Most keys match by name automatically; only known naming differences
        /// (e.g. Alpha0→Digit0, Return→Enter) need manual overrides.
        /// </summary>
        private static Dictionary<KeyCode, Key> BuildKeyCodeToKeyMap()
        {
            var map = new Dictionary<KeyCode, Key>();
            
            // Step 1: Auto-match keys that share the same name in both enums
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None || key == Key.IMESelected) continue;
                if (Enum.TryParse(key.ToString(), true, out KeyCode keyCode))
                    map[keyCode] = key;
            }
            
            // Step 2: Manual overrides for keys with different names between enums
            map[KeyCode.Return]         = Key.Enter;
            map[KeyCode.LeftControl]    = Key.LeftCtrl;
            map[KeyCode.RightControl]   = Key.RightCtrl;
            map[KeyCode.LeftWindows]    = Key.LeftMeta;
            map[KeyCode.LeftCommand]    = Key.LeftMeta;
            map[KeyCode.RightWindows]   = Key.RightMeta;
            map[KeyCode.RightCommand]   = Key.RightMeta;
            map[KeyCode.Menu]           = Key.ContextMenu;
            map[KeyCode.Print]          = Key.PrintScreen;
            map[KeyCode.Numlock]        = Key.NumLock;
            
            // Alpha digits → Input System Digit keys
            map[KeyCode.Alpha0] = Key.Digit0;
            map[KeyCode.Alpha1] = Key.Digit1;
            map[KeyCode.Alpha2] = Key.Digit2;
            map[KeyCode.Alpha3] = Key.Digit3;
            map[KeyCode.Alpha4] = Key.Digit4;
            map[KeyCode.Alpha5] = Key.Digit5;
            map[KeyCode.Alpha6] = Key.Digit6;
            map[KeyCode.Alpha7] = Key.Digit7;
            map[KeyCode.Alpha8] = Key.Digit8;
            map[KeyCode.Alpha9] = Key.Digit9;
            
            // Keypad keys → Input System Numpad keys
            map[KeyCode.Keypad0]        = Key.Numpad0;
            map[KeyCode.Keypad1]        = Key.Numpad1;
            map[KeyCode.Keypad2]        = Key.Numpad2;
            map[KeyCode.Keypad3]        = Key.Numpad3;
            map[KeyCode.Keypad4]        = Key.Numpad4;
            map[KeyCode.Keypad5]        = Key.Numpad5;
            map[KeyCode.Keypad6]        = Key.Numpad6;
            map[KeyCode.Keypad7]        = Key.Numpad7;
            map[KeyCode.Keypad8]        = Key.Numpad8;
            map[KeyCode.Keypad9]        = Key.Numpad9;
            map[KeyCode.KeypadEnter]    = Key.NumpadEnter;
            map[KeyCode.KeypadPlus]     = Key.NumpadPlus;
            map[KeyCode.KeypadMinus]    = Key.NumpadMinus;
            map[KeyCode.KeypadMultiply] = Key.NumpadMultiply;
            map[KeyCode.KeypadDivide]   = Key.NumpadDivide;
            map[KeyCode.KeypadPeriod]   = Key.NumpadPeriod;
            map[KeyCode.KeypadEquals]   = Key.NumpadEquals;
            
            return map;
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