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

    private AgentController enemyAgent;

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
    }

    public void SetEnemyAgent(AgentController enemy)
    {
        enemyAgent = enemy;
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent's state at the beginning of an episode
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!enemyAgent)
        {
            sensor.AddObservation(Vector2.zero); // Distance
            sensor.AddObservation(Vector2.zero); // Relative velocity
            sensor.AddObservation(0f); // Own health
            sensor.AddObservation(0f); // Enemy health
            return;
        }

        // 1. Distance to enemy agent (2 floats)
        sensor.AddObservation((Vector2)(enemyAgent.transform.position - transform.position));

        // 2. Relative velocity to enemy agent (2 floats)
        sensor.AddObservation(rb.velocity - enemyAgent.rb.velocity);

        // 3. Own health (1 float)
        sensor.AddObservation((float)agentHealth.GetCurrentHealth());

        // 4. Enemy health (1 float)
        sensor.AddObservation((float)enemyAgent.agentHealth.GetCurrentHealth());
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Movement
        int moveAction = actions.DiscreteActions[0];
        Vector2 moveDirection = Vector2.zero;
        if (moveAction == 1) moveDirection = Vector2.up;
        else if (moveAction == 2) moveDirection = Vector2.down;
        else if (moveAction == 3) moveDirection = Vector2.left;
        else if (moveAction == 4) moveDirection = Vector2.right;
        
        Move(moveDirection);

        // Attacking
        int attackAction = actions.DiscreteActions[1];
        if (attackAction > 0)
        {
            agentWeapons.SwitchWeapon(attackAction - 1, this);
            agentWeapons.Attack(this);
        }

        // Dashing
        int dashAction = actions.DiscreteActions[2];
        if (dashAction == 1)
        {
            Dash();
        }
    }

    private void FixedUpdate()
    {
        if (enemyAgent)
        {
            AdjustPlayerFacingDirection();
        }
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
}
