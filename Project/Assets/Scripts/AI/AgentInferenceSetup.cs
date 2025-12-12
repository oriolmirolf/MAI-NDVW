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
    
    [Header("Wait For Player")]
    [Tooltip("If true, the boss will stand still until the player enters the room.")]
    [SerializeField] private bool waitForPlayer = false;
    [Tooltip("The room bounds to check for player entry.")]
    [SerializeField] private RectInt roomBounds;
    [Tooltip("How often to check if player has entered (in seconds).")]
    [SerializeField] private float playerCheckInterval = 0.2f;

    [Header("Boss Settings")]
    [Tooltip("If true, spawns a chapter exit portal when this agent dies.")]
    [SerializeField] private bool isBoss = false;
    
    private AgentController agentController;
    private AgentHealth agentHealth;
    private Unity.MLAgents.Agent mlAgent;
    private Rigidbody2D rb;
    private bool isDying = false;
    private bool isWaitingForPlayer = false;
    
    private void Awake()
    {
        agentController = GetComponent<AgentController>();
        agentHealth = GetComponent<AgentHealth>();
        mlAgent = GetComponent<Unity.MLAgents.Agent>();
        rb = GetComponent<Rigidbody2D>();
        
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
        
        // Handle waiting for player
        if (waitForPlayer)
        {
            isWaitingForPlayer = true;
            DisableAgent();
            StartCoroutine(WaitForPlayerCoroutine());
        }
        else if (autoTargetPlayer && agentController != null)
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
        // Immediately disable all colliders to prevent blocking the exit portal
        var colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Make boss invisible immediately
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // Spawn death VFX if assigned
        if (deathVFXPrefab != null)
        {
            Transform dropsParent = FindObjectOfType<BSPMSTDungeonGenerator>()?.DropsParent;
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity, dropsParent);
        }

        // Optionally spawn pickups (if PickUpSpawner is present)
        var pickupSpawner = GetComponent<PickUpSpawner>();
        if (pickupSpawner != null)
        {
            pickupSpawner.DropItems();
        }

        // If this is a boss, trigger the boss defeated logic
        if (isBoss)
        {
            var dungeonManager = DungeonRunManager.Instance;
            if (dungeonManager != null)
            {
                dungeonManager.OnBossDefeated();
                Debug.Log("Boss defeated! Chapter completed and exit portal spawned.");
            }
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
    
    /// <summary>
    /// Configure the agent to wait for player entry before activating
    /// </summary>
    public void SetWaitForPlayer(bool wait, RectInt bounds)
    {
        waitForPlayer = wait;
        roomBounds = bounds;
    }

    /// <summary>
    /// Mark this agent as a boss (spawns chapter exit portal on death)
    /// </summary>
    public void SetIsBoss(bool boss)
    {
        isBoss = boss;
    }

    private void DisableAgent()
    {
        // Disable ML Agent decision making
        if (mlAgent != null)
        {
            mlAgent.enabled = false;
        }
        
        // Freeze the rigidbody
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
    
    private void EnableAgent()
    {
        // Enable ML Agent decision making
        if (mlAgent != null)
        {
            mlAgent.enabled = true;
        }
        
        // Unfreeze the rigidbody (keep rotation frozen as typical for 2D)
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        
        // Set player as target
        if (autoTargetPlayer && agentController != null)
        {
            SetupPlayerTarget();
        }
        
        isWaitingForPlayer = false;
    }
    
    private System.Collections.IEnumerator WaitForPlayerCoroutine()
    {
        WaitForSeconds waitInterval = new WaitForSeconds(playerCheckInterval);
        
        while (isWaitingForPlayer)
        {
            if (IsPlayerInRoom())
            {
                Debug.Log("Player entered boss room - activating boss!");
                EnableAgent();
                yield break;
            }
            
            yield return waitInterval;
        }
    }
    
    private bool IsPlayerInRoom()
    {
        if (PlayerController.Instance == null)
            return false;
        
        Vector3 playerPos = PlayerController.Instance.transform.position;
        
        return playerPos.x >= roomBounds.xMin && 
               playerPos.x <= roomBounds.xMax &&
               playerPos.y >= roomBounds.yMin && 
               playerPos.y <= roomBounds.yMax;
    }
}
