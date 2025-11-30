using UnityEngine;

public class AgentBow : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponInfo weaponInfo;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;

    readonly int FIRE_HASH = Animator.StringToHash("Fire");

    private Animator myAnimator;
    private AgentController agentController;

    private void Awake()
    {
        myAnimator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Get the AgentController from parent hierarchy
        agentController = GetComponentInParent<AgentController>();
        
        // Find arrow spawn point in children if not assigned
        if (arrowSpawnPoint == null)
        {
            arrowSpawnPoint = transform.Find("ArrowSpawnPoint");
            if (arrowSpawnPoint == null)
            {
                // Create a default spawn point at the weapon's position
                GameObject spawnPointObj = new GameObject("ArrowSpawnPoint");
                spawnPointObj.transform.SetParent(transform);
                spawnPointObj.transform.localPosition = Vector3.right * 0.5f;
                arrowSpawnPoint = spawnPointObj.transform;
            }
        }
    }

    private void Update()
    {
        AimAtEnemy();
    }

    public void Attack()
    {
        if (myAnimator != null)
        {
            myAnimator.SetTrigger(FIRE_HASH);
        }
        
        if (arrowPrefab != null && arrowSpawnPoint != null && agentController != null)
        {
            // Get the direction to aim (based on weapon rotation)
            Quaternion rotation = transform.rotation;
            GameObject newArrow = Instantiate(arrowPrefab, arrowSpawnPoint.position, rotation);
            
            // Try AgentProjectile first (for agent-specific prefabs)
            AgentProjectile agentProjectile = newArrow.GetComponent<AgentProjectile>();
            if (agentProjectile != null)
            {
                agentProjectile.Initialize(agentController, weaponInfo.weaponDamage);
                agentProjectile.UpdateProjectileRange(weaponInfo.weaponRange);
            }
            else
            {
                // Fallback to regular Projectile (but it won't deal damage to agents properly)
                Projectile projectile = newArrow.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.UpdateProjectileRange(weaponInfo.weaponRange);
                }
            }
        }
    }

    public WeaponInfo GetWeaponInfo()
    {
        return weaponInfo;
    }

    private void AimAtEnemy()
    {
        if (agentController == null) return;

        float angle = agentController.CurrentAimAngle;
        
        // Apply rotation based on aim angle for projectile direction
        if (agentController.FacingLeft)
        {
            transform.rotation = Quaternion.Euler(0, -180, angle);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
