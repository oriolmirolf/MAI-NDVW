using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : Singleton<PlayerHealth>
{
    public bool IsDead { get; private set; }

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float knockBackThrustAmount = 10f;
    [SerializeField] private float damageRecoveryTime = 1f;

    [Header("Death Screen Settings")]
    [SerializeField] private TransitionScreenUI transitionUI;
    [SerializeField] private float deathScreenDuration = 5.0f; // NEW: Customizable duration
    [SerializeField] private float respawnInvincibilityTime = 2.0f; // NEW: How long you stay immortal after respawn

    private Slider healthSlider;
    private int currentHealth;
    private bool canTakeDamage = true;
    private Knockback knockback;
    private Flash flash;

    const string HEALTH_SLIDER_TEXT = "Health Slider";
    readonly int DEATH_HASH = Animator.StringToHash("Death");

    protected override void Awake() {
        base.Awake();
        flash = GetComponent<Flash>();
        knockback = GetComponent<Knockback>();
    }

    private void Start() {
        // We start with full health and NO invincibility delay (instant action)
        IsDead = false;
        currentHealth = maxHealth;
        canTakeDamage = true; 
        UpdateHealthSlider();
    }

    private void OnCollisionStay2D(Collision2D other) {
        EnemyAI enemy = other.gameObject.GetComponent<EnemyAI>();
        if (enemy) {
            TakeDamage(1, other.transform);
        }
    }

    public void HealPlayer() {
        if (currentHealth < maxHealth) {
            currentHealth += 1;
            UpdateHealthSlider();
        }
    }

    // --- CALLED BY DUNGEON MANAGER ---
    public void ResetHealth() {
        IsDead = false;
        currentHealth = maxHealth;
        UpdateHealthSlider();

        // 1. Hide the screen (Smoothly)
        if (transitionUI != null) transitionUI.HideAllScreens();

        // 2. Reset Animator
        GetComponent<Animator>().Rebind(); 
        GetComponent<Animator>().Update(0f);

        // 3. START INVINCIBILITY
        // We set canTakeDamage to false immediately so you don't die during the fade-in
        canTakeDamage = false;
        StartCoroutine(RespawnInvincibilityRoutine());
    }

    private IEnumerator RespawnInvincibilityRoutine()
    {
        Debug.Log("Player is immortal for fade-in...");
        // Wait for the black screen to fade away + a little buffer
        yield return new WaitForSeconds(respawnInvincibilityTime);
        
        canTakeDamage = true;
        Debug.Log("Player is mortal again!");
    }

    public void TakeDamage(int damageAmount, Transform hitTransform) {
        if (!canTakeDamage || IsDead) { return; }

        ScreenShakeManager.Instance.ShakeScreen();
        knockback.GetKnockedBack(hitTransform, knockBackThrustAmount);
        StartCoroutine(flash.FlashRoutine());
        
        canTakeDamage = false;
        currentHealth -= damageAmount;
        
        // Only recover from damage if we aren't dead
        if (currentHealth > 0)
        {
            StartCoroutine(DamageRecoveryRoutine());
        }

        UpdateHealthSlider();
        CheckIfPlayerDeath();
    }

    private void CheckIfPlayerDeath() {
        if (currentHealth <= 0 && !IsDead) {
            IsDead = true;
            currentHealth = 0;
            
            GetComponent<Animator>().SetTrigger(DEATH_HASH);
            StartCoroutine(DeathLoadSceneRoutine());
        }
    }

    private IEnumerator DeathLoadSceneRoutine() {
        
        // Show the black screen
        if (transitionUI != null)
        {
            int currentChap = 0;
            if (DungeonRunManager.Instance != null) 
                currentChap = DungeonRunManager.Instance.CurrentChapterIndex;
            
            transitionUI.ShowDeathScreen(currentChap);
        }

        // WAIT 5 SECONDS (Or whatever you set in Inspector)
        yield return new WaitForSeconds(deathScreenDuration);

        Stamina.Instance.ReplenishStaminaOnDeath();

        if (DungeonRunManager.Instance != null)
        {
            DungeonRunManager.Instance.OnPlayerDeath();
        }
        else
        {
            Debug.LogError("DungeonRunManager missing!");
        }
    }

    private IEnumerator DamageRecoveryRoutine() {
        yield return new WaitForSeconds(damageRecoveryTime);
        canTakeDamage = true;
    }

    private void UpdateHealthSlider() {
        if (healthSlider == null) {
            healthSlider = GameObject.Find(HEALTH_SLIDER_TEXT)?.GetComponent<Slider>();
        }

        if (healthSlider) {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
    
    public int GetCurrentHealth() { return currentHealth; }
    public int GetMaxHealth() { return maxHealth; }
}