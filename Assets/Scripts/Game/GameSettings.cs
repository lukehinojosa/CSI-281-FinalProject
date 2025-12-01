public enum GameMode
{
    Demo,
    SinglePlayer,
    Multiplayer
}

public static class GameSettings
{
    public static GameMode CurrentMode = GameMode.Demo;
}