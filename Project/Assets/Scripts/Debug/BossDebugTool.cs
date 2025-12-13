using UnityEngine;
using System.Linq;

/// <summary>
/// Debug tool to identify multiple boss issues.
/// Add to scene and press F9 to run diagnostics.
/// </summary>
public class BossDebugTool : MonoBehaviour
{
    [Header("Press F9 to run diagnostics")]
    [SerializeField] private bool runOnStart = true;

    private void Start()
    {
        if (runOnStart)
            Invoke(nameof(RunDiagnostics), 2f); // Delay to let rooms populate
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            RunDiagnostics();
    }

    [ContextMenu("Run Boss Diagnostics")]
    public void RunDiagnostics()
    {
        Debug.Log("<color=yellow>========== BOSS DIAGNOSTICS ==========</color>");

        // 1. Check ArchetypeRoomPopulator settings
        var archetypePopulator = FindObjectOfType<ArchetypeRoomPopulator>();
        if (archetypePopulator != null)
        {
            // Use reflection to get private fields
            var type = archetypePopulator.GetType();

            var bossRoomIndexField = type.GetField("bossRoomIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var additionalBossRoomsField = type.GetField("additionalBossRooms",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            int bossRoomIndex = bossRoomIndexField != null ? (int)bossRoomIndexField.GetValue(archetypePopulator) : -1;
            string additionalBossRooms = additionalBossRoomsField != null ?
                (string)additionalBossRoomsField.GetValue(archetypePopulator) : "";

            Debug.Log($"<color=cyan>[ArchetypeRoomPopulator]</color>");
            Debug.Log($"  bossRoomIndex: {bossRoomIndex}");
            Debug.Log($"  additionalBossRooms: '{additionalBossRooms}'");

            if (!string.IsNullOrEmpty(additionalBossRooms))
                Debug.LogWarning($"  <color=orange>WARNING: additionalBossRooms is not empty! This may cause multiple bosses.</color>");
        }
        else
        {
            Debug.LogError("ArchetypeRoomPopulator not found!");
        }

        // 2. Check ChapterTheme for boss prefab in enemy arrays
        var themes = Resources.FindObjectsOfTypeAll<ChapterTheme>();
        foreach (var theme in themes)
        {
            Debug.Log($"<color=cyan>[ChapterTheme: {theme.name}]</color>");
            Debug.Log($"  bossPrefab: {(theme.bossPrefab != null ? theme.bossPrefab.name : "NULL")}");

            if (theme.commonEnemies != null && theme.bossPrefab != null)
            {
                bool bossInCommon = theme.commonEnemies.Contains(theme.bossPrefab);
                if (bossInCommon)
                    Debug.LogError($"  <color=red>ERROR: Boss prefab is in commonEnemies array!</color>");
                else
                    Debug.Log($"  commonEnemies: {theme.commonEnemies.Length} enemies (boss not included - OK)");
            }
        }

        // 3. Count actual boss enemies in scene
        var allAgents = FindObjectsOfType<AgentInferenceSetup>();
        int bossCount = 0;
        foreach (var agent in allAgents)
        {
            var isBossField = agent.GetType().GetField("isBoss",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool isBoss = isBossField != null && (bool)isBossField.GetValue(agent);

            if (isBoss)
            {
                bossCount++;
                Debug.Log($"<color=magenta>[BOSS FOUND]</color> {agent.gameObject.name} at {agent.transform.position}");
            }
        }

        Debug.Log($"<color=yellow>Total bosses in scene: {bossCount}</color>");

        if (bossCount > 1)
            Debug.LogError($"<color=red>ERROR: Multiple bosses detected! Expected 1, found {bossCount}</color>");
        else if (bossCount == 0)
            Debug.LogWarning("<color=orange>WARNING: No bosses found in scene</color>");
        else
            Debug.Log("<color=green>Boss count OK (1 boss)</color>");

        // 4. Check BSPMSTDungeonGenerator settings
        var dungeonGen = FindObjectOfType<BSPMSTDungeonGenerator>();
        if (dungeonGen != null)
        {
            var actualBossRoomField = dungeonGen.GetType().GetField("actualBossRoomIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int actualBossRoom = actualBossRoomField != null ? (int)actualBossRoomField.GetValue(dungeonGen) : -1;

            var portalPrefabField = dungeonGen.GetType().GetField("chapterExitPortalPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var portalPrefab = portalPrefabField?.GetValue(dungeonGen) as GameObject;

            Debug.Log($"<color=cyan>[BSPMSTDungeonGenerator]</color>");
            Debug.Log($"  actualBossRoomIndex: {actualBossRoom}");

            if (portalPrefab == null)
                Debug.LogError("  <color=red>ERROR: chapterExitPortalPrefab is NULL! Assign it in the inspector.</color>");
            else
                Debug.Log($"  chapterExitPortalPrefab: {portalPrefab.name} (OK)");
        }

        // 5. Check DungeonRunManager
        var dungeonManager = DungeonRunManager.Instance;
        if (dungeonManager != null)
        {
            Debug.Log($"<color=cyan>[DungeonRunManager]</color>");
            Debug.Log($"  CurrentChapterIndex: {dungeonManager.CurrentChapterIndex}");
        }
        else
        {
            Debug.LogError("DungeonRunManager.Instance is NULL!");
        }

        Debug.Log("<color=yellow>========== END DIAGNOSTICS ==========</color>");
    }
}
