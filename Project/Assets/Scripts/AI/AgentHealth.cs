using System.Collections;
using UnityEngine;

public class AgentHealth : MonoBehaviour
{
    public bool IsDead { get; private set; }

    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float knockBackThrustAmount = 10f;
    [SerializeField] private float damageRecoveryTime = 1f;

    private int currentHealth;
    private bool canTakeDamage = true;
    private Knockback knockback;
    private Flash flash;

    readonly int DEATH_HASH = Animator.StringToHash("Death");

    private void Awake() {
        flash = GetComponent<Flash>();
        knockback = GetComponent<Knockback>();
    }

    private void Start() {
        IsDead = false;
        currentHealth = maxHealth;
    }

    private void OnCollisionStay2D(Collision2D other) {
        EnemyAI enemy = other.gameObject.GetComponent<EnemyAI>();

        if (enemy) {
            TakeDamage(1, other.transform);
        }
    }

    public void TakeDamage(int damageAmount, Transform hitTransform) {
        if (IsDead || !canTakeDamage) { return; }

        ScreenShakeManager.Instance.ShakeScreen();
        knockback.GetKnockedBack(hitTransform, knockBackThrustAmount);
        StartCoroutine(flash.FlashRoutine());
        canTakeDamage = false;
        currentHealth -= damageAmount;
        StartCoroutine(DamageRecoveryRoutine());
        CheckIfPlayerDeath();
    }

    private void CheckIfPlayerDeath()
    {
        if (currentHealth <= 0 && !IsDead)
        {
            IsDead = true;
            currentHealth = 0;

            // 1. Play the animation
            GetComponent<Animator>().SetTrigger(DEATH_HASH);

            // 2. Disable further damage so enemies don't keep hitting a corpse
            canTakeDamage = false;

            // 3. Wait for the animation to finish, then tell the Manager
            StartCoroutine(WaitAndRespawn());
        }
    }
    
    private IEnumerator WaitAndRespawn()
    {
        // Adjust this delay to match the length of your death animation!
        yield return new WaitForSeconds(2.0f); 

        if (DungeonRunManager.Instance != null)
        {
            DungeonRunManager.Instance.OnPlayerDeath();
        }
        else
        {
            Debug.LogError("DungeonRunManager missing! Cannot respawn.");
        }
    }

    private IEnumerator DamageRecoveryRoutine() {
        yield return new WaitForSeconds(damageRecoveryTime);
        canTakeDamage = true;
    }

    public int GetCurrentHealth() {
        return currentHealth;
    }

    public int GetMaxHealth() {
        return maxHealth;
    }

    public float GetNormalizedHealth() {
        return (float)currentHealth / maxHealth;
    }

    public void ResetHealth() {
        IsDead = false;
        currentHealth = maxHealth;
        canTakeDamage = true;
    }
}
