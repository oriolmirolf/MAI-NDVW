using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentWeapons : MonoBehaviour
{
    public MonoBehaviour CurrentActiveWeapon { get; private set; }
    
    [SerializeField] private List<GameObject> weaponPrefabs;
    private int currentWeaponIndex = 0;
    private float timeBetweenAttacks;
    private bool isAttacking = false;

    private void Start()
    {
        if (weaponPrefabs.Count > 0)
        {
            EquipWeapon(0, this.GetComponent<AgentController>());
        }
    }

    public void Attack(AgentController attacker)
    {
        if (!isAttacking && CurrentActiveWeapon)
        {
            AttackCoolDown();
            (CurrentActiveWeapon as IWeapon).Attack();
            
            // Pass the attacker to the damage source
            AgentDamageSource damageSource = CurrentActiveWeapon.GetComponentInChildren<AgentDamageSource>();
            if (damageSource != null)
            {
                damageSource.Attacker = attacker;
            }
        }
    }

    public void SwitchWeapon(int weaponIndex, AgentController attacker)
    {
        if (weaponIndex >= 0 && weaponIndex < weaponPrefabs.Count && weaponIndex != currentWeaponIndex)
        {
            EquipWeapon(weaponIndex, attacker);
        }
    }

    private void EquipWeapon(int weaponIndex, AgentController attacker)
    {
        if (CurrentActiveWeapon != null)
        {
            Destroy(CurrentActiveWeapon.gameObject);
        }

        GameObject newWeaponPrefab = weaponPrefabs[weaponIndex];
        GameObject weaponInstance = Instantiate(newWeaponPrefab, transform);
        CurrentActiveWeapon = weaponInstance.GetComponent<MonoBehaviour>();
        currentWeaponIndex = weaponIndex;
        
        if (CurrentActiveWeapon is IWeapon weapon)
        {
            timeBetweenAttacks = weapon.GetWeaponInfo().weaponCooldown;
            
            AgentDamageSource damageSource = weaponInstance.GetComponentInChildren<AgentDamageSource>();
            if (damageSource != null)
            {
                damageSource.Attacker = attacker;
                damageSource.SetDamage(weapon.GetWeaponInfo().weaponDamage);
            }
        }
        
        AttackCoolDown();
    }

    private void AttackCoolDown()
    {
        isAttacking = true;
        StopAllCoroutines();
        StartCoroutine(TimeBetweenAttacksRoutine());
    }

    private IEnumerator TimeBetweenAttacksRoutine()
    {
        yield return new WaitForSeconds(timeBetweenAttacks);
        isAttacking = false;
    }

    public void WeaponNull()
    {
        if (CurrentActiveWeapon != null)
        {
            Destroy(CurrentActiveWeapon.gameObject);
        }
        CurrentActiveWeapon = null;
    }
}
