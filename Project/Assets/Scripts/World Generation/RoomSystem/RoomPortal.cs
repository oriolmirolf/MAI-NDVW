using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RoomPortal : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Direction direction;
    [SerializeField] private Transform spawnPoint;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer portalVisual;
    [SerializeField] private GameObject activeEffects;
    [SerializeField] private GameObject lockedVisual;

    private RoomInstance ownerRoom;
    private RoomNode targetRoom;
    private bool isInitialized;
    private bool isLocked;

    public Direction Direction => direction;
    public Transform SpawnPoint => spawnPoint;
    public RoomNode TargetRoom => targetRoom;
    public bool IsLocked => isLocked;

    public void Initialize(RoomInstance owner, Direction dir, RoomNode target)
    {
        ownerRoom = owner;
        direction = dir;
        targetRoom = target;
        isInitialized = true;

        // Rotate portal visual based on direction
        if (portalVisual != null)
        {
            portalVisual.transform.rotation = Quaternion.Euler(0, 0, dir.ToRotation());
        }

        // Setup spawn point offset if not set
        if (spawnPoint == null)
        {
            var spawnObj = new GameObject("SpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = GetSpawnOffset(dir);
            spawnPoint = spawnObj.transform;
        }

        UpdateVisuals();
    }

    private Vector3 GetSpawnOffset(Direction dir)
    {
        float offset = 1.5f;
        return dir switch
        {
            Direction.North => new Vector3(0, -offset, 0),
            Direction.South => new Vector3(0, offset, 0),
            Direction.East => new Vector3(-offset, 0, 0),
            Direction.West => new Vector3(offset, 0, 0),
            _ => Vector3.zero
        };
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (lockedVisual != null)
        {
            lockedVisual.SetActive(isLocked);
        }
        if (activeEffects != null)
        {
            activeEffects.SetActive(!isLocked);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized || isLocked) return;
        if (!other.CompareTag("Player")) return;

        // Trigger room transition
        if (DungeonGraph.Instance != null && targetRoom != null)
        {
            DungeonGraph.Instance.TransitionToRoom(targetRoom, direction);
        }
    }

    private void OnEnable()
    {
        // Subscribe to room cleared event to unlock portals
        if (ownerRoom != null)
        {
            ownerRoom.OnRoomCleared += OnOwnerRoomCleared;
        }
    }

    private void OnDisable()
    {
        if (ownerRoom != null)
        {
            ownerRoom.OnRoomCleared -= OnOwnerRoomCleared;
        }
    }

    private void OnOwnerRoomCleared()
    {
        SetLocked(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = isLocked ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(spawnPoint.position, 0.2f);
            Gizmos.DrawLine(transform.position, spawnPoint.position);
        }

        // Draw direction arrow
        Gizmos.color = Color.blue;
        Vector3 dir = (Vector3)(Vector2)direction.ToVector();
        Gizmos.DrawRay(transform.position, dir);
    }
#endif
}
