using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Singleton<PlayerController>, ICombatTarget
{
    public bool FacingLeft { get { return facingLeft; } }

    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float dashSpeed = 4f;
    [SerializeField] private float waterSpeedMultiplier = 0.5f;
    [SerializeField] private TrailRenderer myTrailRenderer;
    [SerializeField] private Transform weaponCollider;

    private PlayerControls playerControls;
    private bool isInWater = false;
    private Vector2 movement;
    private Rigidbody2D rb;
    private Animator myAnimator;
    private SpriteRenderer mySpriteRender;
    private Knockback knockback;
    private float startingMoveSpeed;

    private bool facingLeft = false;
    private bool isDashing = false;
    
    // ICombatTarget implementation
    public Transform Transform => transform;
    public Rigidbody2D Rigidbody => rb;
    public bool IsDead => PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
    
    public float CurrentAimAngle
    {
        get
        {
            // Calculate aim angle based on mouse position
            Vector3 mousePos = Input.mousePosition;
            Vector3 playerScreenPoint = Camera.main.WorldToScreenPoint(transform.position);
            Vector3 direction = mousePos - playerScreenPoint;
            
            // Atan2 returns angle where 0 = right, 90 = up
            // We want 0 = up, so we subtract 90 degrees
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            
            // Normalize to [-180, 180]
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            
            return angle;
        }
    }
    
    public float GetNormalizedHealth()
    {
        if (PlayerHealth.Instance == null) return 1f;
        return (float)PlayerHealth.Instance.GetCurrentHealth() / PlayerHealth.Instance.GetMaxHealth();
    }
    
    public float GetNormalizedWeaponIndex()
    {
        // Player typically uses one weapon at a time from inventory
        // For simplicity, return 0 (sword) since agents only trained with swords
        return 0f;
    }
    
    public float GetNormalizedCooldownRemaining()
    {
        // Access cooldown state from ActiveWeapon
        if (ActiveWeapon.Instance == null) return 0f;
        return ActiveWeapon.Instance.GetNormalizedCooldownRemaining();
    }

    protected override void Awake() {
        base.Awake();

        playerControls = new PlayerControls();
        rb = GetComponent<Rigidbody2D>();
        myAnimator = GetComponent<Animator>();
        mySpriteRender = GetComponent<SpriteRenderer>();
        knockback = GetComponent<Knockback>();
    }

    private void Start() {
        playerControls.Combat.Dash.performed += _ => Dash();

        startingMoveSpeed = moveSpeed;

        ActiveInventory.Instance.EquipStartingWeapon();
    }

    private void OnEnable() {
        playerControls.Enable();
    }
    
    private void OnDisable() {
        playerControls.Disable();
    }

    private void Update() {
        PlayerInput();
    }

    private void FixedUpdate() {
        AdjustPlayerFacingDirection();
        Move();
    }

    public Transform GetWeaponCollider() {
        return weaponCollider;
    }

    private void PlayerInput() {
        movement = playerControls.Movement.Move.ReadValue<Vector2>();

        myAnimator.SetFloat("moveX", movement.x);
        myAnimator.SetFloat("moveY", movement.y);
    }

    private void Move() {
        if (knockback.GettingKnockedBack || PlayerHealth.Instance.IsDead) { return; }

        rb.MovePosition(rb.position + movement * (moveSpeed * Time.fixedDeltaTime));
    }

    private void AdjustPlayerFacingDirection() {
        Vector3 mousePos = Input.mousePosition;
        Vector3 playerScreenPoint = Camera.main.WorldToScreenPoint(transform.position);

        if (mousePos.x < playerScreenPoint.x) {
            mySpriteRender.flipX = true;
            facingLeft = true;
        } else {
            mySpriteRender.flipX = false;
            facingLeft = false;
        }
    }

    private void Dash() {
        if (!isDashing && Stamina.Instance.CurrentStamina > 0) {
            Stamina.Instance.UseStamina();
            isDashing = true;
            moveSpeed *= dashSpeed;
            myTrailRenderer.emitting = true;
            StartCoroutine(EndDashRoutine());
        }
    }

    private IEnumerator EndDashRoutine() {
        float dashTime = .2f;
        float dashCD = .25f;
        yield return new WaitForSeconds(dashTime);
        moveSpeed = isInWater ? startingMoveSpeed * waterSpeedMultiplier : startingMoveSpeed;
        myTrailRenderer.emitting = false;
        yield return new WaitForSeconds(dashCD);
        isDashing = false;
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.name == "Water") {
            isInWater = true;
            if (!isDashing) {
                moveSpeed = startingMoveSpeed * waterSpeedMultiplier;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other) {
        if (other.gameObject.name == "Water") {
            isInWater = false;
            if (!isDashing) {
                moveSpeed = startingMoveSpeed;
            }
        }
    }
}
