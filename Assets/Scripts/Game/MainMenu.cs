using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "GameScene"; 

    public void LoadDemo()
    {
        GameSettings.CurrentMode = GameMode.Demo;
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadSinglePlayer()
    {
        GameSettings.CurrentMode = GameMode.SinglePlayer;
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadMultiplayer()
    {
        GameSettings.CurrentMode = GameMode.Multiplayer;
        SceneManager.LoadScene(gameSceneName);
    }
}