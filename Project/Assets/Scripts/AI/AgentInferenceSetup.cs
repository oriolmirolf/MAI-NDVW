using UnityEngine;

/// <summary>
/// Helper component for configuring ML Agents in inference mode (when fighting the player).
/// Attach this to Agent prefabs that will be spawned as enemies in the game.
/// This component handles:
/// - Setting the player as the enemy target
/// - Configuring arena bounds for observation normalization
/// - Optional: Destroying the agent when it dies
/// </summary>
public class AgentInferenceSetup : MonoBehaviour
{
    [Header("Target Configuration")]
    [Tooltip("If true, automatically sets PlayerController as the enemy target on Start.")]
    [SerializeField] private bool autoTargetPlayer = true;
    
    [Header("Arena Bounds")]
    [Tooltip("If true, arena bounds will be set from these values. If false, agent uses its own defaults.")]
    [SerializeField] private bool overrideArenaBounds = false;
    [SerializeField] private Vector2 arenaBoundsMin = new Vector2(-11f, -6f);
    [SerializeField] private Vector2 arenaBoundsMax = new Vector2(11f, 7f);
    
    [Header("Death Handling")]
    [Tooltip("If true, destroys the agent GameObject when it dies.")]
    [SerializeField] private bool destroyOnDeath = true;
    [Tooltip("Delay before destroying the agent after death (allows death animation to play).")]
    [SerializeField] private float deathDestroyDelay = 2f;
    
    [Header("Optional VFX")]
    [Tooltip("Prefab to spawn when the agent dies.")]
    [SerializeField] private GameObject deathVFXPrefab;
    
    private AgentController agentController;
    private AgentHealth agentHealth;
    private bool isDying = false;
    
    private void Awake()
    {
        agentController = GetComponent<AgentController>();
        agentHealth = GetComponent<AgentHealth>();
        
        if (agentController == null)
        {
            Debug.LogError("AgentInferenceSetup: No AgentController found on this GameObject!");
            enabled = false;
        }
    }
    
    private void Start()
    {
        // Set arena bounds if overriding
        if (overrideArenaBounds && agentController != null)
        {
            agentController.SetArenaBounds(arenaBoundsMin, arenaBoundsMax);
        }
        
        // Set player as enemy target
        if (autoTargetPlayer && agentController != null)
        {
            // Delay slightly to ensure PlayerController singleton is initialized
            Invoke(nameof(SetupPlayerTarget), 0.1f);
        }
    }
    
    private void SetupPlayerTarget()
    {
        agentController.SetPlayerAsEnemy();
    }
    
    private void Update()
    {
        // Check for death and handle it
        if (destroyOnDeath && agentHealth != null && agentHealth.IsDead && !isDying)
        {
            isDying = true;
            HandleDeath();
        }
    }
    
    private void HandleDeath()
    {
        // Spawn death VFX if assigned
        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }
        
        // Optionally spawn pickups (if PickUpSpawner is present)
        var pickupSpawner = GetComponent<PickUpSpawner>();
        if (pickupSpawner != null)
        {
            pickupSpawner.DropItems();
        }
        
        // Destroy after delay
        Destroy(gameObject, deathDestroyDelay);
    }
    
    /// <summary>
    /// Set the arena bounds programmatically (useful for dynamic room generation)
    /// </summary>
    public void SetArenaBounds(Vector2 min, Vector2 max)
    {
        arenaBoundsMin = min;
        arenaBoundsMax = max;
        
        if (agentController != null)
        {
            agentController.SetArenaBounds(min, max);
        }
    }
    
    /// <summary>
    /// Manually set a custom target (if not using the player)
    /// </summary>
    public void SetCustomTarget(ICombatTarget target)
    {
        if (agentController != null)
        {
            agentController.SetEnemyTarget(target);
        }
    }
}
