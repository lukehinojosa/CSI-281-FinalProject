using UnityEngine;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    public int maxLives = 3;
    private int currentLives;
    
    // Invincibility cooldown
    private float damageCooldown = 1.0f; 
    private float lastDamageTime;
    
    [Header("Visual Feedback")]
    public Color damageColor = Color.red;
    [Tooltip("How fast the color flashes. Higher is faster.")]
    public float flashSpeed = 10f;

    private Renderer playerRenderer;
    private Color originalColor;
    private Coroutine flashCoroutine;

    private GameManager gameManager;

    void Start()
    {
        currentLives = maxLives;
        gameManager = FindObjectOfType<GameManager>();
        
        // Cache the renderer and the starting color
        playerRenderer = GetComponentInChildren<Renderer>();
        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }
    }

    public void TakeDamage()
    {
        if (GameSettings.CurrentMode == GameMode.Demo) return; // Invincible in Demo
        if (Time.time < lastDamageTime + damageCooldown) return;

        currentLives--;
        lastDamageTime = Time.time;
        Debug.Log($"Player Hit! Lives remaining: {currentLives}");
        
        if (playerRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlash());
        }

        if (currentLives <= 0)
        {
            gameManager.GameOver(false); // Player Lost
            gameObject.SetActive(false); // Hide player
        }
    }
    
    IEnumerator DamageFlash()
    {
        float t = 0;

        // Fade to Red
        while (t < 1)
        {
            t += Time.deltaTime * flashSpeed;
            playerRenderer.material.color = Color.Lerp(originalColor, damageColor, t);
            yield return null;
        }

        t = 0;

        // Fade back to original color
        while (t < 1)
        {
            t += Time.deltaTime * flashSpeed;
            playerRenderer.material.color = Color.Lerp(damageColor, originalColor, t);
            yield return null;
        }
        
        playerRenderer.material.color = originalColor;
    }

    // Destroy Energy Stations
    void OnTriggerEnter(Collider other)
    {
        if (GameSettings.CurrentMode == GameMode.Demo) return;

        if (other.CompareTag("EnergyStation"))
        {
            Debug.Log("Player destroyed an Energy Station!");
            
            Destroy(other.gameObject);
            
            // Update the grid to make this node walkable
            Grid grid = FindObjectOfType<Grid>();
            if (grid != null) grid.UpdateGridObstacles();
        }
    }
}