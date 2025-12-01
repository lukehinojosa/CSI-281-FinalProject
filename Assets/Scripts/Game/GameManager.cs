using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [Header("Single Player UI")]
    public GameObject globalWinScreen;
    public GameObject globalLoseScreen;

    [Header("Multiplayer UI")]
    public Canvas playerSplitCanvas;
    public GameObject playerWinMsg;
    public GameObject playerLoseMsg;

    public Canvas enemySplitCanvas;
    public GameObject enemyWinMsg;
    public GameObject enemyLoseMsg;

    [Header("Scene References")]
    public Camera mainCamera; 
    public VisibilityManager visibilityManager;
    
    private List<EnemyUnit> enemies = new List<EnemyUnit>();
    private EnemyUnit currentPossessedUnit;
    private bool gameEnded = false;

    void Start()
    {
        enemies = FindObjectsOfType<EnemyUnit>().ToList();
        StartCoroutine(InitializeGameMode());
    }

    IEnumerator InitializeGameMode()
    {
        yield return new WaitForEndOfFrame();
        SetupGameMode();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene("MainMenu");
        return;
        
        if (gameEnded)
        {
            if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        // Win Condition Checks
        enemies.RemoveAll(e => e == null);

        if (enemies.Count == 0)
        {
            // If all enemies dead, Player Wins
            GameOver(true); 
        }

        // Multiplayer Input
        if (GameSettings.CurrentMode == GameMode.Multiplayer)
        {
            // If the possessed unit died
            if (currentPossessedUnit == null)
            {
                // Force possess the first available enemy
                Debug.Log("Possessed unit died! Swapping to next unit...");
                PossessEnemy(0);
            }
            
            HandlePossessionInput();
        }
    }

    void SetupGameMode()
    {
        Debug.Log($"Starting Mode: {GameSettings.CurrentMode}");
        Time.timeScale = 1;
        
        if(playerSplitCanvas) playerSplitCanvas.gameObject.SetActive(false);
        if(enemySplitCanvas) enemySplitCanvas.gameObject.SetActive(false);

        switch (GameSettings.CurrentMode)
        {
            case GameMode.Demo:
                break;
            case GameMode.SinglePlayer:
                break;
            case GameMode.Multiplayer:
                SetupSplitScreen();
                break;
        }
    }

    void SetupSplitScreen()
    {
        if (mainCamera) mainCamera.gameObject.SetActive(false);

        if (visibilityManager != null)
        {
            visibilityManager.SetMultiplayerMode(true);
        }
        
        // Possess first enemy and attach UI
        PossessEnemy(0);
    }

    void HandlePossessionInput()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            int currentIndex = enemies.IndexOf(currentPossessedUnit);
            int nextIndex = (currentIndex + 1) % enemies.Count;
            
            PossessEnemy(nextIndex);
        }
    }

    void PossessEnemy(int index)
    {
        if (enemies.Count == 0) return;
        
        if (index >= enemies.Count) index = 0;

        // Release all
        foreach (var e in enemies) 
        {
            if(e != null) e.ReleasePossession();
        }

        // Possess new
        currentPossessedUnit = enemies[index];
        currentPossessedUnit.isPossessed = true;
        
        if (visibilityManager != null)
        {
            EntityCameraRig rig = currentPossessedUnit.GetComponent<EntityCameraRig>();
            visibilityManager.SetMPOpponent(rig);

            // Re-bind UI Camera
            if (enemySplitCanvas != null && rig != null)
            {
                // Find active camera
                Camera cam = null;
                if (rig.primaryCamera && rig.primaryCamera.gameObject.activeInHierarchy) cam = rig.primaryCamera;
                else if (rig.secondaryCamera && rig.secondaryCamera.gameObject.activeInHierarchy) cam = rig.secondaryCamera;
                
                // Fallback to primary if neither active yet
                if (cam == null) cam = rig.primaryCamera; 

                enemySplitCanvas.worldCamera = cam;
            }
        }
    }

    void SetupBlackoutCamera(Rect viewportRect)
    {
        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(true);
            mainCamera.farClipPlane = 0.5f;

            mainCamera.rect = viewportRect;
        }
    }

    public void GameOver(bool playerWon)
    {
        if (gameEnded) return;
        gameEnded = true;
        Time.timeScale = 0; // Pause game

        if (GameSettings.CurrentMode == GameMode.Multiplayer)
        {
            // Split Screen Outcomes
            
            // Activate Canvases
            if (playerSplitCanvas) playerSplitCanvas.gameObject.SetActive(true);
            if (enemySplitCanvas) enemySplitCanvas.gameObject.SetActive(true);
            
            // If a side loses, make backdrop black by including both fog layers
            if (mainCamera != null)
            {
                if (playerWon)
                {
                    // Enemy Lost. Use Main Camera for Right Side.
                    SetupBlackoutCamera(new Rect(0.5f, 0, 0.5f, 1));
                    if (enemySplitCanvas) enemySplitCanvas.worldCamera = mainCamera;
                }
                else
                {
                    // Player Lost. Use Main Camera for Left Side.
                    SetupBlackoutCamera(new Rect(0, 0, 0.5f, 1));
                    if (playerSplitCanvas) playerSplitCanvas.worldCamera = mainCamera;
                }
            }

            // Set Messages
            if (playerWon)
            {
                // Player Win
                if(playerWinMsg) playerWinMsg.SetActive(true);
                // Enemy Lose
                if(enemyLoseMsg) enemyLoseMsg.SetActive(true);
                Debug.Log("MP RESULT: Player Wins");
            }
            else
            {
                // Player Lose
                if(playerLoseMsg) playerLoseMsg.SetActive(true);
                // Enemy Win
                if(enemyWinMsg) enemyWinMsg.SetActive(true);
                Debug.Log("MP RESULT: Enemy Wins");
            }
        }
        else
        {
            // Singleplayer Outcomes
            
            SetupBlackoutCamera(new Rect(0, 0, 1, 1));
            
            if (playerWon)
            {
                if (globalWinScreen) globalWinScreen.SetActive(true);
                Debug.Log("SP RESULT: Player Wins");
            }
            else
            {
                if (globalLoseScreen) globalLoseScreen.SetActive(true);
                Debug.Log("SP RESULT: Enemy Wins");
            }
        }
    }
}