using System;
using System.Collections.Generic;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.Events;

namespace AdvancedPS.Core
{
    [Serializable]
    public struct PopupKeyBinding
    {
        /// <summary>
        /// Allow by pressing any key.
        /// </summary>
        [Tooltip("Allow by pressing any key.")] 
        public bool AnyHotKey;
        /// <summary>
        /// Keys witch bind to popup.
        /// </summary>
        [Tooltip("Keys witch bind to popup.")] 
        public List<KeyCode> HotKeys;
        /// <summary>
        /// Can be invoked when one of selected layers is active.
        /// If empty - can be invoked at any time if other conditions passed.
        /// </summary>
        [Tooltip("Can be invoked when one of selected layers is active.\n" +
                 "If empty - can be invoked at any time if other conditions passed.")] 
        public PopupLayerEnum Layers;
        /// <summary>
        /// Can be invoked when selected popups is IsVisible. (IsBeVisible popups will be ignored)
        /// If empty - can be invoked at any time if other conditions passed. (No need put self popup here)
        /// </summary>
        [Tooltip("Can be invoked when selected popups is IsVisible. (IsBeVisible popups will be ignored)\n" +
                 "If empty - can be invoked at any time if other conditions passed. (No need put self popup here)")] 
        public List<IAdvancedPopup> Popups;
        /// <summary>
        /// Action will invoke when key triggered.
        /// </summary>
        [Tooltip("Action will invoke when key triggered.")]
        public UnityEvent OnTrigger;
        
        public PopupKeyBinding(bool anyHotKey)
        {
            AnyHotKey = anyHotKey;
            HotKeys = new List<KeyCode>();
            Layers = default;
            Popups = new List<IAdvancedPopup>();
            OnTrigger = null;
        }
    }
}