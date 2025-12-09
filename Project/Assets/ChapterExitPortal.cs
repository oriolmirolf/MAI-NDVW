using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ChapterExitPortal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        var mgr = FindObjectOfType<DungeonRunManager>();
        if (mgr == null) return;

        if (mgr.CanUseChapterExit())
        {
            mgr.UseChapterExitRoutine();
        }
        else
        {
            Debug.Log("[ChapterExitPortal] Chapter is not completed yet.");
        }
    }
    
}
