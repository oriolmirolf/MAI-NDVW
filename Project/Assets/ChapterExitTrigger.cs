using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ChapterExitTrigger : MonoBehaviour
{
    private bool hasTriggered = false; // Stops double-activation bugs

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. If we already triggered, ignore everything
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            if (DungeonRunManager.Instance != null && DungeonRunManager.Instance.CanUseChapterExit())
            {
                Debug.Log("[ChapterExitTrigger] Player entered portal. Starting transition.");
                
                // 2. Lock the trigger so it can't fire again
                hasTriggered = true;

                // 3. CORRECTLY start the Coroutine on the Manager
                DungeonRunManager.Instance.StartCoroutine(DungeonRunManager.Instance.UseChapterExitRoutine());
            }
        }
    }
}