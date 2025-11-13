using UnityEngine;

public class AgentHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    private PlayerAgent agent;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        agent = GetComponent<PlayerAgent>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damageAmount, PlayerAgent attacker)
    {
        if (IsDead) return;

        currentHealth -= damageAmount;
        
        // Add rewards
        if (attacker != null)
        {
            attacker.AddReward(1.0f); // Positive reward for landing an attack
        }
        agent.AddReward(-1.0f); // Negative reward for receiving an attack

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            IsDead = true;
            
            if (attacker != null)
            {
                attacker.AddReward(5.0f); // Bonus reward for winning
            }
            agent.AddReward(-5.0f); // Extra penalty for losing

            // Instead of ending episodes directly here, request a synchronized episode end.
            // TrainingManager listens to OnEpisodeRequested and will EndEpisode() for BOTH agents
            // exactly once, then reset the scene.
            agent.RequestEpisodeEnd();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }
}
