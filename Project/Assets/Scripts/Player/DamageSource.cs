using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageSource : MonoBehaviour
{
    private int damageAmount;

    private void Start() {
        MonoBehaviour currentActiveWeapon = ActiveWeapon.Instance.CurrentActiveWeapon;
        damageAmount = (currentActiveWeapon as IWeapon).GetWeaponInfo().weaponDamage;
    }

    private void OnTriggerEnter2D(Collider2D other) {
        // Try to damage regular enemies (EnemyHealth)
        EnemyHealth enemyHealth = other.gameObject.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damageAmount);
            return;
        }
        
        // Try to damage ML Agents (AgentHealth)
        AgentHealth agentHealth = other.gameObject.GetComponent<AgentHealth>();
        if (agentHealth != null)
        {
            // Make sure we're not hitting ourselves (in case player ever has AgentHealth)
            AgentController agent = other.gameObject.GetComponent<AgentController>();
            if (agent != null)
            {
                agentHealth.TakeDamage(damageAmount, PlayerController.Instance.transform);
            }
        }
    }
}
