[System.Flags] public enum PopupLayerEnum
{
    // Hub scene
    LOGIN = 1 << 0,
    REGISTRATION = 1 << 1,
    HUB = 1 << 2,
    TOURNAMENTS_GAME_HISTORY = 1 << 3,
    CREATE_LOBBY = 1 << 4,
    JOIN_LOBBY = 1 << 5,
    CREATE_MINI_TOURNAMENT = 1 << 6,
    LOBBY = 1 << 7,
    JOIN_TOURNAMENT = 1 << 8,
    JOIN_BY_PASSWORD = 1 << 9,
    SETTINGS = 1 << 10,
    JOIN_BY_NAME = 1 << 11,
    
    // Battle
}