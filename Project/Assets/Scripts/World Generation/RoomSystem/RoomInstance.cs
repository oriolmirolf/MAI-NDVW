using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cinemachine;

public class RoomInstance : MonoBehaviour
{
    [Header("Room Structure")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallsTilemap;
    [SerializeField] private PolygonCollider2D cameraBounds;

    [Header("Spawn Points")]
    [SerializeField] private Transform playerSpawnCenter;
    [SerializeField] private Transform[] enemySpawnPoints;

    // Portals are now spawned dynamically by ProceduralRoomBuilder using AreaExit prefab

    [Header("Runtime")]
    [SerializeField] private Transform enemiesContainer;
    [SerializeField] private Transform objectsContainer;

    private RoomNode node;
    private bool isActive;
    private List<GameObject> activeEnemies = new List<GameObject>();

    public RoomNode Node => node;
    public bool IsActive => isActive;
    public Tilemap FloorTilemap => floorTilemap;
    public PolygonCollider2D CameraBounds => cameraBounds;
    public Vector3 CenterPosition => playerSpawnCenter != null ? playerSpawnCenter.position : transform.position;

    public event System.Action OnRoomCleared;
    public event System.Action OnRoomActivated;
    public event System.Action OnRoomDeactivated;

    public void Initialize(RoomNode roomNode)
    {
        this.node = roomNode;

        // Create containers if not assigned
        if (enemiesContainer == null)
        {
            enemiesContainer = new GameObject("Enemies").transform;
            enemiesContainer.SetParent(transform);
        }
        if (objectsContainer == null)
        {
            objectsContainer = new GameObject("Objects").transform;
            objectsContainer.SetParent(transform);
        }

        // Build the room tiles procedurally
        var builder = GetComponent<ProceduralRoomBuilder>();
        if (builder != null)
        {
            int seed = DungeonGraph.Instance != null ? DungeonGraph.Instance.GetHashCode() + node.id : node.id;
            builder.BuildRoom(node, new System.Random(seed));
        }

        // Portals are spawned by ProceduralRoomBuilder using AreaExit prefab

        // Initially deactivated
        Deactivate();
    }

    public Vector3 GetSpawnPositionForDirection(Direction fromDirection)
    {
        // Find the entrance for the given direction
        var entrances = GetComponentsInChildren<AreaEntrance>();
        // For now, return center position - AreaEntrance handles positioning via its own logic
        return CenterPosition;
    }

    public void Activate()
    {
        if (isActive) return;
        isActive = true;
        Debug.Log($"[Room] Activating room {node.id}, isCleared={node.isCleared}, activeEnemies={activeEnemies.Count}");

        // Show room visually
        SetRoomVisible(true);

        // Enable enemy AI
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.SetActive(true);
            }
        }

        // Spawn enemies if not already spawned and room not cleared
        if (!node.isCleared && activeEnemies.Count == 0)
        {
            SpawnRoomContent();
        }
        else
        {
            Debug.Log($"[Room] Skipping spawn: isCleared={node.isCleared}, activeEnemies={activeEnemies.Count}");
        }

        // Snap camera to room center (Binding of Isaac style)
        SnapCameraToRoom();

        // Show narrative dialogue for this room
        ShowRoomNarrative();

        OnRoomActivated?.Invoke();
    }

    private void ShowRoomNarrative()
    {
        bool hasNarrativeGen = LLMNarrativeGenerator.Instance != null;
        bool hasDialogueUI = DialogueUI.Instance != null;
        Debug.Log($"[Room] ShowRoomNarrative: hasNarrativeGen={hasNarrativeGen}, hasDialogueUI={hasDialogueUI}");

        if (!hasNarrativeGen || !hasDialogueUI)
        {
            Debug.LogWarning($"[Room] Missing components - cannot show narrative");
            return;
        }

        var narrative = LLMNarrativeGenerator.Instance.GetNarrative(node.id);
        if (narrative == null)
        {
            Debug.LogWarning($"[Room] No narrative found for room {node.id}");
            return;
        }

        if (narrative.npcDialogues.Count > 0)
        {
            var npcDialogue = narrative.npcDialogues[0];
            Debug.Log($"[Room] Narrative for room {node.id}: {npcDialogue.npcName}, lines={npcDialogue.dialogueLines.Count}, audioPaths={npcDialogue.audioPaths?.Count ?? 0}");

            if (npcDialogue.dialogueLines.Count > 0)
            {
                Debug.Log($"[Room] Showing dialogue for room {node.id}: {npcDialogue.npcName}");
                DialogueUI.Instance.ShowDialogue(npcDialogue);
            }
        }
    }

    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        // Disable enemy AI (keep them in memory)
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.SetActive(false);
            }
        }

        // Hide room visually (fog of war)
        SetRoomVisible(false);

        OnRoomDeactivated?.Invoke();
    }

    private void SetRoomVisible(bool visible)
    {
        // Toggle all renderers in this room
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }

        // Toggle tilemap renderers
        var tilemapRenderers = GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>(true);
        foreach (var tr in tilemapRenderers)
        {
            tr.enabled = visible;
        }
    }

    private void SpawnRoomContent()
    {
        Debug.Log($"[Room] SpawnRoomContent: room={node.id}, template={(node.template != null ? node.template.name : "null")}, useProceduralPopulation={(node.template?.useProceduralPopulation ?? false)}");

        if (node.template == null || !node.template.useProceduralPopulation)
        {
            Debug.Log($"[Room] Skipping spawn: no template or procedural population disabled");
            return;
        }

        bool hasAIManager = AIDungeonContentManager.Instance != null;
        bool aiContentReady = hasAIManager && AIDungeonContentManager.Instance.ContentReady;
        Debug.Log($"[Room] AI check: hasManager={hasAIManager}, contentReady={aiContentReady}");

        if (aiContentReady)
        {
            AIDungeonContentManager.Instance.SpawnEnemiesForRoom(
                node,
                enemiesContainer,
                node.template.size
            );
        }
        else
        {
            Debug.Log($"[Room] Using fallback populator system");
            var populators = GetComponents<IRoomPopulator>();
            if (populators.Length > 0)
            {
                var roomData = new RoomData
                {
                    index = node.id,
                    rect = new RectInt(0, 0, node.template.size.x, node.template.size.y),
                    center = new Vector3Int(node.template.size.x / 2, node.template.size.y / 2, 0)
                };

                var rng = new System.Random(DungeonGraph.Instance != null ?
                    DungeonGraph.Instance.GetHashCode() + node.id : node.id);

                foreach (var pop in populators)
                {
                    pop.Populate(roomData, rng, objectsContainer, enemiesContainer, floorTilemap);
                }
            }
        }

        // Track spawned enemies
        foreach (Transform child in enemiesContainer)
        {
            activeEnemies.Add(child.gameObject);
            node.spawnedEnemies.Add(child.gameObject);
        }
    }

    private void UpdateCameraBounds()
    {
        if (cameraBounds == null) return;

        var confiner = FindObjectOfType<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.m_BoundingShape2D = cameraBounds;
            confiner.InvalidateCache();
        }
    }

    /// <summary>
    /// Binding of Isaac style: Snap camera to show entire room (no player following)
    /// </summary>
    public void SnapCameraToRoom()
    {
        var vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam == null) return;

        // Get room size (default 32x18 for 16:9 aspect ratio)
        Vector2Int roomSize = (node != null && node.template != null)
            ? node.template.size
            : new Vector2Int(32, 18);

        // Calculate room center
        Vector3 roomCenter = transform.position + new Vector3(roomSize.x / 2f, roomSize.y / 2f, 0);

        // Set orthographic size to show full room height
        float orthoSize = roomSize.y / 2f;
        vcam.m_Lens.OrthographicSize = orthoSize;

        // Setup aspect ratio enforcer on main camera to match room proportions
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var aspectEnforcer = mainCam.GetComponent<AspectRatioEnforcer>();
            if (aspectEnforcer == null)
            {
                aspectEnforcer = mainCam.gameObject.AddComponent<AspectRatioEnforcer>();
            }
            aspectEnforcer.SetTargetAspect(roomSize.x, roomSize.y);
        }

        // Create or get a target for the camera to look at
        var cameraTarget = transform.Find("CameraTarget");
        if (cameraTarget == null)
        {
            var targetObj = new GameObject("CameraTarget");
            targetObj.transform.SetParent(transform);
            cameraTarget = targetObj.transform;
        }
        cameraTarget.position = roomCenter;

        // Set camera to follow the room's center point
        vcam.Follow = cameraTarget;

        // Force immediate position update
        vcam.PreviousStateIsValid = false;

        // Update bounds
        UpdateCameraBounds();
    }

    public void OnEnemyKilled(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
        node.spawnedEnemies.Remove(enemy);

        CheckRoomCleared();
    }

    private void CheckRoomCleared()
    {
        if (node.isCleared) return;

        // Remove null entries (destroyed enemies)
        activeEnemies.RemoveAll(e => e == null);

        if (activeEnemies.Count == 0)
        {
            node.isCleared = true;
            OnRoomCleared?.Invoke();
        }
    }

    private void Update()
    {
        if (!isActive || node.isCleared) return;

        // Periodically check for destroyed enemies
        CheckRoomCleared();
    }

    public void ForceCleared()
    {
        node.isCleared = true;
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();
        OnRoomCleared?.Invoke();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw room bounds
        if (node != null && node.template != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 size = new Vector3(node.template.size.x, node.template.size.y, 0);
            Gizmos.DrawWireCube(transform.position + size / 2f, size);
        }

        // Draw spawn points
        Gizmos.color = Color.green;
        if (playerSpawnCenter != null)
        {
            Gizmos.DrawSphere(playerSpawnCenter.position, 0.3f);
        }

        Gizmos.color = Color.red;
        if (enemySpawnPoints != null)
        {
            foreach (var sp in enemySpawnPoints)
            {
                if (sp != null) Gizmos.DrawSphere(sp.position, 0.2f);
            }
        }
    }
#endif
}
