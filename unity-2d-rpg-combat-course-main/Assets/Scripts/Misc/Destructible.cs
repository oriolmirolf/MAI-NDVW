using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destructible : MonoBehaviour
{
    [SerializeField] private GameObject destroyVFX;

    private void OnTriggerEnter2D(Collider2D other) {
        Projectile projectile = other.gameObject.GetComponent<Projectile>();

        if (other.gameObject.GetComponent<DamageSource>() || projectile) {

            if (projectile && projectile.GetIsEnemyProjectile()) { return; }

            PickUpSpawner pickUpSpawner = GetComponent<PickUpSpawner>();
            pickUpSpawner?.DropItems();
            Instantiate(destroyVFX, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
