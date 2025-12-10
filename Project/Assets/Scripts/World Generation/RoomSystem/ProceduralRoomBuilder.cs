using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ProceduralRoomBuilder : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallsTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase[] groundVariations;

    [System.Serializable]
    public class WallTiles
    {
        public TileBase topA, topB, bottomA, bottomB;
        public TileBase cornerTopLeft, cornerTopRight, cornerBottomLeft, cornerBottomRight;
        public TileBase left, right;
        public TileBase innerTopLeft, innerBottomLeft, innerBottomRight, innerTopRight;
    }

    [SerializeField] private WallTiles wallTiles;

    [Header("Portal Settings")]
    [SerializeField] private int portalWidth = 3;  // 3 tiles wide opening (matches VFX)
    [SerializeField] private GameObject areaExitPrefab;

    private HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
    private System.Random rng;
    private RoomInstance roomInstance;

    public Tilemap FloorTilemap => floorTilemap;

    private void Awake()
    {
        roomInstance = GetComponent<RoomInstance>();
    }

    public void BuildRoom(RoomNode node, System.Random random)
    {
        if (node == null || node.template == null)
        {
            Debug.LogError("BuildRoom: node or template is null!");
            return;
        }

        if (floorTilemap == null)
        {
            Debug.LogError("BuildRoom: floorTilemap is not assigned!");
            return;
        }

        if (groundTile == null)
        {
            Debug.LogError("BuildRoom: groundTile is not assigned!");
            return;
        }

        rng = random;
        floorCells.Clear();

        Vector2Int size = node.template.size;

        floorTilemap.ClearAllTiles();
        if (wallsTilemap != null) wallsTilemap.ClearAllTiles();

        PaintFloor(size);
        PlaceWalls(node, size);
        SpawnPortals(node, size);
        SetupCameraBounds(size);

        var contentGenerator = GetComponent<RoomContentGenerator>();
        if (contentGenerator != null)
        {
            contentGenerator.GenerateContent(node, size, rng, transform);
        }
    }

    private void PaintFloor(Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                floorTilemap.SetTile(pos, PickGroundTile());
                floorCells.Add(pos);
            }
        }
    }

    private TileBase PickGroundTile()
    {
        if (groundVariations != null && groundVariations.Length > 0 && rng.NextDouble() < 0.3)
        {
            return groundVariations[rng.Next(groundVariations.Length)];
        }
        return groundTile;
    }

    private HashSet<Vector3Int> GetPortalPositions(RoomNode node, Vector2Int size)
    {
        var portalPositions = new HashSet<Vector3Int>();
        int halfWidth = portalWidth / 2;

        if (node.HasConnection(Direction.North))
        {
            int centerX = size.x / 2;
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
                portalPositions.Add(new Vector3Int(centerX + dx, size.y - 1, 0));
        }
        if (node.HasConnection(Direction.South))
        {
            int centerX = size.x / 2;
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
                portalPositions.Add(new Vector3Int(centerX + dx, 0, 0));
        }
        if (node.HasConnection(Direction.East))
        {
            int centerY = size.y / 2;
            for (int dy = -halfWidth; dy <= halfWidth; dy++)
                portalPositions.Add(new Vector3Int(size.x - 1, centerY + dy, 0));
        }
        if (node.HasConnection(Direction.West))
        {
            int centerY = size.y / 2;
            for (int dy = -halfWidth; dy <= halfWidth; dy++)
                portalPositions.Add(new Vector3Int(0, centerY + dy, 0));
        }

        return portalPositions;
    }

    private void PlaceWalls(RoomNode node, Vector2Int size)
    {
        if (wallsTilemap == null || wallTiles == null) return;

        var portalPositions = GetPortalPositions(node, size);
        Vector3Int UP = Vector3Int.up, DN = Vector3Int.down, RT = Vector3Int.right, LT = Vector3Int.left;

        foreach (var p in floorCells)
        {
            // Skip portal openings - no wall there
            if (portalPositions.Contains(p)) continue;

            bool n = !floorCells.Contains(p + UP);
            bool s = !floorCells.Contains(p + DN);
            bool e = !floorCells.Contains(p + RT);
            bool w = !floorCells.Contains(p + LT);

            if (!n && !s && !e && !w) continue;

            bool ne = !floorCells.Contains(p + UP + RT);
            bool nw = !floorCells.Contains(p + UP + LT);
            bool se = !floorCells.Contains(p + DN + RT);
            bool sw = !floorCells.Contains(p + DN + LT);

            TileBase t = null;

            if (n && w && !s && !e) t = wallTiles.cornerTopLeft;
            else if (n && e && !s && !w) t = wallTiles.cornerTopRight;
            else if (s && w && !n && !e) t = wallTiles.cornerBottomLeft;
            else if (s && e && !n && !w) t = wallTiles.cornerBottomRight;
            else if (n && !s) t = (rng.NextDouble() < 0.5 ? wallTiles.topA : wallTiles.topB);
            else if (s && !n) t = (rng.NextDouble() < 0.5 ? wallTiles.bottomA : wallTiles.bottomB);
            else if (w && !e) t = wallTiles.left ?? wallTiles.topA;
            else if (e && !w) t = wallTiles.right ?? wallTiles.topA;
            else if (n || s || e || w) t = wallTiles.topA;

            if (t != null)
            {
                wallsTilemap.SetTile(p, t);
            }
            else
            {
                // Inner corners
                if (!n && !w && nw && wallTiles.innerTopLeft != null) wallsTilemap.SetTile(p, wallTiles.innerTopLeft);
                else if (!s && !w && sw && wallTiles.innerBottomLeft != null) wallsTilemap.SetTile(p, wallTiles.innerBottomLeft);
                else if (!s && !e && se && wallTiles.innerBottomRight != null) wallsTilemap.SetTile(p, wallTiles.innerBottomRight);
                else if (!n && !e && ne && wallTiles.innerTopRight != null) wallsTilemap.SetTile(p, wallTiles.innerTopRight);
            }
        }

        // Add portal frame walls (walls on sides of portal opening)
        PlacePortalFrameWalls(node, size);
    }

    private void PlacePortalFrameWalls(RoomNode node, Vector2Int size)
    {
        // Portal opening is 3 tiles. We need to place walls on the portal tiles themselves
        // to cover the floor, since portal positions are skipped during normal wall placement
        int halfWidth = portalWidth / 2;

        // North portal - place walls on the portal opening tiles (they have floor but need wall overlay)
        if (node.HasConnection(Direction.North))
        {
            int centerX = size.x / 2;
            int y = size.y - 1;
            // Place walls on the left and right tiles of the portal opening
            wallsTilemap.SetTile(new Vector3Int(centerX - halfWidth, y, 0), wallTiles.topA);
            wallsTilemap.SetTile(new Vector3Int(centerX + halfWidth, y, 0), wallTiles.topA);
        }

        // South portal
        if (node.HasConnection(Direction.South))
        {
            int centerX = size.x / 2;
            int y = 0;
            wallsTilemap.SetTile(new Vector3Int(centerX - halfWidth, y, 0), wallTiles.bottomA);
            wallsTilemap.SetTile(new Vector3Int(centerX + halfWidth, y, 0), wallTiles.bottomA);
        }

        // East portal
        if (node.HasConnection(Direction.East))
        {
            int centerY = size.y / 2;
            int x = size.x - 1;
            wallsTilemap.SetTile(new Vector3Int(x, centerY - halfWidth, 0), wallTiles.right ?? wallTiles.topA);
            wallsTilemap.SetTile(new Vector3Int(x, centerY + halfWidth, 0), wallTiles.right ?? wallTiles.topA);
        }

        // West portal
        if (node.HasConnection(Direction.West))
        {
            int centerY = size.y / 2;
            int x = 0;
            wallsTilemap.SetTile(new Vector3Int(x, centerY - halfWidth, 0), wallTiles.left ?? wallTiles.topA);
            wallsTilemap.SetTile(new Vector3Int(x, centerY + halfWidth, 0), wallTiles.left ?? wallTiles.topA);
        }
    }

    private void SetupCameraBounds(Vector2Int size)
    {
        var cameraBounds = GetComponent<PolygonCollider2D>();
        if (cameraBounds == null)
        {
            cameraBounds = gameObject.AddComponent<PolygonCollider2D>();
        }

        cameraBounds.isTrigger = true;

        float pad = 0.5f;
        Vector2 min = new Vector2(-pad, -pad);
        Vector2 max = new Vector2(size.x + pad, size.y + pad);

        cameraBounds.SetPath(0, new Vector2[]
        {
            new Vector2(min.x, min.y),
            new Vector2(min.x, max.y),
            new Vector2(max.x, max.y),
            new Vector2(max.x, min.y)
        });

        // Assign to RoomInstance if present
        if (roomInstance != null)
        {
            var field = typeof(RoomInstance).GetField("cameraBounds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(roomInstance, cameraBounds);
            }
        }
    }

    private void SpawnPortals(RoomNode node, Vector2Int size)
    {
        if (areaExitPrefab == null)
        {
            Debug.LogWarning("AreaExit prefab not assigned - portals won't be created");
            return;
        }

        // Create portals container
        var portalsContainer = new GameObject("Portals");
        portalsContainer.transform.SetParent(transform);
        portalsContainer.transform.localPosition = Vector3.zero;

        // Spawn portal for each connection
        Direction[] directions = { Direction.North, Direction.East, Direction.South, Direction.West };
        foreach (var dir in directions)
        {
            if (node.HasConnection(dir))
            {
                var targetRoom = node.GetConnection(dir);
                SpawnPortalPair(node, targetRoom, dir, size, portalsContainer.transform);
            }
        }
    }

    private void SpawnPortalPair(RoomNode sourceRoom, RoomNode targetRoom, Direction dir, Vector2Int size, Transform parent)
    {
        // Portal ID: Use consistent naming so both ends of the connection share the same ID
        // This allows AreaExit to find the matching AreaEntrance in the other room
        int minId = Mathf.Min(sourceRoom.id, targetRoom.id);
        int maxId = Mathf.Max(sourceRoom.id, targetRoom.id);
        string portalId = $"Portal_{minId}_{maxId}";

        // Calculate portal position at wall edge
        Vector3 exitPos = GetPortalWorldPosition(dir, size);

        // Rotation: The AreaExit prefab default has entrance at (-1.76, 0) meaning player
        // spawns to the LEFT of the trigger. We rotate based on wall direction:
        // - East wall (player moving right/east): 0°
        // - West wall (player moving left/west): 180°
        // - North wall (player moving up/north): 90°
        // - South wall (player moving down/south): -90°
        float rotation = dir switch
        {
            Direction.North => 90f,
            Direction.South => -90f,
            Direction.East => 0f,
            Direction.West => 180f,
            _ => 0f
        };

        // Spawn the AreaExit prefab
        var portalObj = Instantiate(areaExitPrefab, parent);
        portalObj.name = $"Portal_{dir}_to_Room{targetRoom.id}";
        portalObj.transform.localPosition = exitPos;
        portalObj.transform.localRotation = Quaternion.Euler(0, 0, rotation);

        var areaExit = portalObj.GetComponent<AreaExit>();
        if (areaExit != null)
        {
            areaExit.sceneTransitionName = portalId;
        }

        var areaEntrance = portalObj.GetComponentInChildren<AreaEntrance>();
        if (areaEntrance != null)
        {
            var field = typeof(AreaEntrance).GetField("transitionName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(areaEntrance, portalId);
            }
        }

        // Adjust collider size to match portal opening
        var collider = portalObj.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.offset = Vector2.zero;
            collider.size = (dir == Direction.North || dir == Direction.South)
                ? new Vector2(portalWidth, 0.8f)
                : new Vector2(0.8f, portalWidth);
        }
    }

    private Vector3 GetPortalWorldPosition(Direction dir, Vector2Int size)
    {
        // Position at the center of the wall opening
        float centerX = size.x / 2f;
        float centerY = size.y / 2f;
        return dir switch
        {
            Direction.North => new Vector3(centerX, size.y - 0.5f, 0),
            Direction.South => new Vector3(centerX, 0.5f, 0),
            Direction.East => new Vector3(size.x - 0.5f, centerY, 0),
            Direction.West => new Vector3(0.5f, centerY, 0),
            _ => Vector3.zero
        };
    }

    public Vector3 GetPortalPosition(Direction dir, Vector2Int roomSize)
    {
        return GetPortalWorldPosition(dir, roomSize);
    }

    public int PortalWidth => portalWidth;
}
