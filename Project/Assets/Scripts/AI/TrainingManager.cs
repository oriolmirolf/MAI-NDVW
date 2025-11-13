using UnityEngine;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    public PlayerAgent agent1;
    public PlayerAgent agent2;

    private void Start()
    {
        if (agent1 != null)
        {
            agent1.enemyAgent = agent2;
            agent1.OnEpisodeRequested += AgentEpisodeRequested;
        }
        if (agent2 != null)
        {
            agent2.enemyAgent = agent1;
            agent2.OnEpisodeRequested += AgentEpisodeRequested;
        }

        ResetScene();
    }

    private void AgentEpisodeRequested()
    {
        // This will be called when an agent's episode ends.
        // We want to end the episode for both agents at the same time.
        if (agent1 != null && !agent1.IsEpisodeFinished())
        {
            agent1.EndEpisode();
        }
        if (agent2 != null && !agent2.IsEpisodeFinished())
        {
            agent2.EndEpisode();
        }

        ResetScene();
    }

    public void ResetScene()
    {
        // Randomize positions
        if (agent1 != null)
        {
            agent1.transform.localPosition = new Vector3(Random.Range(-10f, 0f), Random.Range(-5f, 5f), 0);
            agent1.GetComponent<AgentHealth>().ResetHealth();
            agent1.GetComponent<AgentWeapon>().ResetWeapons();
        }
        if (agent2 != null)
        {
            agent2.transform.localPosition = new Vector3(Random.Range(0f, 10f), Random.Range(-5f, 5f), 0);
            agent2.GetComponent<AgentHealth>().ResetHealth();
            agent2.GetComponent<AgentWeapon>().ResetWeapons();
        }
    }

    private void OnDestroy()
    {
        if (agent1 != null)
        {
            agent1.OnEpisodeRequested -= AgentEpisodeRequested;
        }
        if (agent2 != null)
        {
            agent2.OnEpisodeRequested -= AgentEpisodeRequested;
        }
    }
}
