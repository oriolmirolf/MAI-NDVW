using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;

public class AgentController : Agent
{
    public bool FacingLeft { get { return facingLeft; } }

    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float dashSpeed = 4f;
    [SerializeField] private TrailRenderer myTrailRenderer;
    [SerializeField] private Transform weaponCollider;
    
    [Header("Arena Bounds (for observation normalization)")]
    [Tooltip("If set, arena bounds will be fetched from TrainingManager. Otherwise, use local values.")]
    [SerializeField] private TrainingManager trainingManager;
    private Vector2 arenaMin;
    private Vector2 arenaMax;

    private Rigidbody2D rb;
    private Animator myAnimator;
    private SpriteRenderer mySpriteRender;
    private Knockback knockback;
    private AgentHealth agentHealth;
    private AgentStamina agentStamina;
    private AgentWeapons agentWeapons;
    private float startingMoveSpeed;

    private bool facingLeft = false;
    private bool isDashing = false;
    private float currentAimAngle = 0f; // Aim angle in degrees (-180 to 180)

    private AgentController enemyAgent;

    public float CurrentAimAngle => currentAimAngle;

    public override void Initialize()
    {
        base.Initialize();
        rb = GetComponent<Rigidbody2D>();
        myAnimator = GetComponent<Animator>();
        mySpriteRender = GetComponent<SpriteRenderer>();
        knockback = GetComponent<Knockback>();
        agentHealth = GetComponent<AgentHealth>();
        agentStamina = GetComponent<AgentStamina>();
        agentWeapons = GetComponent<AgentWeapons>();
        startingMoveSpeed = moveSpeed;

        // Fetch arena bounds from TrainingManager if available
        if (trainingManager != null)
        {
            var bounds = trainingManager.GetArenaBounds();
            arenaMin = bounds.min;
            arenaMax = bounds.max;
        }
    }

    public void SetEnemyAgent(AgentController enemy)
    {
        enemyAgent = enemy;
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent's state at the beginning of an episode
        StopAllCoroutines(); // Stop any running coroutines (e.g., dash)
        isDashing = false;
        moveSpeed = startingMoveSpeed;
        if (myTrailRenderer) myTrailRenderer.emitting = false;
        
        // Reset health
        if (agentHealth) agentHealth.ResetHealth();
        
        // Reset stamina
        if (agentStamina) agentStamina.ResetStamina();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Total observations: 18 floats
        // - Own position (normalized): 2
        // - Distance to enemy: 2  
        // - Own velocity: 2
        // - Enemy velocity: 2
        // - Own health (normalized): 1
        // - Enemy health (normalized): 1
        // - Own stamina (normalized): 1
        // - Own weapon index (normalized): 1
        // - Enemy weapon index (normalized): 1
        // - Own attack cooldown (normalized): 1
        // - Enemy attack cooldown (normalized): 1
        // - Own aim angle (normalized): 1
        // - Enemy aim angle (normalized): 1
        // - Own angle to enemy (normalized): 1
        
        if (!enemyAgent)
        {
            Debug.LogWarning("Enemy agent not set for observations.");
            // Add zeros for all 18 observations
            for (int i = 0; i < 18; i++)
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        // 1. Own position normalized to arena bounds (2 floats)
        // Normalized to [-1, 1] range for better learning
        Vector2 normalizedOwnPos = NormalizePosition(transform.position);
        sensor.AddObservation(normalizedOwnPos);

        // 2. Distance to enemy agent, normalized by arena size (2 floats)
        // This gives explicit range information for combat decisions
        Vector2 distanceToEnemy = (Vector2)(enemyAgent.transform.position - transform.position);
        Vector2 arenaSize = arenaMax - arenaMin;
        Vector2 normalizedDistance = new Vector2(
            distanceToEnemy.x / arenaSize.x,
            distanceToEnemy.y / arenaSize.y
        );
        sensor.AddObservation(normalizedDistance);

        // 3. Own velocity normalized (2 floats)
        // Helps agent understand its current movement state
        float maxSpeed = moveSpeed * dashSpeed; // Max possible speed during dash
        sensor.AddObservation(rb.velocity / maxSpeed);

        // 4. Enemy velocity normalized (2 floats)
        // Helps predict enemy movement
        sensor.AddObservation(enemyAgent.rb.velocity / maxSpeed);

        // 5. Own health normalized [0, 1] (1 float)
        sensor.AddObservation(agentHealth.GetNormalizedHealth());

        // 6. Enemy health normalized [0, 1] (1 float)
        sensor.AddObservation(enemyAgent.agentHealth.GetNormalizedHealth());

        // 7. Own stamina normalized [0, 1] (1 float)
        sensor.AddObservation(agentStamina.GetNormalizedStamina());

        // 8. Own weapon index normalized [0, 1] (1 float)
        sensor.AddObservation(agentWeapons.GetNormalizedWeaponIndex());

        // 9. Enemy weapon index normalized [0, 1] (1 float)
        sensor.AddObservation(enemyAgent.agentWeapons.GetNormalizedWeaponIndex());

        // 10. Own attack cooldown normalized [0, 1] (1 float)
        // 0 = ready to attack, 1 = just attacked
        sensor.AddObservation(agentWeapons.GetNormalizedCooldownRemaining());

        // 11. Enemy attack cooldown normalized [0, 1] (1 float)
        // Knowing enemy cooldown helps time attacks/dodges
        sensor.AddObservation(enemyAgent.agentWeapons.GetNormalizedCooldownRemaining());

        // 12. Own aim angle normalized [-1, 1] (1 float)
        // Helps agent track where it's currently aiming
        sensor.AddObservation(currentAimAngle / 180f);

        // 13. Enemy aim angle normalized [-1, 1] (1 float)
        // Helps predict/dodge enemy attacks
        sensor.AddObservation(enemyAgent.currentAimAngle / 180f);

        // 14. Own angle to enemy normalized [-1, 1] (1 float)
        // The angle from this agent to the enemy, centered so 0 = up
        float angleToEnemy = GetAngleToEnemy();
        sensor.AddObservation(angleToEnemy / 180f);
    }

    private Vector2 NormalizePosition(Vector3 position)
    {
        return new Vector2(
            Mathf.InverseLerp(arenaMin.x, arenaMax.x, position.x) * 2f - 1f,
            Mathf.InverseLerp(arenaMin.y, arenaMax.y, position.y) * 2f - 1f
        );
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // === DISCRETE ACTIONS ===
        
        // Movement (discrete: 0=idle, 1=up, 2=down, 3=left, 4=right)
        int moveAction = actions.DiscreteActions[0];
        Vector2 moveDirection = Vector2.zero;
        if (moveAction == 1) moveDirection = Vector2.up;
        else if (moveAction == 2) moveDirection = Vector2.down;
        else if (moveAction == 3) moveDirection = Vector2.left;
        else if (moveAction == 4) moveDirection = Vector2.right;
        
        Move(moveDirection);

        // Combined weapon attack action (0 = no attack, 1+ = attack with weapon index-1)
        // This action has a cooldown that prevents constant attacking/switching
        int weaponAttackAction = actions.DiscreteActions[1];
        if (weaponAttackAction > 0)
        {
            int targetWeaponIndex = weaponAttackAction - 1;
            agentWeapons.AttackWithWeapon(targetWeaponIndex, this);
        }

        // Dashing (discrete: 0=no dash, 1=dash)
        int dashAction = actions.DiscreteActions[2];
        if (dashAction == 1)
        {
            Dash();
        }

        // === CONTINUOUS ACTIONS ===
        
        // Aiming angle (continuous: -1 to 1, mapped so that 0 = up)
        // This ensures left/right are symmetric around 0:
        //   0 = up (90°), +0.5 = left (180°), -0.5 = right (0°), ±1 = down (-90°)
        float aimInput = actions.ContinuousActions[0];
        currentAimAngle = (aimInput * 180f) - 90f; // Convert [-1, 1] to [270°, -90°] with 0 = up
        
        // Normalize angle to [-180, 180] range
        if (currentAimAngle > 180f) currentAimAngle -= 360f;
        if (currentAimAngle < -180f) currentAimAngle += 360f;
        
        // Update facing direction based on aim angle
        // Face left when aiming into the left hemisphere (90° to 180° or -90° to -180°)
        facingLeft = currentAimAngle > 0f;
        mySpriteRender.flipX = facingLeft;
    }

    private void FixedUpdate()
    {
        // Facing direction is now controlled by aim angle in OnActionReceived
        RequestDecision();
    }

    private void Move(Vector2 movement)
    {
        if (knockback.GettingKnockedBack || agentHealth.IsDead) { return; }

        rb.MovePosition(rb.position + movement * (moveSpeed * Time.fixedDeltaTime));
        myAnimator.SetFloat("moveX", movement.x);
        myAnimator.SetFloat("moveY", movement.y);
    }

    private void AdjustPlayerFacingDirection()
    {
        if (enemyAgent.transform.position.x < transform.position.x)
        {
            mySpriteRender.flipX = true;
            facingLeft = true;
        }
        else
        {
            mySpriteRender.flipX = false;
            facingLeft = false;
        }
    }

    private void Dash()
    {
        if (!isDashing && agentStamina.CurrentStamina > 0)
        {
            agentStamina.UseStamina();
            isDashing = true;
            moveSpeed *= dashSpeed;
            myTrailRenderer.emitting = true;
            StartCoroutine(EndDashRoutine());
        }
        else
        {
            trainingManager.PenalizeDashSpamming(this);
        }
    }

    public void PenalizeAttackSpamming()
    {
        trainingManager.PenalizeAttackSpamming(this);
    }

    private IEnumerator EndDashRoutine()
    {
        float dashTime = .2f;
        float dashCD = .25f;
        yield return new WaitForSeconds(dashTime);
        moveSpeed = startingMoveSpeed;
        myTrailRenderer.emitting = false;
        yield return new WaitForSeconds(dashCD);
        isDashing = false;
    }

    public Transform GetWeaponCollider()
    {
        return weaponCollider;
    }

    public float GetAngleToEnemy()
    {
        if (!enemyAgent) return 0f;
        
        Vector2 directionToEnemy = (Vector2)(enemyAgent.transform.position - transform.position);
        // Atan2 returns angle where 0 = right, 90 = up
        // We want 0 = up, so we subtract 90 degrees
        float angle = Mathf.Atan2(directionToEnemy.y, directionToEnemy.x) * Mathf.Rad2Deg - 90f;
        
        // Normalize to [-180, 180]
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        
        return angle;
    }

    public float GetAimAngleDifferenceToEnemy()
    {
        float angleToEnemy = GetAngleToEnemy();
        float diff = Mathf.Abs(currentAimAngle - angleToEnemy);
        
        // Handle wraparound (e.g., -170 vs 170 should be 20 degrees apart, not 340)
        if (diff > 180f) diff = 360f - diff;
        
        return diff;
    }
}
