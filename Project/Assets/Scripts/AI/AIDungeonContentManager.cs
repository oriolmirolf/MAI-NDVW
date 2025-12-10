using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIDungeonContentManager : MonoBehaviour
{
    public static AIDungeonContentManager Instance { get; private set; }

    [Header("AI Settings")]
    [SerializeField] private bool useAIGeneration = true;
    [SerializeField] private string theme = "dark forest";
    [SerializeField] private string[] availableEnemies = { "slime", "ghost", "grape" };

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject slimePrefab;
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private GameObject grapePrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float enemyPlacementMargin = 2f;

    private DungeonContentResponse aiContent;
    private bool contentReady = false;
    private bool isGenerating = false;
    private Dictionary<string, GameObject> enemyPrefabMap;

    public bool ContentReady => contentReady;
    public bool IsGenerating => isGenerating;
    public DungeonContentResponse AIContent => aiContent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        enemyPrefabMap = new Dictionary<string, GameObject>
        {
            { "slime", slimePrefab },
            { "ghost", ghostPrefab },
            { "grape", grapePrefab }
        };

        // Warn about missing prefabs
        if (slimePrefab == null) Debug.LogError("[AI] Slime prefab not assigned!");
        if (ghostPrefab == null) Debug.LogError("[AI] Ghost prefab not assigned!");
        if (grapePrefab == null) Debug.LogError("[AI] Grape prefab not assigned!");
    }

    private void Start()
    {
        if (DungeonGraph.Instance != null)
        {
            DungeonGraph.Instance.OnDungeonGenerated += OnDungeonGenerated;
        }
    }

    private void OnDungeonGenerated()
    {
        if (useAIGeneration)
        {
            StartCoroutine(RequestAIContent());
        }
        else
        {
            contentReady = true;
        }
    }

    private IEnumerator RequestAIContent()
    {
        contentReady = false;
        isGenerating = true;

        // Pause the game while generating
        Time.timeScale = 0f;

        if (GenAIClient.Instance == null)
        {
            Debug.LogWarning("[AI] GenAIClient not found, using fallback");
            ResumeGame();
            yield break;
        }

        var dungeonGraph = DungeonGraph.Instance;
        if (dungeonGraph == null)
        {
            Debug.LogError("[AI] DungeonGraph not found");
            ResumeGame();
            yield break;
        }

        var request = BuildRequest(dungeonGraph);
        Debug.Log($"[AI] Requesting content for {request.rooms.Length} rooms...");

        bool requestComplete = false;

        yield return GenAIClient.Instance.GenerateDungeonContent(
            request,
            (response) =>
            {
                aiContent = response;
                requestComplete = true;
                Debug.Log($"[AI] Received content: seed={response.seed}, theme={response.theme}, rooms={response.rooms.Count}");
                foreach (var kvp in response.rooms)
                {
                    var room = kvp.Value;
                    string enemies = room.enemies.Count > 0
                        ? string.Join(", ", room.enemies.ConvertAll(e => $"{e.count}x {e.type}"))
                        : "none";
                    Debug.Log($"[AI] Room {room.room_id}: {room.description} | Enemies: {enemies}");
                }
            },
            (error) =>
            {
                Debug.LogError($"[AI] Request failed: {error}");
                requestComplete = true;
            }
        );

        while (!requestComplete)
        {
            yield return null;
        }

        ResumeGame();
        Debug.Log($"[AI] ContentReady={contentReady}, aiContent={(aiContent != null ? "loaded" : "null")}");
    }

    private void ResumeGame()
    {
        contentReady = true;
        isGenerating = false;
        Time.timeScale = 1f;
    }

    private DungeonContentRequest BuildRequest(DungeonGraph dungeonGraph)
    {
        var rooms = dungeonGraph.Rooms;
        var roomInfoList = new List<RoomInfo>();

        foreach (var kvp in rooms)
        {
            var node = kvp.Value;
            var connections = new List<string>();

            if (node.HasConnection(Direction.North)) connections.Add("north");
            if (node.HasConnection(Direction.South)) connections.Add("south");
            if (node.HasConnection(Direction.East)) connections.Add("east");
            if (node.HasConnection(Direction.West)) connections.Add("west");

            roomInfoList.Add(new RoomInfo
            {
                id = node.id,
                connections = connections.ToArray(),
                is_start = node.isStartRoom,
                is_boss = node.isBossRoom
            });
        }

        return new DungeonContentRequest
        {
            seed = dungeonGraph.Seed,
            theme = theme,
            rooms = roomInfoList.ToArray(),
            available_enemies = availableEnemies,
            use_cache = true
        };
    }

    public void SpawnEnemiesForRoom(RoomNode node, Transform enemiesContainer, Vector2Int roomSize)
    {
        Debug.Log($"[AI] SpawnEnemiesForRoom called: room={node.id}, useAI={useAIGeneration}, aiContent={(aiContent != null ? "loaded" : "null")}");

        if (!useAIGeneration || aiContent == null)
        {
            Debug.LogWarning($"[AI] Skipping spawn: useAIGeneration={useAIGeneration}, aiContent={(aiContent != null ? "loaded" : "null")}");
            return;
        }

        var roomContent = aiContent.GetRoom(node.id);
        if (roomContent == null)
        {
            Debug.LogWarning($"[AI] No content found for room {node.id}");
            return;
        }

        Debug.Log($"[AI] Spawning for room {node.id}: {roomContent.enemies.Count} enemy types");

        var rng = new System.Random(node.id + aiContent.seed);
        var occupiedPositions = new HashSet<Vector3Int>();

        MarkOccupiedAreas(node, roomSize, occupiedPositions);

        int totalSpawned = 0;
        foreach (var enemySpawn in roomContent.enemies)
        {
            string enemyType = enemySpawn.type.ToLower();
            bool hasPrefab = enemyPrefabMap.TryGetValue(enemyType, out var prefab);
            Debug.Log($"[AI] Enemy type '{enemyType}': hasPrefab={hasPrefab}, prefab={(prefab != null ? prefab.name : "null")}, count={enemySpawn.count}");

            if (!hasPrefab || prefab == null)
                continue;

            for (int i = 0; i < enemySpawn.count; i++)
            {
                Vector3 spawnPos = GetValidSpawnPosition(roomSize, rng, occupiedPositions);
                var enemy = Instantiate(prefab, enemiesContainer);
                enemy.transform.localPosition = spawnPos;
                enemy.name = $"{enemySpawn.type}_{i}";
                occupiedPositions.Add(Vector3Int.FloorToInt(spawnPos));
                totalSpawned++;
            }
        }

        Debug.Log($"[AI] Room {node.id}: spawned {totalSpawned} enemies total");
    }

    private void MarkOccupiedAreas(RoomNode node, Vector2Int roomSize, HashSet<Vector3Int> occupied)
    {
        // Mark borders
        for (int x = 0; x < roomSize.x; x++)
        {
            occupied.Add(new Vector3Int(x, 0, 0));
            occupied.Add(new Vector3Int(x, roomSize.y - 1, 0));
        }
        for (int y = 0; y < roomSize.y; y++)
        {
            occupied.Add(new Vector3Int(0, y, 0));
            occupied.Add(new Vector3Int(roomSize.x - 1, y, 0));
        }

        // Mark center (player spawn area)
        MarkArea(new Vector2Int(roomSize.x / 2, roomSize.y / 2), 3f, occupied);

        // Mark portals
        if (node.HasConnection(Direction.North))
            MarkArea(new Vector2Int(roomSize.x / 2, roomSize.y - 1), 2f, occupied);
        if (node.HasConnection(Direction.South))
            MarkArea(new Vector2Int(roomSize.x / 2, 0), 2f, occupied);
        if (node.HasConnection(Direction.East))
            MarkArea(new Vector2Int(roomSize.x - 1, roomSize.y / 2), 2f, occupied);
        if (node.HasConnection(Direction.West))
            MarkArea(new Vector2Int(0, roomSize.y / 2), 2f, occupied);
    }

    private void MarkArea(Vector2Int center, float radius, HashSet<Vector3Int> occupied)
    {
        int r = Mathf.CeilToInt(radius);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    occupied.Add(new Vector3Int(center.x + x, center.y + y, 0));
                }
            }
        }
    }

    private Vector3 GetValidSpawnPosition(Vector2Int roomSize, System.Random rng, HashSet<Vector3Int> occupied)
    {
        int maxAttempts = 50;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = enemyPlacementMargin + (float)rng.NextDouble() * (roomSize.x - 2 * enemyPlacementMargin);
            float y = enemyPlacementMargin + (float)rng.NextDouble() * (roomSize.y - 2 * enemyPlacementMargin);
            var pos = new Vector3(x, y, 0);
            var gridPos = Vector3Int.FloorToInt(pos);

            if (!occupied.Contains(gridPos))
            {
                return pos;
            }
        }

        // Fallback: return center-ish position
        return new Vector3(roomSize.x / 2f, roomSize.y / 2f + 2f, 0);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (DungeonGraph.Instance != null)
        {
            DungeonGraph.Instance.OnDungeonGenerated -= OnDungeonGenerated;
        }
    }
}
