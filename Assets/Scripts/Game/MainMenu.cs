using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "GameScene";
    
    [Header("UI Panels")]
    [Tooltip("The GameObject containing the main buttons (Demo, Single, Multi, Controls)")]
    public GameObject mainButtonsPanel;
    [Tooltip("The GameObject containing the controls text and back button")]
    public GameObject controlsPanel;

    void Start()
    {
        // Ensure correct initial state
        if(mainButtonsPanel) mainButtonsPanel.SetActive(true);
        if(controlsPanel) controlsPanel.SetActive(false);
    }

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
    
    public void ShowControls()
    {
        if(mainButtonsPanel) mainButtonsPanel.SetActive(false);
        if(controlsPanel) controlsPanel.SetActive(true);
    }

    public void HideControls()
    {
        if(controlsPanel) controlsPanel.SetActive(false);
        if(mainButtonsPanel) mainButtonsPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}