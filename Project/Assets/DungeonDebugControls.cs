using UnityEngine;

public class DungeonDebugControls : MonoBehaviour
{
    private DungeonRunManager mgr;

    private void Awake()
    {
        mgr = FindObjectOfType<DungeonRunManager>();
    }

    private void Update()
    {
        if (mgr == null) return;

        // Simulate player death
        if (Input.GetKeyDown(KeyCode.K))
            mgr.OnPlayerDeath();

        // Simulate boss killed in current chapter
        if (Input.GetKeyDown(KeyCode.B))
            mgr.OnBossDefeated();

    }
}
