namespace AdvancedPS.Core.Enum
{
    [System.Flags] public enum PopupLayerEnum
    {
        LOGIN = 1 << 0,
        REGISTRATION = 1 << 1,
        HUB = 1 << 2,
        LOBBY = 1 << 3,
        SETTINGS = 1 << 4,
    }
}