using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class PlayerAgent : Agent
{
    public PlayerAgent enemyAgent;
    public event Action OnEpisodeRequested;
    
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private Transform weaponCollider;
    [SerializeField] private int teamId = 0; // For ML-Agents self-play (set different values per side)
    [SerializeField] private bool useDistanceShaping = true;
    [SerializeField] private float approachReward = 0.001f;
    [SerializeField] private float retreatPenalty = -0.001f;
    [SerializeField] private float idlePenaltyPerStep = -0.0005f;

    private Rigidbody2D rb;
    private AgentHealth agentHealth;
    private AgentWeapon agentWeapon;
    private Vector2 movement;
    private SpriteRenderer mySpriteRender;
    private float lastDistanceToEnemy;
    private int idleSteps;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        agentHealth = GetComponent<AgentHealth>();
        agentWeapon = GetComponent<AgentWeapon>();
        mySpriteRender = GetComponent<SpriteRenderer>();

        // Optionally set team id if BehaviorParameters component exists
        var bp = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (bp != null)
        {
            bp.TeamId = teamId;
        }
    }

    public override void OnEpisodeBegin()
    {
        // The TrainingManager now handles resetting the agent's state
        if (enemyAgent != null)
        {
            lastDistanceToEnemy = Vector2.Distance(transform.localPosition, enemyAgent.transform.localPosition);
        }
        idleSteps = 0;
    }

    public bool IsEpisodeFinished()
    {
        return agentHealth.IsDead;
    }

    public void RequestEpisodeEnd()
    {
        OnEpisodeRequested?.Invoke();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Could change positions to relative distance and velocity
        // Agent's position (2 floats)
        sensor.AddObservation(transform.localPosition);

        // Enemy's position (2 floats)
        sensor.AddObservation(enemyAgent != null ? enemyAgent.transform.localPosition : Vector3.zero);

        // Agent's health (1 float)
        sensor.AddObservation((float)agentHealth.CurrentHealth / agentHealth.MaxHealth);

        // Enemy's health (1 float)
        var enemyHealth = enemyAgent != null ? enemyAgent.GetComponent<AgentHealth>() : null;
        sensor.AddObservation(enemyHealth != null ? (float)enemyHealth.CurrentHealth / enemyHealth.MaxHealth : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Movement
        int moveAction = actions.DiscreteActions[0];
        movement = Vector2.zero;

        switch (moveAction)
        {
            case 1: movement.y = 1; break; // Up
            case 2: movement.y = -1; break; // Down
            case 3: movement.x = -1; break; // Left
            case 4: movement.x = 1; break; // Right
        }

        if (movement == Vector2.zero)
        {
            idleSteps++;
            AddReward(idlePenaltyPerStep); // discourage doing nothing
        }
        else
        {
            idleSteps = 0;
        }

        // Attack
        int attackAction = actions.DiscreteActions[1];
        if (attackAction > 0)
        {
            agentWeapon.EquipWeapon(attackAction - 1); // Weapons 0, 1, 2
            if (agentWeapon.CurrentActiveWeapon != null)
            {
                agentWeapon.Attack();
            }
        }

        // Small negative reward per step to encourage faster completion
        AddReward(-0.001f);
    }

    private void FixedUpdate()
    {
        if (agentHealth != null && !agentHealth.IsDead)
        {
            Move();
            AdjustPlayerFacingDirection();

            if (useDistanceShaping && enemyAgent != null && !enemyAgent.agentHealth.IsDead)
            {
                float currentDistance = Vector2.Distance(transform.localPosition, enemyAgent.transform.localPosition);
                if (currentDistance < lastDistanceToEnemy - 0.01f)
                {
                    AddReward(approachReward);
                }
                else if (currentDistance > lastDistanceToEnemy + 0.01f)
                {
                    AddReward(retreatPenalty);
                }
                lastDistanceToEnemy = currentDistance;
            }
        }
    }

    private void Move()
    {
        rb.MovePosition(rb.position + movement.normalized * (moveSpeed * Time.fixedDeltaTime));
    }

    private void AdjustPlayerFacingDirection()
    {
        if (enemyAgent != null)
        {
            if (enemyAgent.transform.position.x < transform.position.x)
            {
                mySpriteRender.flipX = true;
            }
            else
            {
                mySpriteRender.flipX = false;
            }
        }
    }

    public Transform GetWeaponCollider()
    {
        return weaponCollider;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Weapon"))
        {
            IWeapon weapon = col.gameObject.GetComponent<IWeapon>();
            if (weapon != null)
            {
                PlayerAgent attacker = col.gameObject.GetComponentInParent<PlayerAgent>();
                if (attacker != null && attacker != this)
                {
                    agentHealth.TakeDamage(weapon.GetWeaponInfo().weaponDamage, attacker);
                }
            }
        }
    }
}
