using UnityEngine;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    [SerializeField] private AgentController agent1;
    [SerializeField] private AgentController agent2;

    // Statistics tracking (per reporting window, not cumulative)
    private StatsRecorder statsRecorder;

    [Header("Reward Parameters")]
    [Tooltip("Penalty applied every time step.")]
    public float timePenalty = -0.001f;
    [Tooltip("Reward for victory.")]
    public float victoryReward = 1f;
    [Tooltip("Penalty for defeat.")]
    public float defeatPenalty = -1f;
    [Tooltip("Reward for hitting opponent.")]
    public float hitReward = 0.5f;
    [Tooltip("Penalty for being hit.")]
    public float hitPenalty = -0.5f;
    [Tooltip("Penalty for attempting to attack while on cooldown.")]
    public float attackSpammingPenalty = -0.001f;
    [Tooltip("Penalty for attempting to dash while on cooldown.")]
    public float dashSpammingPenalty = -0.001f;

    [Header("Proximity Reward (Early Training)")]
    [Tooltip("Enable proximity reward to encourage agents to approach each other. Disable once agents learn to fight.")]
    public bool enableProximityReward = true;
    [Tooltip("Maximum reward per step when agents are very close.")]
    public float proximityRewardScale = 0.001f;

    [Header("Aiming Accuracy Reward")]
    [Tooltip("Enable aiming accuracy reward to encourage agents to aim at each other.")]
    public bool enableAimingReward = true;
    [Tooltip("Maximum reward per step when aiming perfectly at the enemy.")]
    public float aimingRewardScale = 0.005f;
    [Tooltip("Angle threshold in degrees. Agent receives reward when aiming within this angle of the enemy.")]
    public float aimingAngleThreshold = 20f;

    [Header("Episode Settings")]
    [Tooltip("Maximum steps per episode. Episode ends if this limit is reached.")]
    public int maxEpisodeSteps = 10000;
    private int currentEpisodeSteps = 0;

    [Header("Spawning Settings")]
    [Tooltip("If true, agents spawn at random positions each episode. If false, they spawn at their initial positions.")]
    public bool randomSpawning = false;
    [Tooltip("Minimum distance between agents when spawning randomly.")]
    public float minSpawnDistance = 5f;

    [Header("Arena Bounds")]
    [Tooltip("Minimum corner of the arena (bottom-left).")]
    public Vector2 arenaMin = new Vector2(-11f, -6f);
    [Tooltip("Maximum corner of the arena (top-right).")]
    public Vector2 arenaMax = new Vector2(11f, 7f);

    private Vector3 agent1StartPos;
    private Quaternion agent1StartRot;
    private Vector3 agent2StartPos;
    private Quaternion agent2StartRot;
    private AgentHealth agent1Health;
    private AgentHealth agent2Health;
    private Rigidbody2D agent1Rb;
    private Rigidbody2D agent2Rb;

    private void Awake()
    {
        agent1.SetEnemyAgent(agent2);
        agent2.SetEnemyAgent(agent1);

        agent1StartPos = agent1.transform.position;
        agent1StartRot = agent1.transform.rotation;
        agent2StartPos = agent2.transform.position;
        agent2StartRot = agent2.transform.rotation;

        agent1Health = agent1.GetComponent<AgentHealth>();
        agent2Health = agent2.GetComponent<AgentHealth>();
        agent1Rb = agent1.GetComponent<Rigidbody2D>();
        agent2Rb = agent2.GetComponent<Rigidbody2D>();

        // Initialize stats recorder for TensorBoard logging
        statsRecorder = Academy.Instance.StatsRecorder;
    }

    private void FixedUpdate()
    {
        currentEpisodeSteps++;

        // Time penalty
        agent1.AddReward(timePenalty);
        agent2.AddReward(timePenalty);

        // Proximity reward to encourage agents to approach each other
        if (enableProximityReward)
        {
            Vector2 vectorToAgent2 = agent2.transform.position - agent1.transform.position;
            Vector2 dirToAgent2 = vectorToAgent2.normalized;

            // Reward based on velocity towards enemy (dot product)
            float dot1 = Vector2.Dot(agent1Rb.velocity, dirToAgent2);
            agent1.AddReward(dot1 * proximityRewardScale);
            // if (agent1Rb.velocity != Vector2.zero)
            // {
            //     Debug.Log($"Agent 1 Velocity: {agent1Rb.velocity}, Dir to Agent 2: {dirToAgent2}, Dot: {dot1:F4}");
            //     Debug.Log($"[Proximity Reward] Agent1: +{(dot1 * proximityRewardScale):F4} (Total: {agent1.GetCumulativeReward():F4})");
            // }

            // For agent 2, direction is opposite
            Vector2 dirToAgent1 = -dirToAgent2;
            float dot2 = Vector2.Dot(agent2Rb.velocity, dirToAgent1);
            agent2.AddReward(dot2 * proximityRewardScale);
            // if (agent2Rb.velocity != Vector2.zero)
            // {
            //     Debug.Log($"Agent 2 Velocity: {agent2Rb.velocity}, Dir to Agent 1: {dirToAgent1}, Dot: {dot2:F4}");
            //     Debug.Log($"[Proximity Reward] Agent2: +{(dot2 * proximityRewardScale):F4} (Total: {agent2.GetCumulativeReward():F4})");
            // }
        }

        // Aiming accuracy reward to encourage agents to aim at each other
        if (enableAimingReward)
        {
            // Agent 1 aiming reward
            float agent1AimDiff = agent1.GetAimAngleDifferenceToEnemy();
            if (agent1AimDiff < aimingAngleThreshold)
            {
                // Reward scales inversely with angle difference (closer to perfect aim = higher reward)
                float aimingReward1 = aimingRewardScale * (1f - agent1AimDiff / aimingAngleThreshold);
                agent1.AddReward(aimingReward1);
                // Debug.Log($"[Aiming Reward] Agent1: +{aimingReward1:F4} (Total: {agent1.GetCumulativeReward():F4})");
                // Debug.Log($"Agent 1: Current Aim Angle: {agent1.CurrentAimAngle}, Angle To Enemy: {agent1.GetAngleToEnemy()}");
            }

            // Agent 2 aiming reward
            float agent2AimDiff = agent2.GetAimAngleDifferenceToEnemy();
            if (agent2AimDiff < aimingAngleThreshold)
            {
                float aimingReward2 = aimingRewardScale * (1f - agent2AimDiff / aimingAngleThreshold);
                agent2.AddReward(aimingReward2);
                // Debug.Log($"[Aiming Reward] Agent2: +{aimingReward2:F4} (Total: {agent2.GetCumulativeReward():F4})");
                // Debug.Log($"Agent 2: Current Aim Angle: {agent2.CurrentAimAngle}, Angle To Enemy: {agent2.GetAngleToEnemy()}");
            }
        }

        // Check for max episode steps (timeout - draw)
        if (currentEpisodeSteps >= maxEpisodeSteps)
        {
            agent1.SetReward(defeatPenalty);
            agent2.SetReward(defeatPenalty);
            Debug.Log($"[Timeout Draw] Agent1: {defeatPenalty:F4} (Total: {agent1.GetCumulativeReward():F4}) | Agent2: {defeatPenalty:F4} (Total: {agent2.GetCumulativeReward():F4})");
            
            // Track statistics - this is a timeout (not a real fight)
            RecordEpisodeOutcome(isTimeout: true);
            
            agent1.EndEpisode();
            agent2.EndEpisode();
            ResetScene();
            return;
        }

        if (agent1Health.IsDead || agent2Health.IsDead)
        {
            if (agent1Health.IsDead && !agent2Health.IsDead)
            {
                // Agent 2 wins
                agent2.SetReward(victoryReward);
                agent1.SetReward(defeatPenalty);
                Debug.Log($"[Victory] Agent2 wins! Agent1: {defeatPenalty:F4} (Total: {agent1.GetCumulativeReward():F4}) | Agent2: {victoryReward:F4} (Total: {agent2.GetCumulativeReward():F4})");
            }
            else if (agent2Health.IsDead && !agent1Health.IsDead)
            {
                // Agent 1 wins
                agent1.SetReward(victoryReward);
                agent2.SetReward(defeatPenalty);
                Debug.Log($"[Victory] Agent1 wins! Agent1: {victoryReward:F4} (Total: {agent1.GetCumulativeReward():F4}) | Agent2: {defeatPenalty:F4} (Total: {agent2.GetCumulativeReward():F4})");
            }
            else
            {
                // Draw - both died at the same time (still a real fight!)
                agent1.SetReward(defeatPenalty);
                agent2.SetReward(defeatPenalty);
                Debug.Log($"[Draw] Both dead! Agent1: {defeatPenalty:F4} (Total: {agent1.GetCumulativeReward():F4}) | Agent2: {defeatPenalty:F4} (Total: {agent2.GetCumulativeReward():F4})");
            }

            // Track statistics - this is a real fight (not a timeout)
            RecordEpisodeOutcome(isTimeout: false);

            agent1.EndEpisode();
            agent2.EndEpisode();
            ResetScene();
        }
    }

    private void RecordEpisodeOutcome(bool isTimeout)
    {
        // Log per-episode outcome: 1 = real fight, 0 = timeout
        // StatsRecorder automatically averages these over the summary_freq window
        // So this will show the "current" non-timeout rate, not cumulative
        statsRecorder.Add("Environment/Non-Timeout Rate", isTimeout ? 0f : 1f);
    }

    public void OnAgentHit(AgentController attacker, AgentController receiver)
    {
        attacker.AddReward(hitReward);
        receiver.AddReward(hitPenalty);
        string attackerName = attacker == agent1 ? "Agent1" : "Agent2";
        string receiverName = receiver == agent1 ? "Agent1" : "Agent2";
        Debug.Log($"[Hit] {attackerName} hit {receiverName}! {attackerName}: +{hitReward:F4} (Total: {attacker.GetCumulativeReward():F4}) | {receiverName}: {hitPenalty:F4} (Total: {receiver.GetCumulativeReward():F4})");
    }

    public void PenalizeAttackSpamming(AgentController attacker)
    {
        attacker.AddReward(attackSpammingPenalty);
        // string attackerName = attacker == agent1 ? "Agent1" : "Agent2";
        // Debug.Log($"[Attack Spamming] {attackerName} attempted attack on cooldown! Penalty: {attackSpammingPenalty:F4} (Total: {attacker.GetCumulativeReward():F4})");
    }

    public void PenalizeDashSpamming(AgentController attacker)
    {
        attacker.AddReward(dashSpammingPenalty);
        // string attackerName = attacker == agent1 ? "Agent1" : "Agent2";
        // Debug.Log($"[Dash Spamming] {attackerName} attempted dash on cooldown! Penalty: {dashSpammingPenalty:F4} (Total: {attacker.GetCumulativeReward():F4})");
    }

    private void ResetScene()
    {
        currentEpisodeSteps = 0;

        if (randomSpawning)
        {
            // Generate random positions for both agents
            Vector3 pos1 = GetRandomSpawnPosition();
            Vector3 pos2 = GetRandomSpawnPositionAwayFrom(pos1, minSpawnDistance);

            agent1.transform.position = pos1;
            agent2.transform.position = pos2;

            // Random rotation (facing direction will be adjusted by the agent)
            agent1.transform.rotation = agent1StartRot;
            agent2.transform.rotation = agent2StartRot;
        }
        else
        {
            // Deterministic spawning at initial positions
            agent1.transform.position = agent1StartPos;
            agent1.transform.rotation = agent1StartRot;
            agent2.transform.position = agent2StartPos;
            agent2.transform.rotation = agent2StartRot;
        }

        agent1Rb.velocity = Vector2.zero;
        agent1Rb.angularVelocity = 0f;
        agent2Rb.velocity = Vector2.zero;
        agent2Rb.angularVelocity = 0f;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float x = Random.Range(arenaMin.x, arenaMax.x);
        float y = Random.Range(arenaMin.y, arenaMax.y);
        return new Vector3(x, y, 0f);
    }

    private Vector3 GetRandomSpawnPositionAwayFrom(Vector3 otherPosition, float minDistance)
    {
        Vector3 newPosition;
        int maxAttempts = 100;
        int attempts = 0;

        do
        {
            newPosition = GetRandomSpawnPosition();
            attempts++;
        }
        while (Vector3.Distance(newPosition, otherPosition) < minDistance && attempts < maxAttempts);

        if (attempts >= maxAttempts)
        {
            Debug.LogWarning("Could not find spawn position with minimum distance. Using best available.");
        }

        return newPosition;
    }

    public (Vector2 min, Vector2 max) GetArenaBounds()
    {
        return (arenaMin, arenaMax);
    }
}
