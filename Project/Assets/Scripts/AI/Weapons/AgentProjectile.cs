using UnityEngine;

public class AgentProjectile : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 22f;
    [SerializeField] private GameObject particleOnHitPrefabVFX;
    [SerializeField] private float projectileRange = 10f;

    private Vector3 startPosition;
    private int damageAmount;
    private AgentController attacker;
    private TrainingManager trainingManager;

    private void Start()
    {
        startPosition = transform.position;
        trainingManager = FindObjectOfType<TrainingManager>();
    }

    private void Update()
    {
        MoveProjectile();
        DetectFireDistance();
    }

    public void Initialize(AgentController attackerAgent, int damage)
    {
        attacker = attackerAgent;
        damageAmount = damage;
    }

    public void UpdateProjectileRange(float range)
    {
        projectileRange = range;
    }

    public void UpdateMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if we hit another agent
        AgentHealth agentHealth = other.gameObject.GetComponent<AgentHealth>();
        if (agentHealth != null)
        {
            AgentController receiver = other.gameObject.GetComponent<AgentController>();
            if (receiver != null && receiver != attacker)
            {
                agentHealth.TakeDamage(damageAmount, transform);

                if (trainingManager != null)
                {
                    trainingManager.OnAgentHit(attacker, receiver);
                }

                if (particleOnHitPrefabVFX != null)
                {
                    Transform dropsParent = FindObjectOfType<BSPMSTDungeonGenerator>()?.DropsParent;
                    Instantiate(particleOnHitPrefabVFX, transform.position, transform.rotation, dropsParent);
                }
                Destroy(gameObject);
                return;
            }
        }

        // Check if we hit an indestructible object
        Indestructible indestructible = other.gameObject.GetComponent<Indestructible>();
        if (indestructible != null && !other.isTrigger)
        {
            if (particleOnHitPrefabVFX != null)
            {
                Transform dropsParent = FindObjectOfType<BSPMSTDungeonGenerator>()?.DropsParent;
                Instantiate(particleOnHitPrefabVFX, transform.position, transform.rotation, dropsParent);
            }
            Destroy(gameObject);
        }
    }

    private void DetectFireDistance()
    {
        if (Vector3.Distance(transform.position, startPosition) > projectileRange)
        {
            Destroy(gameObject);
        }
    }

    private void MoveProjectile()
    {
        transform.Translate(Vector3.right * Time.deltaTime * moveSpeed);
    }
}
