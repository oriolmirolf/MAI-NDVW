using System.Collections;
using UnityEngine;

public class AgentMagicLaser : MonoBehaviour
{
    [SerializeField] private float laserGrowTime = 2f;

    private bool isGrowing = true;
    private float laserRange;
    private SpriteRenderer spriteRenderer;
    private CapsuleCollider2D capsuleCollider2D;
    private int damageAmount;
    private AgentController attacker;
    private TrainingManager trainingManager;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        capsuleCollider2D = GetComponent<CapsuleCollider2D>();
    }

    private void Start()
    {
        trainingManager = FindObjectOfType<TrainingManager>();
        // Rotation is now set by the staff when instantiating the laser
    }

    public void Initialize(AgentController attackerAgent, int damage)
    {
        attacker = attackerAgent;
        damageAmount = damage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Stop growing if we hit an indestructible object
        if (other.gameObject.GetComponent<Indestructible>() && !other.isTrigger)
        {
            isGrowing = false;
        }

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
            }
        }
    }

    public void UpdateLaserRange(float range)
    {
        laserRange = range;
        StartCoroutine(IncreaseLaserLengthRoutine());
    }

    private IEnumerator IncreaseLaserLengthRoutine()
    {
        float timePassed = 0f;

        while (spriteRenderer.size.x < laserRange && isGrowing)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / laserGrowTime;

            // sprite 
            spriteRenderer.size = new Vector2(Mathf.Lerp(1f, laserRange, linearT), 1f);

            // collider
            capsuleCollider2D.size = new Vector2(Mathf.Lerp(1f, laserRange, linearT), capsuleCollider2D.size.y);
            capsuleCollider2D.offset = new Vector2((Mathf.Lerp(1f, laserRange, linearT)) / 2, capsuleCollider2D.offset.y);

            yield return null;
        }

        SpriteFade spriteFade = GetComponent<SpriteFade>();
        if (spriteFade != null)
        {
            StartCoroutine(spriteFade.SlowFadeRoutine());
        }
        else
        {
            // If no SpriteFade component, just destroy after a delay
            Destroy(gameObject, 1f);
        }
    }

    private void LaserFaceEnemy()
    {
        if (attacker == null) return;

        // Face towards the direction the attacker is facing
        if (attacker.FacingLeft)
        {
            transform.rotation = Quaternion.Euler(0, 0, 180);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
