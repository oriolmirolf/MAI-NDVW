using UnityEngine;

public class AgentDamageSource : MonoBehaviour
{
    public AgentController Attacker { get; set; }
    private int damageAmount;
    private TrainingManager trainingManager;

    private void Start()
    {
        // TrainingManager is only used during training for rewards, may be null during inference
        trainingManager = FindObjectOfType<TrainingManager>();
    }

    public void SetDamage(int damage)
    {
        damageAmount = damage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Don't hit ourselves
        if (Attacker != null && other.gameObject == Attacker.gameObject)
        {
            return;
        }
        
        // Try to damage an AgentController (self-play or agent vs agent)
        AgentHealth agentHealth = other.gameObject.GetComponent<AgentHealth>();
        if (agentHealth != null)
        {
            AgentController receiver = other.gameObject.GetComponent<AgentController>();
            if (receiver != null && receiver != Attacker)
            {
                agentHealth.TakeDamage(damageAmount, Attacker != null ? Attacker.transform : transform);

                // Only report hit to TrainingManager if it exists (training mode)
                if (trainingManager != null)
                {
                    trainingManager.OnAgentHit(Attacker, receiver);
                }
                return;
            }
        }
        
        // Try to damage the Player (agent vs player in inference mode)
        PlayerHealth playerHealth = other.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount, Attacker != null ? Attacker.transform : transform);
        }
    }
}
