using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentWeapon : MonoBehaviour
{
    public MonoBehaviour CurrentActiveWeapon { get; private set; }
    private float timeBetweenAttacks;
    private bool isAttacking = false;
    private PlayerAgent agent;
    private List<IWeapon> weapons = new List<IWeapon>();

    // List of weapon prefabs assigned via Inspector
    [SerializeField]
    private List<GameObject> weaponPrefabs;

    private void Awake()
    {
        agent = GetComponent<PlayerAgent>();
        foreach (var prefab in weaponPrefabs)
        {
            // Instantiate but keep inactive
            var weaponObj = Instantiate(prefab, transform);
            weaponObj.SetActive(false);

            var weapon = weaponObj.GetComponent<IWeapon>();
            if (weapon != null)
            {
                weapons.Add(weapon);
            }
            else
            {
                Debug.LogWarning($"Prefab {prefab.name} does not have an IWeapon component.");
            }
        }
    }

    public void NewWeapon(MonoBehaviour newWeapon)
    {
        if (CurrentActiveWeapon != null)
        {
            (CurrentActiveWeapon as MonoBehaviour).gameObject.SetActive(false);
        }

        CurrentActiveWeapon = newWeapon;
        (CurrentActiveWeapon as MonoBehaviour).gameObject.SetActive(true);

        timeBetweenAttacks = (CurrentActiveWeapon as IWeapon).GetWeaponInfo().weaponCooldown;
        AttackCoolDown();
    }

    public void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Count)
        {
            if (CurrentActiveWeapon != null)
            {
                (CurrentActiveWeapon as MonoBehaviour).gameObject.SetActive(false);
                CurrentActiveWeapon = null;
            }
            return;
        }

        var weaponToEquip = weapons[weaponIndex] as MonoBehaviour;
        NewWeapon(weaponToEquip);
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

    public void Attack()
    {
        if (!isAttacking && CurrentActiveWeapon)
        {
            AttackCoolDown();
            (CurrentActiveWeapon as IWeapon).Attack();
        }
    }

    public void ResetWeapons()
    {
        if (CurrentActiveWeapon != null)
        {
            (CurrentActiveWeapon as MonoBehaviour).gameObject.SetActive(false);
        }
        CurrentActiveWeapon = null;
        isAttacking = false;
        StopAllCoroutines();
    }
}
