using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "GameScene";
    
    [Header("UI Panels")]
    [Tooltip("The GameObject containing the controls text and back button")]
    public GameObject controlsPanel;
    
    // Online
    public RelayManager relay;
    public TMP_InputField codeInput;
    public TMP_Text codeDisplay; // To show the code to the Host
    [Tooltip("The GameObject containing the main buttons (Demo, Single, Multi, Controls)")]
    public GameObject menuPanel; // To hide menu after start

    void Start()
    {
        // Ensure correct initial state
        if(menuPanel) menuPanel.SetActive(true);
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
        if(menuPanel) menuPanel.SetActive(false);
        if(controlsPanel) controlsPanel.SetActive(true);
    }

    public void HideControls()
    {
        if(controlsPanel) controlsPanel.SetActive(false);
        if(menuPanel) menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
    
    public async void HostGame()
    {
        GameSettings.CurrentMode = GameMode.Multiplayer;
        string code = await relay.CreateRelay();
        codeDisplay.text = "CODE: " + code;
        codeDisplay.gameObject.SetActive(true);
        // Don't hide menu immediately, wait for player to read code or just show HUD
    }

    public void JoinGame()
    {
        GameSettings.CurrentMode = GameMode.Multiplayer;
        string code = codeInput.text;
        relay.JoinRelay(code);
        menuPanel.SetActive(false);
    }
}