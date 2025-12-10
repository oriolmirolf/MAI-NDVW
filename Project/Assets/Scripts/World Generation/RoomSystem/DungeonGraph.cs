using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonGraph : MonoBehaviour
{
    public static DungeonGraph Instance { get; private set; }

    [Header("Generation Settings")]
    [SerializeField] private int roomCount = 8;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool generateOnStart = true;

    [Header("Room Templates")]
    [SerializeField] private RoomTemplate[] combatTemplates;
    [SerializeField] private RoomTemplate bossTemplate;
    [SerializeField] private RoomTemplate startTemplate;

    [Header("Room Loading")]
    [Tooltip("Keep adjacent rooms loaded for smooth transitions")]
    [SerializeField] private bool preloadAdjacentRooms = false;

    [Header("References")]
    [SerializeField] private Transform roomContainer;

    // Runtime data
    private Dictionary<int, RoomNode> rooms = new Dictionary<int, RoomNode>();
    private RoomNode currentRoom;
    private RoomNode startRoom;
    private RoomNode bossRoom;
    private System.Random rng;

    public RoomNode CurrentRoom => currentRoom;
    public RoomNode StartRoom => startRoom;
    public RoomNode BossRoom => bossRoom;
    public IReadOnlyDictionary<int, RoomNode> Rooms => rooms;
    public int Seed => seed;
    public int RoomCount => roomCount;

    public event System.Action<RoomNode, RoomNode> OnRoomChanged;
    public event System.Action OnDungeonGenerated;

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

        if (roomContainer == null)
        {
            roomContainer = new GameObject("RoomContainer").transform;
            roomContainer.SetParent(transform);
        }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();
        rng = new System.Random(seed);

        // Phase 1: Generate room graph using random walk
        GenerateRoomGraph();

        // Phase 2: Assign templates to rooms
        AssignTemplates();

        // Phase 3: Load ALL rooms (like BSP system - all rooms exist in same scene)
        // This allows AreaExit/AreaEntrance teleportation to work via FindObjectsOfType
        foreach (var room in rooms.Values)
        {
            LoadRoom(room);
        }

        // Phase 4: Trigger AI content generation (rooms will be activated after AI content is ready)
        OnDungeonGenerated?.Invoke();

        // Phase 5: Start coroutine to wait for AI then activate starting room
        StartCoroutine(WaitForAIThenActivate());
    }

    private System.Collections.IEnumerator WaitForAIThenActivate()
    {
        // Wait for AI content to be ready (or skip if no AI manager)
        if (AIDungeonContentManager.Instance != null)
        {
            while (!AIDungeonContentManager.Instance.ContentReady)
            {
                yield return null;
            }
            Debug.Log("[DungeonGraph] AI content ready");
        }

        // Wait for narratives to be ready (or skip if no narrative generator)
        if (LLMNarrativeGenerator.Instance != null)
        {
            while (!LLMNarrativeGenerator.Instance.IsReady())
            {
                yield return null;
            }
            Debug.Log("[DungeonGraph] Narratives ready");
        }

        Debug.Log("[DungeonGraph] All content ready, activating starting room");
        SetCurrentRoom(startRoom);
    }

    private void GenerateRoomGraph()
    {
        var grid = new Dictionary<Vector2Int, RoomNode>();
        var frontier = new List<(Vector2Int pos, Direction fromDir, RoomNode parent)>();

        // Create start room at origin
        startRoom = new RoomNode(0, Vector2Int.zero);
        startRoom.isStartRoom = true;
        startRoom.isDiscovered = true;
        startRoom.archetype = RoomArchetype.CombatRoom;
        rooms[0] = startRoom;
        grid[Vector2Int.zero] = startRoom;

        // Add initial frontier
        AddToFrontier(frontier, grid, Vector2Int.zero, startRoom);

        int currentId = 1;
        int maxDistance = 0;
        RoomNode furthestRoom = startRoom;

        // Generate rooms via random expansion
        while (rooms.Count < roomCount && frontier.Count > 0)
        {
            // Pick random frontier position
            int idx = rng.Next(frontier.Count);
            var (pos, fromDir, parent) = frontier[idx];
            frontier.RemoveAt(idx);

            if (grid.ContainsKey(pos)) continue;

            // Create new room
            var newRoom = new RoomNode(currentId++, pos);
            newRoom.archetype = RoomArchetype.CombatRoom;
            rooms[newRoom.id] = newRoom;
            grid[pos] = newRoom;

            // Connect to parent
            parent.Connect(fromDir, newRoom);

            // Track furthest room for boss placement
            int distance = Mathf.Abs(pos.x) + Mathf.Abs(pos.y);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                furthestRoom = newRoom;
            }

            // Add new frontier positions
            AddToFrontier(frontier, grid, pos, newRoom);
        }

        // Connect some additional rooms for loops (optional)
        AddExtraConnections(grid);

        // Mark boss room
        bossRoom = furthestRoom;
        bossRoom.isBossRoom = true;
        bossRoom.archetype = RoomArchetype.BossArena;
    }

    private void AddToFrontier(
        List<(Vector2Int, Direction, RoomNode)> frontier,
        Dictionary<Vector2Int, RoomNode> grid,
        Vector2Int pos,
        RoomNode room)
    {
        Direction[] dirs = { Direction.North, Direction.East, Direction.South, Direction.West };
        foreach (var dir in dirs)
        {
            var newPos = pos + dir.ToVector();
            if (!grid.ContainsKey(newPos))
            {
                frontier.Add((newPos, dir, room));
            }
        }
    }

    private void AddExtraConnections(Dictionary<Vector2Int, RoomNode> grid)
    {
        // Add 1-2 extra connections to create loops
        int extraConnections = rng.Next(1, 3);
        int added = 0;

        var roomList = rooms.Values.ToList();
        for (int i = 0; i < roomList.Count && added < extraConnections; i++)
        {
            var room = roomList[i];
            Direction[] dirs = { Direction.North, Direction.East, Direction.South, Direction.West };

            foreach (var dir in dirs)
            {
                if (room.HasConnection(dir)) continue;

                var neighborPos = room.gridPosition + dir.ToVector();
                if (grid.TryGetValue(neighborPos, out var neighbor))
                {
                    if (!neighbor.HasConnection(dir.Opposite()))
                    {
                        room.Connect(dir, neighbor);
                        added++;
                        break;
                    }
                }
            }
        }
    }

    private void AssignTemplates()
    {
        foreach (var room in rooms.Values)
        {
            DoorMask required = room.GetRequiredDoors();

            if (room.isStartRoom && startTemplate != null)
            {
                room.template = startTemplate;
            }
            else if (room.isBossRoom && bossTemplate != null)
            {
                room.template = bossTemplate;
            }
            else
            {
                room.template = SelectTemplate(combatTemplates, required, room.archetype);
            }
        }
    }

    private RoomTemplate SelectTemplate(RoomTemplate[] templates, DoorMask required, RoomArchetype archetype)
    {
        if (templates == null || templates.Length == 0) return null;

        // Filter by door requirements and archetype
        var valid = templates.Where(t =>
            t != null &&
            t.MatchesDoorRequirements(required) &&
            t.archetype == archetype
        ).ToList();

        if (valid.Count == 0)
        {
            // Fallback: any template with all doors
            valid = templates.Where(t => t != null && t.availableDoors == DoorMask.All).ToList();
        }

        if (valid.Count == 0) return templates[0];

        // Weighted random selection
        int totalWeight = valid.Sum(t => t.selectionWeight);
        int roll = rng.Next(totalWeight);
        int cumulative = 0;

        foreach (var t in valid)
        {
            cumulative += t.selectionWeight;
            if (roll < cumulative) return t;
        }

        return valid[0];
    }

    public void LoadRoom(RoomNode node)
    {
        if (node == null || node.IsLoaded) return;
        if (node.template == null || node.template.prefab == null)
        {
            Debug.LogWarning($"Room {node.id} has no template or prefab assigned");
            return;
        }

        // Calculate world position based on grid position and room size
        Vector3 worldPos = GridToWorld(node.gridPosition, node.template.size);

        node.instance = Instantiate(node.template.prefab, worldPos, Quaternion.identity, roomContainer);
        node.instance.name = $"Room_{node.id}_{node.archetype}";

        node.roomInstance = node.instance.GetComponent<RoomInstance>();
        if (node.roomInstance != null)
        {
            node.roomInstance.Initialize(node);
        }

        node.isDiscovered = true;
    }

    public void UnloadRoom(RoomNode node)
    {
        if (node == null || !node.IsLoaded) return;
        if (node == currentRoom) return; // Never unload current room

        node.ClearRuntimeReferences();
    }

    public void SetCurrentRoom(RoomNode node)
    {
        if (node == null) return;

        var previous = currentRoom;
        currentRoom = node;

        // Deactivate previous room
        if (previous != null && previous.roomInstance != null)
        {
            previous.roomInstance.Deactivate();
        }

        // Activate current room
        if (node.roomInstance != null)
        {
            node.roomInstance.Activate();
        }

        // Manage room loading
        if (preloadAdjacentRooms)
        {
            // Load adjacent rooms
            foreach (var adjacent in node.connections.Values)
            {
                if (!adjacent.IsLoaded)
                {
                    LoadRoom(adjacent);
                }
            }

            // Unload distant rooms (more than 2 steps away)
            UnloadDistantRooms(node, 2);
        }

        OnRoomChanged?.Invoke(previous, node);
    }

    private void UnloadDistantRooms(RoomNode center, int maxDistance)
    {
        var distances = CalculateDistances(center);

        foreach (var kvp in distances)
        {
            if (kvp.Value > maxDistance && kvp.Key != center)
            {
                UnloadRoom(kvp.Key);
            }
        }
    }

    private Dictionary<RoomNode, int> CalculateDistances(RoomNode start)
    {
        var distances = new Dictionary<RoomNode, int>();
        var queue = new Queue<(RoomNode node, int dist)>();

        queue.Enqueue((start, 0));
        distances[start] = 0;

        while (queue.Count > 0)
        {
            var (node, dist) = queue.Dequeue();

            foreach (var adjacent in node.connections.Values)
            {
                if (!distances.ContainsKey(adjacent))
                {
                    distances[adjacent] = dist + 1;
                    queue.Enqueue((adjacent, dist + 1));
                }
            }
        }

        return distances;
    }

    public Vector3 GridToWorld(Vector2Int gridPos, Vector2Int roomSize)
    {
        // Each room takes roomSize tiles, add some spacing
        float spacing = 2f;
        float x = gridPos.x * (roomSize.x + spacing);
        float y = gridPos.y * (roomSize.y + spacing);
        return new Vector3(x, y, 0);
    }

    public RoomNode GetRoomAt(Vector2Int gridPos)
    {
        return rooms.Values.FirstOrDefault(r => r.gridPosition == gridPos);
    }

    public RoomNode GetRoom(int roomId)
    {
        rooms.TryGetValue(roomId, out var room);
        return room;
    }

    public void TransitionToRoom(RoomNode targetRoom, Direction fromDirection)
    {
        if (targetRoom == null) return;

        // Ensure target room is loaded
        if (!targetRoom.IsLoaded)
        {
            LoadRoom(targetRoom);
        }

        // Trigger transition via RoomTransitionManager
        if (RoomTransitionManager.Instance != null)
        {
            RoomTransitionManager.Instance.TransitionTo(targetRoom, fromDirection);
        }
        else
        {
            // Fallback: instant transition
            SetCurrentRoom(targetRoom);
        }
    }

    public void ClearDungeon()
    {
        foreach (var room in rooms.Values)
        {
            room.ClearRuntimeReferences();
        }
        rooms.Clear();
        currentRoom = null;
        startRoom = null;
        bossRoom = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
