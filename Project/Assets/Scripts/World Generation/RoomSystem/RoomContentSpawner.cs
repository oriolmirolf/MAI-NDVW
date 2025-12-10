using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class RoomContentSpawner : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private ChapterTheme theme;

    [Header("Populators")]
    [SerializeField] private CombatRoomPopulator combatPopulator = new CombatRoomPopulator();
    [SerializeField] private BossArenaPopulator bossPopulator = new BossArenaPopulator();

    [Header("References")]
    [SerializeField] private Tilemap floorTilemap;

    private RoomInstance roomInstance;
    private Transform objectsContainer;
    private Transform enemiesContainer;

    public ChapterTheme Theme => theme;

    private void Awake()
    {
        roomInstance = GetComponent<RoomInstance>();
    }

    public void SpawnContent(RoomNode node, System.Random rng)
    {
        if (node == null || theme == null) return;
        if (node.isCleared) return;

        // Get or create containers
        objectsContainer = transform.Find("Objects");
        if (objectsContainer == null)
        {
            objectsContainer = new GameObject("Objects").transform;
            objectsContainer.SetParent(transform);
        }

        enemiesContainer = transform.Find("Enemies");
        if (enemiesContainer == null)
        {
            enemiesContainer = new GameObject("Enemies").transform;
            enemiesContainer.SetParent(transform);
        }

        // Build room data in local space
        var roomData = new RoomData
        {
            index = node.id,
            rect = new RectInt(0, 0, node.template.size.x, node.template.size.y),
            center = new Vector3Int(node.template.size.x / 2, node.template.size.y / 2, 0)
        };

        // Select populator based on archetype
        IArchetypePopulator populator = node.archetype switch
        {
            RoomArchetype.BossArena => bossPopulator,
            _ => combatPopulator
        };

        if (populator != null && floorTilemap != null)
        {
            populator.PopulateRoom(roomData, theme, rng, floorTilemap, objectsContainer, enemiesContainer);
        }

        // Track spawned enemies
        foreach (Transform child in enemiesContainer)
        {
            node.spawnedEnemies.Add(child.gameObject);
        }

        foreach (Transform child in objectsContainer)
        {
            node.spawnedObjects.Add(child.gameObject);
        }
    }

    public void ClearContent()
    {
        if (objectsContainer != null)
        {
            foreach (Transform child in objectsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (enemiesContainer != null)
        {
            foreach (Transform child in enemiesContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void SetTheme(ChapterTheme newTheme)
    {
        theme = newTheme;
    }
}
