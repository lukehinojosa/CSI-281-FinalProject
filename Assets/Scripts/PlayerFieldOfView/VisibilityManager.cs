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
    [Tooltip("How fast the fog fades in and out.")]
    public float fogSmoothSpeed = 10f; 

    // Internal Lists & State
    private EntityCameraRig playerRig;
    private List<EntityCameraRig> enemyRigs = new List<EntityCameraRig>();

    private ViewMode currentViewMode = ViewMode.Main;
    private MainFowType mainFowState = MainFowType.Combined;
    
    private int currentEnemyIndex = 0;
    private bool isSecondaryCamera = false;

    // Rendering Data
    private Texture2D visibilityTexture;
    private Color32[] textureColors;
    
    // Arrays for smoothing: Current brightness vs Target brightness
    private float[] currentVisibilityValues;
    private float[] targetVisibilityValues;
    
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
        // 1. Calculate coarse grid dimensions (AI Grid)
        coarseGridX = Mathf.RoundToInt(grid.gridWorldSize.x / (grid.nodeRadius * 2));
        coarseGridY = Mathf.RoundToInt(grid.gridWorldSize.y / (grid.nodeRadius * 2));

        // 2. Calculate fine grid dimensions (Visual Grid)
        texWidth = coarseGridX * resolutionScale;
        texHeight = coarseGridY * resolutionScale;

        Debug.Log($"Initializing FOW. Logic: {coarseGridX}x{coarseGridY}. Visual: {texWidth}x{texHeight}");

        // 3. Initialize Arrays
        visibilityTexture = new Texture2D(texWidth, texHeight, TextureFormat.Alpha8, false);
        visibilityTexture.wrapMode = TextureWrapMode.Clamp;
        visibilityTexture.filterMode = FilterMode.Bilinear; // For smoothness
        
        textureColors = new Color32[texWidth * texHeight];
        currentVisibilityValues = new float[texWidth * texHeight];
        targetVisibilityValues = new float[texWidth * texHeight];
        highResWallMap = new bool[texWidth * texHeight];

        // 4. Build the static wall map for high-res rendering
        BuildHighResWallMap();

        // 5. Shader Global Variables
        Vector3 bottomLeft = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;
        Shader.SetGlobalVector("_GridBottomLeft", new Vector4(bottomLeft.x, bottomLeft.z, 0, 0));
        Shader.SetGlobalVector("_GridWorldSize", new Vector4(grid.gridWorldSize.x, grid.gridWorldSize.y, 0, 0));
        Shader.SetGlobalTexture("_VisibilityTex", visibilityTexture);

        // Initialize Camera
        UpdateActiveCamera();
    }

    void BuildHighResWallMap()
    {
        Node[,] nodes = grid.GetGridNodes();
        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                // Map pixel coordinate back to coarse grid coordinate
                int cx = x / resolutionScale;
                int cy = y / resolutionScale;

                if (cx < coarseGridX && cy < coarseGridY)
                {
                    // If the coarse node is an obstacle, this pixel is a wall
                    if (!nodes[cx, cy].isWalkable)
                    {
                        highResWallMap[x + y * texWidth] = true;
                    }
                }
            }
        }
    }

    void Update()
    {
        HandleInput();
        
        // 1. Reset target visibility for this frame to 0
        System.Array.Clear(targetVisibilityValues, 0, targetVisibilityValues.Length);

        // 2. Compute visibility on the high-res grid
        ComputeHighResVisibility();

        // 3. Smoothly interpolate values and upload to GPU
        UpdateTextureSmoothing();
        
        // 4. Update Character Transparency based on high-res data
        UpdateEntityTransparency();

        // 5. Debug View
        if (debugImage != null) debugImage.texture = visibilityTexture;
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
    }

    private void ComputeHighResVisibility()
    {
        // Local helper function to run shadowcaster for a single entity
        void RunShadowCaster(EntityCameraRig rig)
        {
            if (rig == null) return;
            
            // Get coarse position
            Node n = grid.NodeFromWorldPoint(rig.transform.position);
            if (n == null) return;

            // Convert to High-Res center
            Vector2Int origin = new Vector2Int(
                n.gridX * resolutionScale + (resolutionScale / 2), 
                n.gridY * resolutionScale + (resolutionScale / 2)
            );

            // Scale radius
            int scaledRadius = rig.fov.viewRadius * resolutionScale;

            // Run ShadowCaster on the High-Res Grid
            ShadowCaster.ComputeVisibility(
                texWidth, texHeight, 
                origin, 
                scaledRadius,
                (x, y) => highResWallMap[x + y * texWidth], // IsBlocking?
                (x, y) => targetVisibilityValues[x + y * texWidth] = 1.0f // SetVisible!
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

    private void UpdateTextureSmoothing()
    {
        for (int i = 0; i < currentVisibilityValues.Length; i++)
        {
            // Lerp current value towards target (0 or 1)
            currentVisibilityValues[i] = Mathf.Lerp(currentVisibilityValues[i], targetVisibilityValues[i], Time.deltaTime * fogSmoothSpeed);
            
            // Map to byte for texture
            textureColors[i].a = (byte)(currentVisibilityValues[i] * 255);
        }

        visibilityTexture.SetPixels32(textureColors);
        visibilityTexture.Apply(false);
    }
    
    private void UpdateEntityTransparency()
    {
        // Helper to get visibility float (0.0 - 1.0) at a specific world position
        float GetVisibilityAtWorldPos(Vector3 pos)
        {
            Node n = grid.NodeFromWorldPoint(pos);
            if (n == null) return 0f;
            
            // Map coarse node to high-res pixel
            int x = n.gridX * resolutionScale + (resolutionScale / 2);
            int y = n.gridY * resolutionScale + (resolutionScale / 2);
            
            if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
                return currentVisibilityValues[x + y * texWidth];
            
            return 0f;
        }

        // Helper to check if we are currently spectating a specific rig
        bool IsSpectating(EntityCameraRig rig)
        {
            if (currentViewMode == ViewMode.Player && rig == playerRig) return true;
            if (currentViewMode == ViewMode.Enemy && enemyRigs.Count > 0 && rig == enemyRigs[currentEnemyIndex]) return true;
            return false;
        }

        // Update Player
        if (playerRig)
        {
            if (IsSpectating(playerRig)) playerRig.SetMaterialAlpha(1.0f);
            else playerRig.SetMaterialAlpha(GetVisibilityAtWorldPos(playerRig.transform.position));
        }

        // Update Enemies
        foreach (var rig in enemyRigs)
        {
            if (IsSpectating(rig)) rig.SetMaterialAlpha(1.0f);
            else rig.SetMaterialAlpha(GetVisibilityAtWorldPos(rig.transform.position));
        }
    }
}