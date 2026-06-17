using UnityEngine;
using UnityEngine.EventSystems;

namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Automatically switches EventSystem from StandaloneInputModule to InputSystemUIInputModule
    /// when enabled in APS Settings (Auto Switch Input Module checkbox).
    /// No need to add this to the scene manually.
    /// </summary>
    public static class InputSwitcher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSwitch()
        {
#if ENABLE_INPUT_SYSTEM
            if (!AdvancedPS.Core.System.SettingsManager.Settings.AutoSwitchInputModule) return;
            
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            
            StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (inputModule)
            {
                Object.Destroy(inputModule);
            }
            
            UnityEngine.InputSystem.UI.InputSystemUIInputModule newInputModule = 
                eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (newInputModule == null)
            {
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#endif
        }
    }
}