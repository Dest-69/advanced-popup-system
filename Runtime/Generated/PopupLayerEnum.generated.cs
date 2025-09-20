using System;
namespace AdvancedPS.Core
{
    [Flags]
    public enum PopupLayerEnum
    {
        None = 0,
        LOGIN = 1 << 0,
        REGISTRATION = 1 << 1,
        HUB = 1 << 2,
        SETTINGS = 1 << 3,
    }
}
