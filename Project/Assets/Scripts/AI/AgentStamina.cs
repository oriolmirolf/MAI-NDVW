using System.Collections;
using UnityEngine;

public class AgentStamina : MonoBehaviour
{
    public int CurrentStamina { get; private set; }

    [SerializeField] private int timeBetweenStaminaRefresh = 3;

    private int startingStamina = 3;
    private int maxStamina;

    private void Awake() {
        maxStamina = startingStamina;
        CurrentStamina = startingStamina;
    }

    public void UseStamina() {
        CurrentStamina--;
        StopAllCoroutines();
        StartCoroutine(RefreshStaminaRoutine());
    }

    public void RefreshStamina() {
        if (CurrentStamina < maxStamina) {
            CurrentStamina++;
        }
    }

    public void ReplenishStaminaOnDeath() {
        CurrentStamina = startingStamina;
    }

    public void ResetStamina() {
        StopAllCoroutines();
        CurrentStamina = startingStamina;
    }

    public float GetNormalizedStamina() {
        return (float)CurrentStamina / maxStamina;
    }

    private IEnumerator RefreshStaminaRoutine() {
        while (true)
        {
            yield return new WaitForSeconds(timeBetweenStaminaRefresh);
            RefreshStamina();
        }
    }
}
