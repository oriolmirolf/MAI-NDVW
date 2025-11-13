using UnityEngine;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    [SerializeField] private AgentController agent1;
    [SerializeField] private AgentController agent2;

    [Header("Reward Parameters")]
    [Tooltip("Penalty applied every time step.")]
    public float timePenalty = -0.001f;
    [Tooltip("Reward for victory.")]
    public float victoryReward = 1f;
    [Tooltip("Penalty for defeat.")]
    public float defeatReward = -1f;
    [Tooltip("Reward for hitting opponent.")]
    public float hitReward = 0.5f;
    [Tooltip("Penalty for being hit.")]
    public float hitPenalty = -0.5f;

    private Transform agent1StartPos;
    private Transform agent2StartPos;
    private AgentHealth agent1Health;
    private AgentHealth agent2Health;

    private void Awake()
    {
        agent1.SetEnemyAgent(agent2);
        agent2.SetEnemyAgent(agent1);

        agent1StartPos = agent1.transform.parent;
        agent2StartPos = agent2.transform.parent;

        agent1Health = agent1.GetComponent<AgentHealth>();
        agent2Health = agent2.GetComponent<AgentHealth>();
    }

    private void FixedUpdate()
    {
    // Time penalty
    agent1.AddReward(timePenalty);
    agent2.AddReward(timePenalty);

        if (agent1Health.IsDead || agent2Health.IsDead)
        {
            if (agent1Health.IsDead && !agent2Health.IsDead)
            {
                // Agent 2 wins
                agent2.SetReward(victoryReward);
                agent1.SetReward(defeatReward);
            }
            else if (agent2Health.IsDead && !agent1Health.IsDead)
            {
                // Agent 1 wins
                agent1.SetReward(victoryReward);
                agent2.SetReward(defeatReward);
            }
            else
            {
                // Draw
                agent1.SetReward(defeatReward);
                agent2.SetReward(defeatReward);
            }

            agent1.EndEpisode();
            agent2.EndEpisode();
            ResetScene();
        }
    }

    public void OnAgentHit(AgentController attacker, AgentController receiver)
    {
    attacker.AddReward(hitReward);
    receiver.AddReward(hitPenalty);
    }

    private void ResetScene()
    {
        agent1.transform.position = agent1StartPos.position;
        agent1.transform.rotation = agent1StartPos.rotation;
        agent2.transform.position = agent2StartPos.position;
        agent2.transform.rotation = agent2StartPos.rotation;

        var agent1Rb = agent1.GetComponent<Rigidbody2D>();
        var agent2Rb = agent2.GetComponent<Rigidbody2D>();

        agent1Rb.velocity = Vector2.zero;
        agent1Rb.angularVelocity = 0f;
        agent2Rb.velocity = Vector2.zero;
        agent2Rb.angularVelocity = 0f;

        // A full reset of the agent's state is needed.
        // A simple way is to deactivate and reactivate, but a dedicated Reset method in each component is better.
        agent1.gameObject.SetActive(false);
        agent1.gameObject.SetActive(true);
        agent2.gameObject.SetActive(false);
        agent2.gameObject.SetActive(true);
    }
}
