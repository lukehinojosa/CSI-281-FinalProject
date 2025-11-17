using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class VisibilityManager : MonoBehaviour
{
    [Header("Field of View Targets")]
    [Tooltip("The FieldOfView component on the player.")]
    public FieldOfView playerFov;
    [Tooltip("The FieldOfView component on the enemy.")]
    public FieldOfView enemyFov;

    [Header("Setup")]
    [Tooltip("A reference to the Grid in the scene.")]
    public Grid grid;
    [Tooltip("Optional: A RawImage UI element to display the debug texture.")]
    public RawImage debugImage;

    private Texture2D visibilityTexture;
    private Color32[] textureColors;
    private int gridSizeX;
    private int gridSizeY;

    private Material playerMaterial;
    private Material enemyMaterial;

    private bool isEnemySpectateMode = false;

    void Start()
    {
        if (playerFov == null || enemyFov == null || grid == null)
        {
            Debug.LogError("Player FOV, Enemy FOV, or Grid is not assigned!");
            enabled = false;
            return;
        }

        // Get the renderers and cache their materials. Using .material creates an instance.
        Renderer playerRenderer = playerFov.GetComponent<Renderer>();
        Renderer enemyRenderer = enemyFov.GetComponent<Renderer>();
        if(playerRenderer != null) playerMaterial = playerRenderer.material;
        if(enemyRenderer != null) enemyMaterial = enemyRenderer.material;

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

        UpdateVisibilityTexture();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            isEnemySpectateMode = !isEnemySpectateMode;
            Debug.Log("Spectate Mode Toggled: " + (isEnemySpectateMode ? "ON (Viewing from Enemy)" : "OFF (Viewing from Player)"));
        }
    }

    void LateUpdate()
    {
        // First, update the main fog of war texture
        UpdateVisibilityTexture();
        
        // Then, update the transparency of the characters based on the new visibility
        UpdateCharacterVisibility();

        if (debugImage != null)
        {
            debugImage.texture = visibilityTexture;
        }
    }

    private void UpdateVisibilityTexture()
    {
        HashSet<Node> activeVisibleNodes = isEnemySpectateMode ? enemyFov.visibleNodes : playerFov.visibleNodes;
        Node[,] allNodes = grid.GetGridNodes();
        if (allNodes == null || activeVisibleNodes == null) return;

        byte visible = 255;
        byte hidden = 0;

        for (int y = 0; y < gridSizeY; y++) {
            for (int x = 0; x < gridSizeX; x++) {
                if (activeVisibleNodes.Contains(allNodes[x, y])) {
                    textureColors[x + y * gridSizeX].a = visible;
                } else {
                    textureColors[x + y * gridSizeX].a = hidden;
                }
            }
        }

        visibilityTexture.SetPixels32(textureColors);
        visibilityTexture.Apply(false);
    }
    
    // Controls character transparency
    private void UpdateCharacterVisibility()
    {
        if (isEnemySpectateMode)
        {
            // Viewing from the enemy's perspective
            SetMaterialAlpha(enemyMaterial, 1.0f); // Enemy always sees itself

            Node playerNode = grid.NodeFromWorldPoint(playerFov.transform.position);
            bool isPlayerVisible = enemyFov.visibleNodes.Contains(playerNode);
            SetMaterialAlpha(playerMaterial, isPlayerVisible ? 1.0f : 0.0f);
        }
        else
        {
            // Viewing from the player's perspective
            SetMaterialAlpha(playerMaterial, 1.0f); // Player always sees themselves

            Node enemyNode = grid.NodeFromWorldPoint(enemyFov.transform.position);
            bool isEnemyVisible = playerFov.visibleNodes.Contains(enemyNode);
            SetMaterialAlpha(enemyMaterial, isEnemyVisible ? 1.0f : 0.0f);
        }
    }

    // Helper function to change a material's alpha
    private void SetMaterialAlpha(Material mat, float alpha)
    {
        if (mat == null) return;
        Color color = mat.color;
        color.a = alpha;
        mat.color = color;
    }
}