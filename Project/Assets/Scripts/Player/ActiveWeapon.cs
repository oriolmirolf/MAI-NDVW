using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveWeapon : Singleton<ActiveWeapon>
{
    public MonoBehaviour CurrentActiveWeapon { get; private set; }
    private PlayerControls playerControls;
    private float timeBetweenAttacks;
    private bool attackButtonDown, isAttacking = false;
    private float lastAttackTime = -Mathf.Infinity;


    protected override void Awake() {
        base.Awake();

        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        if (playerControls == null)
            playerControls = new PlayerControls();
        playerControls.Enable();
    }

    private void Start()
    {
        playerControls.Combat.Attack.started += _ => StartAttacking();
        playerControls.Combat.Attack.canceled += _ => StopAttacking();
    }

    private void Update() {
        Attack();
    }

    public void NewWeapon(MonoBehaviour newWeapon) {
        CurrentActiveWeapon = newWeapon;

        AttackCoolDown();

        timeBetweenAttacks = (CurrentActiveWeapon as IWeapon).GetWeaponInfo().weaponCooldown;
    }

    private void AttackCoolDown()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        StopAllCoroutines();
        StartCoroutine(TimeBetweenAttacksRoutine());
    }

    private IEnumerator TimeBetweenAttacksRoutine()
    {
        yield return new WaitForSeconds(timeBetweenAttacks);
        isAttacking = false;
    }

    public void WeaponNull() {
        CurrentActiveWeapon = null;
    }

    private void StartAttacking()
    {
        attackButtonDown = true;
    }

    private void StopAttacking()
    {
        attackButtonDown = false;
    }

    private void Attack() {
        if (attackButtonDown && !isAttacking && CurrentActiveWeapon) {
            AttackCoolDown();
            (CurrentActiveWeapon as IWeapon).Attack();
        }
    }
    
    /// <summary>
    /// Get normalized cooldown remaining [0, 1] for ICombatTarget interface
    /// 0 = ready to attack, 1 = just attacked
    /// </summary>
    public float GetNormalizedCooldownRemaining()
    {
        if (timeBetweenAttacks <= 0f) return 0f;
        float timeSinceLastAttack = Time.time - lastAttackTime;
        float cooldownRemaining = Mathf.Max(0f, timeBetweenAttacks - timeSinceLastAttack);
        return Mathf.Clamp01(cooldownRemaining / timeBetweenAttacks);
    }
}
