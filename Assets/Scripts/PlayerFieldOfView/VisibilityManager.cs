using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class VisibilityManager : MonoBehaviour
{
    public enum ViewMode { Main, Player, Enemy }
    public enum MainFowType { Combined, PlayerOnly, EnemiesOnly }

    [Header("Global Setup")]
    public Camera mainCamera;
    public Grid grid;
    [Tooltip("The Quad object used for the visibility screen.")]
    public Transform visibilityQuad; 
    public RawImage debugImage;

    [Header("Quality Settings")]
    [Tooltip("Multiplier for FOV Resolution. 1 = Grid Size, 4+ = High Res Shadows.")]
    [Range(1, 10)]
    public int resolutionScale = 4;
    
    private EntityCameraRig playerRig;
    private List<EntityCameraRig> enemyRigs = new List<EntityCameraRig>();

    private ViewMode currentViewMode = ViewMode.Main;
    private MainFowType mainFowState = MainFowType.Combined;
    
    private int currentEnemyIndex = 0;
    private bool isSecondaryCamera = false;

    // Rendering Data
    private Texture2D visibilityTexture;
    private Color32[] textureColors;
    
    // The High-Resolution Map of obstacles
    private bool[] highResWallMap; 
    
    // Texture Dimensions
    private int texWidth;
    private int texHeight;
    private int coarseGridX;
    private int coarseGridY;

    void Start()
    {
        if (grid == null || visibilityQuad == null || mainCamera == null)
        {
            Debug.LogError("Grid, Visibility Quad, or Main Camera not assigned in VisibilityManager!");
            enabled = false;
            return;
        }

        // Dynamic Discovery
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerRig = playerObj.GetComponent<EntityCameraRig>();

        GameObject[] enemyObjs = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var obj in enemyObjs)
        {
            var rig = obj.GetComponent<EntityCameraRig>();
            if (rig != null) enemyRigs.Add(rig);
        }

        if (playerRig == null && enemyRigs.Count == 0)
        {
            Debug.LogError("No Player or Enemies found! Ensure Tags are set correctly.");
            return;
        }

        // High Res Texture Setup
        // Calculate coarse grid dimensions (AI Grid)
        coarseGridX = Mathf.RoundToInt(grid.gridWorldSize.x / (grid.nodeRadius * 2));
        coarseGridY = Mathf.RoundToInt(grid.gridWorldSize.y / (grid.nodeRadius * 2));

        // Calculate fine grid dimensions (Visual Grid)
        texWidth = coarseGridX * resolutionScale;
        texHeight = coarseGridY * resolutionScale;

        Debug.Log($"Initializing FOW. Logic: {coarseGridX}x{coarseGridY}. Visual: {texWidth}x{texHeight}");

        // Initialize Arrays
        visibilityTexture = new Texture2D(texWidth, texHeight, TextureFormat.Alpha8, false);
        visibilityTexture.wrapMode = TextureWrapMode.Clamp;
        visibilityTexture.filterMode = FilterMode.Bilinear;
        
        textureColors = new Color32[texWidth * texHeight];
        highResWallMap = new bool[texWidth * texHeight];

        // Build the high-res wall map using Physics
        BuildHighResWallMap();

        // Shader Global Variables
        Vector3 bottomLeft = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;
        Shader.SetGlobalVector("_GridBottomLeft", new Vector4(bottomLeft.x, bottomLeft.z, 0, 0));
        Shader.SetGlobalVector("_GridWorldSize", new Vector4(grid.gridWorldSize.x, grid.gridWorldSize.y, 0, 0));
        Shader.SetGlobalTexture("_VisibilityTex", visibilityTexture);
        
        ResetEntityRendering();

        // Initialize Camera
        UpdateActiveCamera();
    }

    void BuildHighResWallMap()
    {
        // Calculate the physical size of a single high-res pixel in world space
        float worldNodeSize = grid.nodeRadius * 2;
        float pixelWorldSize = worldNodeSize / resolutionScale;
        float pixelRadius = pixelWorldSize * 0.45f; // Slightly smaller than half to avoid edge overlaps

        // Get the bottom-left corner of the grid
        Vector3 gridBottomLeft = grid.transform.position 
                                 - Vector3.right * grid.gridWorldSize.x / 2 
                                 - Vector3.forward * grid.gridWorldSize.y / 2;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                // Calculate the precise World Position of this specific pixel
                float worldX = (x * pixelWorldSize) + (pixelWorldSize * 0.5f);
                float worldZ = (y * pixelWorldSize) + (pixelWorldSize * 0.5f);
                
                Vector3 pixelWorldPos = gridBottomLeft + Vector3.right * worldX + Vector3.forward * worldZ;

                // Perform a physics check at this specific pixel location
                // This creates high-fidelity wall definitions independent of the coarse grid
                if (Physics.CheckSphere(pixelWorldPos, pixelRadius, grid.unwalkableMask))
                {
                    highResWallMap[x + y * texWidth] = true;
                }
                else
                {
                    highResWallMap[x + y * texWidth] = false;
                }
            }
        }
    }

    void Update()
    {
        ValidateActiveEntities();
        
        HandleInput();
        
        UpdatePathVisualization();
        
        // Reset target visibility for this frame to 0 
        System.Array.Clear(textureColors, 0, textureColors.Length);

        // Compute visibility on the high-res grid
        ComputeHighResVisibility();

        // Upload to GPU immediately
        visibilityTexture.SetPixels32(textureColors);
        visibilityTexture.Apply(false);
        
        // Debug View
        if (debugImage != null) debugImage.texture = visibilityTexture;
    }
    
    private void ValidateActiveEntities()
    {
        // Remove dead enemies from the list
        int removedCount = enemyRigs.RemoveAll(rig => rig == null);

        // Check if the Player died
        if (playerRig == null && currentViewMode == ViewMode.Player)
        {
            Debug.Log("Player died. Switching to Main View.");
            currentViewMode = ViewMode.Main;
            UpdateActiveCamera();
        }

        // Check if the Enemy being spectated died
        if (currentViewMode == ViewMode.Enemy)
        {
            if (enemyRigs.Count == 0)
            {
                // No enemies left at all
                Debug.Log("All enemies died. Switching to Main View.");
                currentViewMode = ViewMode.Main;
                UpdateActiveCamera();
            }
            else if (currentEnemyIndex >= enemyRigs.Count)
            {
                // The index is now invalid
                currentEnemyIndex = 0;
                Debug.Log("Spectated enemy invalid. Switching to first available enemy.");
                UpdateActiveCamera();
            }
        }
    }

    private void HandleInput()
    {
        // '1' Cycles View Modes
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            switch (currentViewMode)
            {
                case ViewMode.Main: 
                    currentViewMode = ViewMode.Player; 
                    break;
                case ViewMode.Player: 
                    currentViewMode = (enemyRigs.Count > 0) ? ViewMode.Enemy : ViewMode.Main; 
                    break;
                case ViewMode.Enemy: 
                    currentViewMode = ViewMode.Main; 
                    break;
            }
            // Reset sub-states
            isSecondaryCamera = false; 
            mainFowState = MainFowType.Combined; 
            Debug.Log($"Mode Switched: {currentViewMode}");
            UpdateActiveCamera();
        }

        // '2' Toggles Camera Angle
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (currentViewMode != ViewMode.Main)
            {
                isSecondaryCamera = !isSecondaryCamera;
                Debug.Log($"Camera Angle: {(isSecondaryCamera ? "Secondary" : "Primary")}");
                UpdateActiveCamera();
            }
        }

        // 'Tab' Cycles Contents inside a mode
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (currentViewMode == ViewMode.Main)
            {
                // Cycle Fog Modes: Combined -> Player -> Enemies -> Combined
                mainFowState = (MainFowType)(((int)mainFowState + 1) % 3);
                Debug.Log($"Main View FOW: {mainFowState}");
            }
            else if (currentViewMode == ViewMode.Enemy)
            {
                // Cycle through Enemies
                if (enemyRigs.Count > 0)
                {
                    currentEnemyIndex = (currentEnemyIndex + 1) % enemyRigs.Count;
                    Debug.Log($"Spectating Enemy {currentEnemyIndex}");
                    UpdateActiveCamera();
                }
            }
        }
    }

    private void UpdateActiveCamera()
    {
        // Disable all cameras
        if (mainCamera) mainCamera.gameObject.SetActive(false);
        if (playerRig) { if (playerRig.primaryCamera) playerRig.primaryCamera.gameObject.SetActive(false); if (playerRig.secondaryCamera) playerRig.secondaryCamera.gameObject.SetActive(false); }
        foreach (var rig in enemyRigs) { if (rig.primaryCamera) rig.primaryCamera.gameObject.SetActive(false); if (rig.secondaryCamera) rig.secondaryCamera.gameObject.SetActive(false); }

        // Select new target camera
        Camera targetCam = null;
        switch (currentViewMode)
        {
            case ViewMode.Main: targetCam = mainCamera; break;
            case ViewMode.Player: if (playerRig) targetCam = playerRig.GetCamera(isSecondaryCamera); break;
            case ViewMode.Enemy: if (enemyRigs.Count > 0) targetCam = enemyRigs[currentEnemyIndex].GetCamera(isSecondaryCamera); break;
        }
        
        // Check if the camera is still valid
        if (targetCam == null && currentViewMode != ViewMode.Main)
        {
            currentViewMode = ViewMode.Main;
            targetCam = mainCamera;
        }

        // Activate and Move Quad
        if (targetCam != null)
        {
            targetCam.gameObject.SetActive(true);
            if (visibilityQuad != null)
            {
                visibilityQuad.SetParent(targetCam.transform);
                visibilityQuad.localPosition = new Vector3(0, 0, 0.4f);
                visibilityQuad.localRotation = Quaternion.identity;
                visibilityQuad.localScale = Vector3.one;
            }
        }
        
        // Update Player Control Scheme
        if (playerRig != null)
        {
            PlayerController controller = playerRig.GetComponent<PlayerController>();
            if (controller != null)
            {
                // If spectating the Player and using Secondary (Perspective) camera:
                bool isPerspective = (currentViewMode == ViewMode.Player && isSecondaryCamera);
                controller.SetControlMode(isPerspective);
            }
        }
    }

    private void ComputeHighResVisibility()
    {
        // Calculate grid world bounds once
        Vector3 worldBottomLeft = grid.transform.position 
                                - Vector3.right * grid.gridWorldSize.x / 2 
                                - Vector3.forward * grid.gridWorldSize.y / 2;

        void RunShadowCaster(EntityCameraRig rig)
        {
            if (rig == null) return;
            
            // Calculate relative world position
            Vector3 relativePos = rig.transform.position - worldBottomLeft;

            // Convert directly to High-Res Grid Coordinates
            // (Relative X / Total Width) * Total Pixels
            int pixelX = Mathf.RoundToInt((relativePos.x / grid.gridWorldSize.x) * texWidth);
            int pixelY = Mathf.RoundToInt((relativePos.z / grid.gridWorldSize.y) * texHeight);

            // Bounds Check
            if (pixelX < 0 || pixelX >= texWidth || pixelY < 0 || pixelY >= texHeight) return;
            
            // Run ShadowCaster
            ShadowCaster.ComputeVisibility(
                texWidth, texHeight, 
                new Vector2Int(pixelX, pixelY), 
                rig.fov.viewRadius * resolutionScale,
                (x, y) => highResWallMap[x + y * texWidth], 
                (x, y) => textureColors[x + y * texWidth].a = 255 
            );
        }

        // Determine who to compute for
        if (currentViewMode == ViewMode.Player)
        {
            RunShadowCaster(playerRig);
        }
        else if (currentViewMode == ViewMode.Enemy)
        {
            if (enemyRigs.Count > 0) RunShadowCaster(enemyRigs[currentEnemyIndex]);
        }
        else if (currentViewMode == ViewMode.Main)
        {
            if (mainFowState == MainFowType.Combined || mainFowState == MainFowType.PlayerOnly)
                RunShadowCaster(playerRig);
            
            if (mainFowState == MainFowType.Combined || mainFowState == MainFowType.EnemiesOnly)
                foreach (var r in enemyRigs) RunShadowCaster(r);
        }
    }
    
    private void ResetEntityRendering()
    {
        // Set the Render Queue higher than the Fog Quad.
        int entityQueue = 3002;

        void SetQueue(EntityCameraRig rig)
        {
            if (rig == null || rig.meshRenderer == null) return;
            rig.meshRenderer.material.renderQueue = entityQueue;
        }

        if (playerRig) SetQueue(playerRig);
        foreach (var rig in enemyRigs) SetQueue(rig);
    }
    
    // Visualizer API
    public GOAPAgent GetSpectatedEnemyAgent()
    {
        if (currentViewMode == ViewMode.Enemy && enemyRigs.Count > 0 && currentEnemyIndex < enemyRigs.Count)
        {
            if (enemyRigs[currentEnemyIndex] != null)
                return enemyRigs[currentEnemyIndex].GetComponent<GOAPAgent>();
        }
        return null;
    }
    
    private void UpdatePathVisualization()
    {
        // Clear previous frame's path
        grid.debugPath = null;

        // Check if spectating an enemy
        if (currentViewMode == ViewMode.Enemy && enemyRigs.Count > 0 && currentEnemyIndex < enemyRigs.Count)
        {
            EntityCameraRig rig = enemyRigs[currentEnemyIndex];
            if (rig == null) return;

            GOAPAgent agent = rig.GetComponent<GOAPAgent>();
            if (agent != null && agent.currentActions.Count > 0)
            {
                // Get the currently running action
                GOAPAction currentAction = agent.currentActions.Peek();
                
                // Get the path from the action
                if (currentAction != null)
                {
                    grid.debugPath = currentAction.GetPath();

                    // Determine Color based on Action Type
                    if (currentAction is MoveToAction)
                        grid.debugPathColor = Color.green; // Player Chase
                    else if (currentAction is RechargeAction)
                        grid.debugPathColor = new Color(0.6f, 0f, 1f); // Purple (Recharge)
                    else if (currentAction is RoamAction)
                        grid.debugPathColor = new Color(0.6f, 0.4f, 0.2f); // Brown (Roam)
                    else
                        grid.debugPathColor = Color.white; // Default
                }
            }
        }
    }
}