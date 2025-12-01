using UnityEngine;

public class AgentStaff : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponInfo weaponInfo;
    [SerializeField] private GameObject magicLaser;
    [SerializeField] private Transform magicLaserSpawnPoint;

    private Animator myAnimator;
    private AgentController agentController;

    readonly int ATTACK_HASH = Animator.StringToHash("Attack");

    private void Awake()
    {
        myAnimator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Get the AgentController from parent hierarchy
        agentController = GetComponentInParent<AgentController>();
        
        // Find magic laser spawn point in children if not assigned
        if (magicLaserSpawnPoint == null)
        {
            magicLaserSpawnPoint = transform.Find("MagicLaserSpawnPoint");
            if (magicLaserSpawnPoint == null)
            {
                // Create a default spawn point at the weapon's position
                GameObject spawnPointObj = new GameObject("MagicLaserSpawnPoint");
                spawnPointObj.transform.SetParent(transform);
                spawnPointObj.transform.localPosition = Vector3.right * 0.5f;
                magicLaserSpawnPoint = spawnPointObj.transform;
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
            myAnimator.SetTrigger(ATTACK_HASH);
        }
        
        // Spawn the laser directly instead of relying on animation events
        SpawnStaffProjectile();
    }

    // Called directly from Attack() - animation events are unreliable for agent prefabs
    private void SpawnStaffProjectile()
    {
        if (magicLaser != null && magicLaserSpawnPoint != null && agentController != null)
        {
            // Calculate the rotation for the laser based on aim angle
            float angle = agentController.CurrentAimAngle;
            Quaternion laserRotation;
            if (agentController.FacingLeft)
            {
                // When facing left, we need to flip and adjust the angle
                laserRotation = Quaternion.Euler(0, 0, 180 + angle);
            }
            else
            {
                laserRotation = Quaternion.Euler(0, 0, angle);
            }
            
            GameObject newLaser = Instantiate(magicLaser, magicLaserSpawnPoint.position, laserRotation);
            
            // Try AgentMagicLaser first (for agent-specific prefabs)
            AgentMagicLaser agentLaser = newLaser.GetComponent<AgentMagicLaser>();
            if (agentLaser != null)
            {
                agentLaser.Initialize(agentController, weaponInfo.weaponDamage);
                agentLaser.UpdateLaserRange(weaponInfo.weaponRange);
            }
            else
            {
                // Fallback to regular MagicLaser (but it won't deal damage to agents properly)
                MagicLaser laser = newLaser.GetComponent<MagicLaser>();
                if (laser != null)
                {
                    laser.UpdateLaserRange(weaponInfo.weaponRange);
                }
            }
        }
    }

    // Keep this for backwards compatibility if animation events are set up
    public void SpawnStaffProjectileAnimEvent()
    {
        // Only spawn if not already spawned by Attack()
        // This prevents double-spawning if animation events are configured
    }

    public WeaponInfo GetWeaponInfo()
    {
        return weaponInfo;
    }

    private void AimAtEnemy()
    {
        if (agentController == null) return;

        float angle = agentController.CurrentAimAngle;
        
        // Apply rotation based on aim angle for laser direction
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
