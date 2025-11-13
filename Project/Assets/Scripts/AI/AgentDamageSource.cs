using UnityEngine;

public class AgentDamageSource : MonoBehaviour
{
    public AgentController Attacker { get; set; }
    private int damageAmount;
    private TrainingManager trainingManager;

    private void Start()
    {
        // This assumes the TrainingManager is in the scene. A more robust solution might use a singleton or service locator.
        trainingManager = FindObjectOfType<TrainingManager>();
    }

    public void SetDamage(int damage)
    {
        damageAmount = damage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        AgentHealth agentHealth = other.gameObject.GetComponent<AgentHealth>();
        if (agentHealth != null)
        {
            AgentController receiver = other.gameObject.GetComponent<AgentController>();
            if (receiver != null && receiver != Attacker)
            {
                agentHealth.TakeDamage(damageAmount, Attacker.transform);

                if (trainingManager != null)
                {
                    trainingManager.OnAgentHit(Attacker, receiver);
                }
            }
        }
    }
}
