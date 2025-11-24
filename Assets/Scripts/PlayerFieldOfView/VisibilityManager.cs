using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class VisibilityManager : MonoBehaviour
{
    public enum ViewMode { Main, Player, Enemy }
    public enum MainFowType { Combined, PlayerOnly, EnemiesOnly }

    [Header("Global Setup")]
    public Camera mainCamera; // Assign your top-down overview camera here
    public Grid grid;
    public Transform visibilityQuad; 
    public RawImage debugImage;

    // Internal Lists
    private EntityCameraRig playerRig;
    private List<EntityCameraRig> enemyRigs = new List<EntityCameraRig>();

    // State Machine
    private ViewMode currentViewMode = ViewMode.Main;
    private MainFowType mainFowState = MainFowType.Combined;
    
    private int currentEnemyIndex = 0;
    private bool isSecondaryCamera = false;

    // Rendering
    private Texture2D visibilityTexture;
    private Color32[] textureColors;
    private int gridSizeX;
    private int gridSizeY;

    void Start()
    {
        if (grid == null || visibilityQuad == null || mainCamera == null)
        {
            Debug.LogError("Grid, Visibility Quad, or Main Camera not assigned!");
            enabled = false;
            return;
        }
        
        // 1. Find Player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerRig = playerObj.GetComponent<EntityCameraRig>();

        // 2. Find Enemies
        GameObject[] enemyObjs = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var obj in enemyObjs)
        {
            var rig = obj.GetComponent<EntityCameraRig>();
            if (rig != null) enemyRigs.Add(rig);
        }

        if (playerRig == null && enemyRigs.Count == 0)
        {
            Debug.LogError("No Player or Enemies found! Ensure Tags are set.");
            return;
        }

        // Texture setup
        gridSizeX = Mathf.RoundToInt(grid.gridWorldSize.x / (grid.nodeRadius * 2));
        gridSizeY = Mathf.RoundToInt(grid.gridWorldSize.y / (grid.nodeRadius * 2));

        visibilityTexture = new Texture2D(gridSizeX, gridSizeY, TextureFormat.Alpha8, false);
        visibilityTexture.wrapMode = TextureWrapMode.Clamp;
        visibilityTexture.filterMode = FilterMode.Bilinear;
        textureColors = new Color32[gridSizeX * gridSizeY];

        Vector3 bottomLeft = grid.transform.position - Vector3.right * grid.gridWorldSize.x / 2 - Vector3.forward * grid.gridWorldSize.y / 2;
        Shader.SetGlobalVector("_GridBottomLeft", new Vector4(bottomLeft.x, bottomLeft.z, 0, 0));
        Shader.SetGlobalVector("_GridWorldSize", new Vector4(grid.gridWorldSize.x, grid.gridWorldSize.y, 0, 0));
        Shader.SetGlobalTexture("_VisibilityTex", visibilityTexture);
        
        UpdateActiveCamera();
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        // 1. Calculate which nodes are visible based on current state
        HashSet<Node> visibleNodes = GetCurrentVisibleNodes();
        
        // 2. Paint the texture
        UpdateVisibilityTexture(visibleNodes);
        
        // 3. Update entity transparency (hide enemies not in view)
        UpdateEntityTransparency(visibleNodes);

        if (debugImage != null) debugImage.texture = visibilityTexture;
    }

    private void HandleInput()
    {
        // Cycle Modes (Main -> Player -> Enemy)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            switch (currentViewMode)
            {
                case ViewMode.Main:
                    currentViewMode = ViewMode.Player;
                    break;
                case ViewMode.Player:
                    // Only switch to Enemy mode if enemies exist
                    currentViewMode = (enemyRigs.Count > 0) ? ViewMode.Enemy : ViewMode.Main;
                    break;
                case ViewMode.Enemy:
                    currentViewMode = ViewMode.Main;
                    break;
            }
            
            // Reset sub-states on mode switch
            isSecondaryCamera = false;
            mainFowState = MainFowType.Combined; // Main always starts as Combined
            Debug.Log($"Mode Switched: {currentViewMode}");
            UpdateActiveCamera();
        }

        // Toggle Camera Angle (Player/Enemy)
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (currentViewMode != ViewMode.Main)
            {
                isSecondaryCamera = !isSecondaryCamera;
                Debug.Log($"Camera Angle: {(isSecondaryCamera ? "Secondary" : "Primary")}");
                UpdateActiveCamera();
            }
        }

        // Cycle sub-content
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
        // 1. Disable all cameras
        if (mainCamera) mainCamera.gameObject.SetActive(false);
        if (playerRig)
        {
            if (playerRig.primaryCamera) playerRig.primaryCamera.gameObject.SetActive(false);
            if (playerRig.secondaryCamera) playerRig.secondaryCamera.gameObject.SetActive(false);
        }
        foreach (var rig in enemyRigs)
        {
            if (rig.primaryCamera) rig.primaryCamera.gameObject.SetActive(false);
            if (rig.secondaryCamera) rig.secondaryCamera.gameObject.SetActive(false);
        }

        // 2. Select New Camera
        Camera targetCam = null;

        switch (currentViewMode)
        {
            case ViewMode.Main:
                targetCam = mainCamera;
                break;
            case ViewMode.Player:
                if (playerRig) targetCam = playerRig.GetCamera(isSecondaryCamera);
                break;
            case ViewMode.Enemy:
                if (enemyRigs.Count > 0) targetCam = enemyRigs[currentEnemyIndex].GetCamera(isSecondaryCamera);
                break;
        }

        // 3. Activate and Move Quad
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

    // Logic to combine different FOVs based on the state
    private HashSet<Node> GetCurrentVisibleNodes()
    {
        HashSet<Node> nodes = new HashSet<Node>();

        if (currentViewMode == ViewMode.Player)
        {
            if(playerRig) nodes.UnionWith(playerRig.fov.visibleNodes);
        }
        else if (currentViewMode == ViewMode.Enemy)
        {
            if (enemyRigs.Count > 0) nodes.UnionWith(enemyRigs[currentEnemyIndex].fov.visibleNodes);
        }
        else if (currentViewMode == ViewMode.Main)
        {
            // Handle cycling in Main mode
            if (mainFowState == MainFowType.Combined || mainFowState == MainFowType.PlayerOnly)
            {
                if(playerRig) nodes.UnionWith(playerRig.fov.visibleNodes);
            }
            
            if (mainFowState == MainFowType.Combined || mainFowState == MainFowType.EnemiesOnly)
            {
                foreach (var rig in enemyRigs)
                {
                    nodes.UnionWith(rig.fov.visibleNodes);
                }
            }
        }

        return nodes;
    }

    private void UpdateVisibilityTexture(HashSet<Node> visibleNodes)
    {
        Node[,] allNodes = grid.GetGridNodes();
        if (allNodes == null) return;

        byte visible = 255;
        byte hidden = 0;

        for (int y = 0; y < gridSizeY; y++) {
            for (int x = 0; x < gridSizeX; x++) {
                if (visibleNodes.Contains(allNodes[x, y])) {
                    textureColors[x + y * gridSizeX].a = visible;
                } else {
                    textureColors[x + y * gridSizeX].a = hidden;
                }
            }
        }

        visibilityTexture.SetPixels32(textureColors);
        visibilityTexture.Apply(false);
    }
    
    private void UpdateEntityTransparency(HashSet<Node> visibleNodes)
    {
        // Helper to determine if a specific rig is the one currently being controlled/spectated
        bool IsSpectating(EntityCameraRig rig)
        {
            if (currentViewMode == ViewMode.Player && rig == playerRig) return true;
            if (currentViewMode == ViewMode.Enemy && enemyRigs.Count > 0 && rig == enemyRigs[currentEnemyIndex]) return true;
            return false;
        }

        // Update Player
        if (playerRig)
        {
            if (IsSpectating(playerRig))
            {
                playerRig.SetMaterialAlpha(1.0f);
            }
            else
            {
                Node n = grid.NodeFromWorldPoint(playerRig.transform.position);
                playerRig.SetMaterialAlpha(visibleNodes.Contains(n) ? 1.0f : 0.0f);
            }
        }

        // Update Enemies
        foreach (var rig in enemyRigs)
        {
            if (IsSpectating(rig))
            {
                rig.SetMaterialAlpha(1.0f);
            }
            else
            {
                Node n = grid.NodeFromWorldPoint(rig.transform.position);
                rig.SetMaterialAlpha(visibleNodes.Contains(n) ? 1.0f : 0.0f);
            }
        }
    }
}