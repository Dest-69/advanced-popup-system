using System;

namespace AdvancedPS.Core.System
{
    [Serializable]
    public class PopupSettings
    {
        public InspectorEnum InspectorView { get; set; }
        public bool KeyEventSystemEnabled { get; set; }
        public bool AutoSwitchInputModule { get; set; }
        public string LogType { get; set; }
    }

    public enum InspectorEnum : byte
    {
        APSInspector = 0,
        APSOptimized = 1,
        UnityInspector = 2
    }
}