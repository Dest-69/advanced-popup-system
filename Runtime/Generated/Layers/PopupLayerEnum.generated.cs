using System;
namespace AdvancedPS.Core
{
    [Flags]
    public enum PopupLayerEnum
    {
        None = 0,
        GUI = 1 << 0,
        GAME = 1 << 1,
        MENU = 1 << 2,
        OVERLAY = 1 << 3,
    }
}
