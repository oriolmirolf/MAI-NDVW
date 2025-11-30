using UnityEngine;

public class AgentSword : MonoBehaviour, IWeapon
{
    [SerializeField] private WeaponInfo weaponInfo;
    [SerializeField] private GameObject slashAnimPrefab;
    [SerializeField] private Transform slashAnimSpawnPoint;

    private Transform weaponCollider;
    private Animator myAnimator;
    private GameObject slashAnim;
    private AgentController agentController;

    private void Awake()
    {
        myAnimator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Get the AgentController from parent hierarchy
        agentController = GetComponentInParent<AgentController>();
        if (agentController != null)
        {
            weaponCollider = agentController.GetWeaponCollider();
        }
        
        // Find slash spawn point in children if not assigned
        if (slashAnimSpawnPoint == null)
        {
            slashAnimSpawnPoint = transform.Find("SlashAnimSpawnPoint");
            if (slashAnimSpawnPoint == null)
            {
                // Create a default spawn point at the weapon's position
                GameObject spawnPointObj = new GameObject("SlashAnimSpawnPoint");
                spawnPointObj.transform.SetParent(transform);
                spawnPointObj.transform.localPosition = Vector3.right * 0.5f;
                slashAnimSpawnPoint = spawnPointObj.transform;
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
            myAnimator.SetTrigger("Attack");
        }
        
        if (weaponCollider != null)
        {
            weaponCollider.gameObject.SetActive(true);
        }
        
        if (slashAnimPrefab != null && slashAnimSpawnPoint != null)
        {
            slashAnim = Instantiate(slashAnimPrefab, slashAnimSpawnPoint.position, Quaternion.identity);
            slashAnim.transform.parent = this.transform.parent;
        }
    }

    public WeaponInfo GetWeaponInfo()
    {
        return weaponInfo;
    }

    public void DoneAttackingAnimEvent()
    {
        if (weaponCollider != null)
        {
            weaponCollider.gameObject.SetActive(false);
        }
    }

    public void SwingUpFlipAnimEvent()
    {
        if (slashAnim != null)
        {
            slashAnim.transform.rotation = Quaternion.Euler(-180, 0, 0);

            if (agentController != null && agentController.FacingLeft)
            {
                SpriteRenderer sr = slashAnim.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.flipX = true;
                }
            }
        }
    }

    public void SwingDownFlipAnimEvent()
    {
        if (slashAnim != null)
        {
            slashAnim.transform.rotation = Quaternion.Euler(0, 0, 0);

            if (agentController != null && agentController.FacingLeft)
            {
                SpriteRenderer sr = slashAnim.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.flipX = true;
                }
            }
        }
    }

    private void AimAtEnemy()
    {
        if (agentController == null) return;

        float angle = agentController.CurrentAimAngle;
        
        // Apply rotation based on aim angle, similar to player's MouseFollowWithOffset
        if (agentController.FacingLeft)
        {
            transform.rotation = Quaternion.Euler(0, -180, angle);
            if (weaponCollider != null)
            {
                weaponCollider.rotation = Quaternion.Euler(0, -180, 0);
            }
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, angle);
            if (weaponCollider != null)
            {
                weaponCollider.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
    }
}
