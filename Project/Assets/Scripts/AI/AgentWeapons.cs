using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentWeapons : MonoBehaviour
{
    public MonoBehaviour CurrentActiveWeapon { get; private set; }
    
    [SerializeField] private List<GameObject> weaponPrefabs;
    [SerializeField] private float globalAttackCooldown = 0.5f; // Minimum time between any attacks
    
    private int currentWeaponIndex = 0;
    private float timeBetweenAttacks;
    private float lastAttackTime = -Mathf.Infinity; // Time-based cooldown instead of bool
    
    private AgentController agentController;
    private AgentDamageSource weaponDamageSource;

    private void Awake()
    {
        agentController = GetComponent<AgentController>();
    }

    private void Start()
    {
        // Get the damage source from the weapon collider (which is a child of the agent, not the weapon)
        if (agentController != null)
        {
            Transform weaponCollider = agentController.GetWeaponCollider();
            if (weaponCollider != null)
            {
                weaponDamageSource = weaponCollider.GetComponent<AgentDamageSource>();
                if (weaponDamageSource != null)
                {
                    weaponDamageSource.Attacker = agentController;
                }
            }
        }

        if (weaponPrefabs.Count > 0)
        {
            EquipWeapon(0, agentController);
        }
    }

    public void Attack(AgentController attacker)
    {
        // Use time-based cooldown that persists across weapon switches
        float currentCooldown = Mathf.Max(timeBetweenAttacks, globalAttackCooldown);
        if (Time.time >= lastAttackTime + currentCooldown && CurrentActiveWeapon)
        {
            lastAttackTime = Time.time;
            
            // Ensure the attacker is set on the weapon collider's damage source
            if (weaponDamageSource != null)
            {
                weaponDamageSource.Attacker = attacker;
            }
            
            (CurrentActiveWeapon as IWeapon).Attack();
        }
    }

    public void SwitchWeapon(int weaponIndex, AgentController attacker)
    {
        if (weaponIndex >= 0 && weaponIndex < weaponPrefabs.Count && weaponIndex != currentWeaponIndex)
        {
            EquipWeapon(weaponIndex, attacker);
        }
    }
    
    public int GetWeaponCount()
    {
        return weaponPrefabs.Count;
    }

    public int GetCurrentWeaponIndex()
    {
        return currentWeaponIndex;
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
            
            // Update damage on the weapon collider's damage source
            if (weaponDamageSource != null)
            {
                weaponDamageSource.Attacker = attacker;
                weaponDamageSource.SetDamage(weapon.GetWeaponInfo().weaponDamage);
            }
        }
        // Note: We do NOT reset the attack cooldown when switching weapons
        // The time-based cooldown persists across weapon switches
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
