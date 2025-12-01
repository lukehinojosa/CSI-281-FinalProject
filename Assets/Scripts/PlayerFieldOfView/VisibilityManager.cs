using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class VisibilityManager : MonoBehaviour
{
    // Mode Enums
    public enum ViewMode { SinglePlayerDirector, MultiplayerSplitScreen }
    public enum MainFowType { Combined, PlayerOnly, EnemiesOnly }

    [Header("Global Setup")]
    public Camera mainCamera;
    public Grid grid;
    
    [Header("Dual Quad Setup")]
    [Tooltip("Quad on layer 'PlayerFog'. Used for SP and P1 in MP.")]
    public Transform playerFogQuad; 
    [Tooltip("Quad on layer 'EnemyFog'. Used for P2 in MP.")]
    public Transform enemyFogQuad;

    [Header("Debug")]
    public RawImage debugImage;

    [Header("Quality Settings")]
    [Tooltip("Multiplier for FOV Resolution. 1 = Grid Size, 4+ = High Res Shadows.")]
    [Range(1, 10)]
    public int resolutionScale = 4;

    // State
    public ViewMode currentMode = ViewMode.SinglePlayerDirector;
    
    // Singleplayer State
    private int currentSpectatorIndex = 0; // 0 = Main, 1 = Player, 2+ = Enemies
    private MainFowType spFowType = MainFowType.Combined;
    private bool spSecondaryCam = false;

    // Multiplayer State
    private EntityCameraRig mpPlayerRig;
    private EntityCameraRig mpEnemyRig; 

    // Render Pipeline
    private Texture2DArray visibilityArray; 
    
    private Color32[] colorsPlayer;
    private Color32[] colorsEnemy;
    private bool[] highResWallMap;
    
    private int texWidth;
    private int texHeight;
    private int coarseGridX;
    private int coarseGridY;
    
    // Cached Materials to set indices
    private Material matPlayer;
    private Material matEnemy;

    // Entity Tracking
    private EntityCameraRig playerEntity;
    private List<EntityCameraRig> enemyEntities = new List<EntityCameraRig>();

    void Start()
    {
        if (grid == null || playerFogQuad == null || enemyFogQuad == null || mainCamera == null)
        {
            Debug.LogError("Grid, PlayerFogQuad, EnemyFogQuad, or Main Camera not assigned in VisibilityManager!");
            enabled = false;
            return;
        }
        
        // Detach quads from parents
        playerFogQuad.SetParent(null);
        enemyFogQuad.SetParent(null);

        RefreshEntityList();

        // High Res Texture Setup
        // Calculate coarse grid dimensions (AI Grid)
        coarseGridX = Mathf.RoundToInt(grid.gridWorldSize.x / (grid.nodeRadius * 2));
        coarseGridY = Mathf.RoundToInt(grid.gridWorldSize.y / (grid.nodeRadius * 2));

        // Calculate fine grid dimensions (Visual Grid)
        texWidth = coarseGridX * resolutionScale;
        texHeight = coarseGridY * resolutionScale;

        Debug.Log($"Initializing FOW. Logic: {coarseGridX}x{coarseGridY}. Visual: {texWidth}x{texHeight}");

        // Initialize Texture Array
        // Slice 0 = Player, Slice 1 = Enemy
        visibilityArray = new Texture2DArray(texWidth, texHeight, 2, TextureFormat.Alpha8, false);
        visibilityArray.wrapMode = TextureWrapMode.Clamp;
        visibilityArray.filterMode = FilterMode.Bilinear;
        
        // Initialize Color Buffers
        colorsPlayer = new Color32[texWidth * texHeight];
        colorsEnemy = new Color32[texWidth * texHeight];
        highResWallMap = new bool[texWidth * texHeight];

        // Build the high-res wall map using Physics
        BuildHighResWallMap();
        
        // Setup Materials
        if (playerFogQuad.GetComponent<Renderer>()) matPlayer = playerFogQuad.GetComponent<Renderer>().material;
        if (enemyFogQuad.GetComponent<Renderer>()) matEnemy = enemyFogQuad.GetComponent<Renderer>().material;

        // Set indices for the quads so they know which slice to look at
        if (matPlayer) matPlayer.SetFloat("_FowIndex", 0); // Player Quad looks at Slice 0
        if (matEnemy) matEnemy.SetFloat("_FowIndex", 1);  // Enemy Quad looks at Slice 1

        // Shader Global Variables
        Vector3 bottomLeft = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;
        Shader.SetGlobalVector("_GridBottomLeft", new Vector4(bottomLeft.x, bottomLeft.z, 0, 0));
        Shader.SetGlobalVector("_GridWorldSize", new Vector4(grid.gridWorldSize.x, grid.gridWorldSize.y, 0, 0));
        
        // Bind the Array Globally
        Shader.SetGlobalTexture("_VisibilityTexArray", visibilityArray);
        
        ResetEntityRendering();
        
        // Initialize Camera
        if (currentMode == ViewMode.MultiplayerSplitScreen)
        {
            // GameManager ran first. Do nothing.
        }
        else if (GameSettings.CurrentMode == GameMode.Multiplayer)
        {
            SetMultiplayerMode(true);
        }
        else
        {
            if (GameSettings.CurrentMode == GameMode.SinglePlayer)
            {
                // Force start on Player
                currentSpectatorIndex = 1;
                // Force Primary Camera (Bird's Eye)
                spSecondaryCam = false; 
            }
            else
            {
                // Demo Mode starts on Main Camera (Index 0)
                currentSpectatorIndex = 0;
            }
            
            UpdateSPCamera(); 
        }
    }

    void BuildHighResWallMap()
    {
        float pixelSize = (grid.nodeRadius * 2) / resolutionScale;
        float pixelRadius = pixelSize * 0.45f;
        Vector3 bl = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;

        for (int y = 0; y < texHeight; y++) {
            for (int x = 0; x < texWidth; x++) {
                Vector3 pos = bl + Vector3.right * (x * pixelSize + pixelSize/2) + Vector3.forward * (y * pixelSize + pixelSize/2);
                highResWallMap[x + y * texWidth] = Physics.CheckSphere(pos, pixelRadius, grid.unwalkableMask);
            }
        }
    }

    void Update()
    {
        ValidateActiveEntities();

        if (currentMode == ViewMode.SinglePlayerDirector) HandleSPInput();

        UpdatePathVisualization();
        
        System.Array.Clear(colorsPlayer, 0, colorsPlayer.Length);
        System.Array.Clear(colorsEnemy, 0, colorsEnemy.Length);

        if (currentMode == ViewMode.MultiplayerSplitScreen)
        {
            if(mpPlayerRig) RunShadowCaster(mpPlayerRig, colorsPlayer);
            if(mpEnemyRig) RunShadowCaster(mpEnemyRig, colorsEnemy);
        }
        else
        {
            // Write to colorsPlayer (Slice 0) for the PlayerFogQuad
            if (currentSpectatorIndex == 0) // Main
            {
                if (spFowType != MainFowType.EnemiesOnly && playerEntity) RunShadowCaster(playerEntity, colorsPlayer);
                if (spFowType != MainFowType.PlayerOnly) foreach(var e in enemyEntities) RunShadowCaster(e, colorsPlayer);
            }
            else if (currentSpectatorIndex == 1 && playerEntity)
            {
                RunShadowCaster(playerEntity, colorsPlayer);
            }
            else if (currentSpectatorIndex >= 2)
            {
                int enemyIdx = currentSpectatorIndex - 2;
                if (enemyIdx < enemyEntities.Count) RunShadowCaster(enemyEntities[enemyIdx], colorsPlayer);
            }
        }

        // Apply to GPU
        // Write Player buffer to Slice 0
        visibilityArray.SetPixels32(colorsPlayer, 0);
        // Write Enemy buffer to Slice 1
        visibilityArray.SetPixels32(colorsEnemy, 1);
        
        // Upload all slices
        visibilityArray.Apply(false);

        // Camera Tracking
        UpdateQuadPositions();
    }

    void RunShadowCaster(EntityCameraRig rig, Color32[] buffer)
    {
        if (!rig) return;
        Vector3 bl = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;
        Vector3 rel = rig.transform.position - bl;
        
        int px = Mathf.RoundToInt((rel.x / grid.gridWorldSize.x) * texWidth);
        int py = Mathf.RoundToInt((rel.z / grid.gridWorldSize.y) * texHeight);

        if (px < 0 || px >= texWidth || py < 0 || py >= texHeight) return;

        ShadowCaster.ComputeVisibility(texWidth, texHeight, new Vector2Int(px, py), rig.fov.viewRadius * resolutionScale,
            (x, y) => highResWallMap[x + y * texWidth],
            (x, y) => buffer[x + y * texWidth].a = 255);
    }

    private void ValidateActiveEntities()
    {
        int removedCount = enemyEntities.RemoveAll(rig => rig == null);

        if (currentMode == ViewMode.MultiplayerSplitScreen)
        {
            // Handled by GameManager
        }
        else
        {
            if (playerEntity == null && currentSpectatorIndex == 1)
            {
                currentSpectatorIndex = 0; 
                UpdateSPCamera();
            }
            if (currentSpectatorIndex >= 2 && currentSpectatorIndex - 2 >= enemyEntities.Count)
            {
                currentSpectatorIndex = 0;
                UpdateSPCamera();
            }
        }
    }

    void HandleSPInput()
    {
        // Singleplayer mode
        if (GameSettings.CurrentMode == GameMode.SinglePlayer)
        {
            // '2' Toggles between Player's Primary and Secondary cameras
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                spSecondaryCam = !spSecondaryCam;
                Debug.Log($"Camera Angle: {(spSecondaryCam ? "Secondary" : "Primary")}");
                UpdateSPCamera();
            }

            // '1' to spectate player
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                if (currentSpectatorIndex != 1) 
                {
                    currentSpectatorIndex = 1;
                    UpdateSPCamera();
                }
            }

            // Tab and other cycling keys are ignored in SinglePlayer
            return;
        }

        // Demo Mode
        // '1' Cycles View Modes (Main -> Player -> Enemy...)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            currentSpectatorIndex++;
            int max = 2 + enemyEntities.Count;
            if (currentSpectatorIndex >= max) currentSpectatorIndex = 0;
            
            spSecondaryCam = false;
            Debug.Log($"Spectating Index: {currentSpectatorIndex}");
            UpdateSPCamera();
        }
        
        // '2' Toggles Camera Angle
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            spSecondaryCam = !spSecondaryCam;
            UpdateSPCamera();
        }
        
        // 'Tab' Cycles Fog Mode in Main View
        if (Input.GetKeyDown(KeyCode.Tab) && currentSpectatorIndex == 0)
        {
            spFowType = (MainFowType)(((int)spFowType + 1) % 3);
        }
    }

    void ConfigureCameraForLayer(Camera cam, int fogIndex, bool viewPlayerFog)
    {
        if (cam == null) return;
        
        // Set Shader Index
        var binder = cam.GetComponent<CameraFogBinder>();
        if (binder != null) binder.fogLayerIndex = fogIndex;

        // Update Culling Mask
        int pLayer = LayerMask.NameToLayer("PlayerFog");
        int eLayer = LayerMask.NameToLayer("EnemyFog");

        if (viewPlayerFog)
        {
            // See PlayerFog, Hide EnemyFog
            cam.cullingMask |= (1 << pLayer);
            cam.cullingMask &= ~(1 << eLayer);
        }
        else
        {
            // See EnemyFog, Hide PlayerFog
            cam.cullingMask &= ~(1 << pLayer);
            cam.cullingMask |= (1 << eLayer);
        }
    }

    void UpdateSPCamera()
    {
        DisableAllCameras();
        Camera cam = mainCamera;

        if (currentSpectatorIndex == 1 && playerEntity) 
            cam = playerEntity.GetCamera(spSecondaryCam);
        else if (currentSpectatorIndex >= 2) {
            int idx = currentSpectatorIndex - 2;
            if (idx < enemyEntities.Count) cam = enemyEntities[idx].GetCamera(spSecondaryCam);
        }

        if (cam)
        {
            cam.gameObject.SetActive(true);
            ConfigureCameraForLayer(cam, 0, true);
        }
        
        if (playerEntity) {
            var ctrl = playerEntity.GetComponent<PlayerController>();
            if(ctrl) ctrl.SetControlMode(currentSpectatorIndex == 1 && spSecondaryCam);
        }
    }

    void UpdateQuadPositions()
    {
        // Update Player Fog Quad
        Camera activePCam = GetActiveCameraForLayer("PlayerFog");
        if (playerFogQuad)
        {
            // Tell it which camera to follow.
            var fitter = playerFogQuad.GetComponent<FitQuadToCamera>();
            if (fitter != null)
            {
                fitter.targetCamera = activePCam;
            }
        }

        // Update Enemy Fog Quad
        if (enemyFogQuad)
        {
            Camera activeEnemyCam = null;

            // Only find a camera if in multiplayer mode and rig exists
            if (currentMode == ViewMode.MultiplayerSplitScreen && mpEnemyRig != null)
            {
                if (mpEnemyRig.primaryCamera && mpEnemyRig.primaryCamera.gameObject.activeInHierarchy)
                    activeEnemyCam = mpEnemyRig.primaryCamera;
                else if (mpEnemyRig.secondaryCamera && mpEnemyRig.secondaryCamera.gameObject.activeInHierarchy)
                    activeEnemyCam = mpEnemyRig.secondaryCamera;
            }

            var fitter = enemyFogQuad.GetComponent<FitQuadToCamera>();
            if (fitter != null)
            {
                fitter.targetCamera = activeEnemyCam;
            }
        }
    }

    Camera GetActiveCameraForLayer(string layerName)
    {
        if (currentMode == ViewMode.SinglePlayerDirector)
        {
            if (mainCamera.gameObject.activeInHierarchy) return mainCamera;
            if (playerEntity && playerEntity.primaryCamera.gameObject.activeInHierarchy) return playerEntity.primaryCamera;
            if (playerEntity && playerEntity.secondaryCamera.gameObject.activeInHierarchy) return playerEntity.secondaryCamera;
            foreach(var e in enemyEntities) {
                if(e.primaryCamera.gameObject.activeInHierarchy) return e.primaryCamera;
                if(e.secondaryCamera.gameObject.activeInHierarchy) return e.secondaryCamera;
            }
        }
        else
        {
            if (playerEntity && playerEntity.primaryCamera.gameObject.activeInHierarchy) return playerEntity.primaryCamera;
        }
        return null;
    }

    public void SetMultiplayerMode(bool enabled)
    {
        currentMode = enabled ? ViewMode.MultiplayerSplitScreen : ViewMode.SinglePlayerDirector;
        
        if (enemyFogQuad) enemyFogQuad.gameObject.SetActive(enabled);
        
        DisableAllCameras();
        
        if (enabled)
        {
            if (playerEntity) {
                mpPlayerRig = playerEntity;
                playerEntity.primaryCamera.rect = new Rect(0, 0, 0.5f, 1);
                playerEntity.primaryCamera.gameObject.SetActive(true);
                ConfigureCameraForLayer(playerEntity.primaryCamera, 0, true);
                
                // Set player camera size in multiplayer
                playerEntity.primaryCamera.orthographicSize = 18f;
            }
        }
        else
        {
            if (playerEntity) playerEntity.primaryCamera.rect = new Rect(0,0,1,1);
            UpdateSPCamera();
        }
    }

    public void SetMPOpponent(EntityCameraRig rig)
    {
        if (mpEnemyRig)
        {
            if(mpEnemyRig.primaryCamera) mpEnemyRig.primaryCamera.gameObject.SetActive(false);
            if(mpEnemyRig.secondaryCamera) mpEnemyRig.secondaryCamera.gameObject.SetActive(false);
        }

        mpEnemyRig = rig;

        if (mpEnemyRig)
        {
            Camera c = mpEnemyRig.GetCamera(false); 
            c.rect = new Rect(0.5f, 0, 0.5f, 1);
            c.gameObject.SetActive(true);
            ConfigureCameraForLayer(c, 1, false);
            
            // Set enemy camera sizes in multiplayer
            mpEnemyRig.primaryCamera.orthographicSize = 23f;
        }
    }

    void DisableAllCameras()
    {
        if (mainCamera) mainCamera.gameObject.SetActive(false);
        if (playerEntity) { 
            if(playerEntity.primaryCamera) playerEntity.primaryCamera.gameObject.SetActive(false); 
            if(playerEntity.secondaryCamera) playerEntity.secondaryCamera.gameObject.SetActive(false);
        }
        foreach (var r in enemyEntities) { 
            if(r.primaryCamera) r.primaryCamera.gameObject.SetActive(false); 
            if(r.secondaryCamera) r.secondaryCamera.gameObject.SetActive(false);
        }
    }

    void RefreshEntityList()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerEntity = p.GetComponent<EntityCameraRig>();
        
        enemyEntities.Clear();
        foreach (var go in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            var r = go.GetComponent<EntityCameraRig>();
            if (r) enemyEntities.Add(r);
        }
    }
    
    private void ResetEntityRendering()
    {
        int entityQueue = 3002;
        void SetQueue(EntityCameraRig rig)
        {
            if (rig == null || rig.meshRenderer == null) return;
            rig.meshRenderer.material.renderQueue = entityQueue;
        }
        if (playerEntity) SetQueue(playerEntity);
        foreach (var rig in enemyEntities) SetQueue(rig);
    }
    
    public GOAPAgent GetSpectatedEnemyAgent()
    {
        if (currentMode == ViewMode.MultiplayerSplitScreen) return mpEnemyRig ? mpEnemyRig.GetComponent<GOAPAgent>() : null;
        
        if (currentSpectatorIndex >= 2) {
            int idx = currentSpectatorIndex - 2;
            if (idx < enemyEntities.Count) return enemyEntities[idx].GetComponent<GOAPAgent>();
        }
        return null;
    }
    
    private void UpdatePathVisualization()
    {
        // Always reset path first
        grid.debugPath = null;

        // Do not show debug paths in Multiplayer
        if (currentMode == ViewMode.MultiplayerSplitScreen) return;

        // Check if spectating an enemy
        GOAPAgent activeAgent = GetSpectatedEnemyAgent();

        if (activeAgent != null && activeAgent.currentActions.Count > 0)
        {
            GOAPAction currentAction = activeAgent.currentActions.Peek();
            if (currentAction != null)
            {
                grid.debugPath = currentAction.GetPath();

                if (currentAction is MoveToAction) grid.debugPathColor = Color.green;
                else if (currentAction is RechargeAction) grid.debugPathColor = new Color(0.6f, 0f, 1f);
                else if (currentAction is RoamAction) grid.debugPathColor = new Color(0.6f, 0.4f, 0.2f);
                else grid.debugPathColor = Color.white;
            }
        }
    }
}