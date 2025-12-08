using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cinemachine;

[DisallowMultipleComponent]
public class BSPMSTDungeonGenerator : MonoBehaviour
{
    public enum ConnectionType { Corridors, Portals }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap floorTilemap; 
    [SerializeField] private Tilemap pathTilemap;
    [SerializeField] private bool usePathForCorridors = false;
    [SerializeField] private Tilemap wallsTilemap;

    [Header("Background / Outside Fill")]
    [SerializeField] private bool fillOutside = false;
    [SerializeField] private Tilemap outsideTilemap;
    [SerializeField] private TileBase[] outsideTiles;
    [SerializeField, Min(0)] private int outsidePadding = 4;

    [Header("Floor Tiles")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase[] groundTileVariations;
    [Tooltip("Extra floor tiles around rooms that are NOT walkable (visual only)")]
    [SerializeField, Min(0)] private int visualFloorPadding = 2;

    [Header("Connections")]
    public ConnectionType connectionType = ConnectionType.Corridors;
    [SerializeField] private GameObject portalPrefab;
    [Tooltip("Global offset if your sprite is rotated (e.g. 90 or -90)")]
    [SerializeField] private float portalBaseRotation = 0f; 
    [Tooltip("How far the visual corridor opening extends out")]
    [SerializeField, Min(1)] private int portalStubLength = 2; 

    [System.Serializable]
    public class WallTiles
    {
        public TileBase topA, topB, bottomA, bottomB;
        public TileBase cornerTopLeft, cornerTopRight, cornerBottomLeft, cornerBottomRight;
        public TileBase left, right;
        public TileBase innerTopLeft, innerBottomLeft, innerBottomRight, innerTopRight;
    }

    [Header("Walls")]
    [SerializeField] private WallTiles wallTiles;
    [SerializeField] private bool surroundWithWalls = true;

    [Header("Generation")]
    [Min(10)] public int mapWidth  = 140; 
    [Min(10)] public int mapHeight = 140;
    [Min(1)]  public int roomCount = 8;
    [Min(0)]  public int extraConnections = 2;
    [Min(8)]  public int minLeafSize = 36; 
    [Min(4)]  public int minRoomSize = 25;
    [Min(4)]  public int maxRoomSize = 30;
    [Min(1)]  public int roomMargin = 4; 
    [Min(1)]  public int corridorWidth = 2;
    public bool manhattanCorridors = true;
    public bool centerMapAtZero = true;
    public bool clearOnStart = true;
    public bool generateOnStart = true;
    public int seed = 12345;

    [Header("Camera")]
    [SerializeField] private PolygonCollider2D cameraConfiner; 
    [SerializeField] private int backgroundSortingOrder = -10;

    [Header("Camera Bounds Tweaks")]
    [SerializeField] private int   offsetLeftTiles   = 0;   
    [SerializeField] private int   offsetRightTiles  = 0;   
    [SerializeField] private int   offsetTopTiles    = 0;   
    [SerializeField] private int   offsetBottomTiles = 0;   

    [SerializeField] private float offsetLeftWorld   = 0f;  
    [SerializeField] private float offsetRightWorld  = 0f;  
    [SerializeField] private float offsetTopWorld    = 0f;  
    [SerializeField] private float offsetBottomWorld = 0f;  

    private List<IRoomPopulator> populators;
    private System.Random rng;
    private List<Room> rooms;
    private HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
    private Transform objectsParent, enemiesParent, roomsParent;

    private void Awake() {
        populators = GetComponentsInChildren<IRoomPopulator>(includeInactive: true).ToList();
    }

    private void Start() {
        objectsParent = new GameObject("Objects").transform;  objectsParent.SetParent(transform);
        enemiesParent = new GameObject("Enemies").transform;  enemiesParent.SetParent(transform);
        roomsParent   = new GameObject("RoomsBounds").transform; roomsParent.SetParent(transform);
        rng = new System.Random(seed);

        if (generateOnStart) {
            GenerateDungeon();
            MovePlayerAndCamera();
        }
    }

    [ContextMenu("Generate Now")]
    public void GenerateDungeon()
    {
        if (!floorTilemap || !groundTile) { Debug.LogError("Assign Tilemaps!"); return; }

        if (clearOnStart) {
            if (outsideTilemap) outsideTilemap.ClearAllTiles();
            floorTilemap.ClearAllTiles();
            if (pathTilemap)  pathTilemap.ClearAllTiles();
            if (wallsTilemap) wallsTilemap.ClearAllTiles();
            floorCells.Clear();
            foreach(Transform t in objectsParent) Destroy(t.gameObject);
            foreach(Transform t in enemiesParent) Destroy(t.gameObject);
            foreach(Transform t in roomsParent)   Destroy(t.gameObject);
        }

        var bounds = centerMapAtZero
            ? new RectInt(-mapWidth/2, -mapHeight/2, mapWidth, mapHeight)
            : new RectInt(0, 0, mapWidth, mapHeight);

        // 1. BSP Split
        var root = new Leaf(bounds);
        var leaves = new List<Leaf> { root };
        int guard = 0;
        
        while (leaves.Count(l => l.IsLeaf) < roomCount && guard++ < 10000) {
            var cand = leaves.Where(l => l.IsLeaf && (l.bounds.width >= minLeafSize*2 || l.bounds.height >= minLeafSize*2))
                             .OrderByDescending(l => l.bounds.width * l.bounds.height)
                             .FirstOrDefault();
            
            if (cand == null) break; 
            if (cand.Split(rng, minLeafSize)) { leaves.Add(cand.left); leaves.Add(cand.right); }
        }

        // 2. Create Rooms
        rooms = new List<Room>();
        foreach (var leaf in leaves.Where(l => l.IsLeaf).Take(roomCount)) {
            var r = leaf.CreateRoom(rng, minRoomSize, maxRoomSize, roomMargin);
            if (r != null) rooms.Add(r);
        }
        if (rooms.Count == 0) return;

        foreach (var r in rooms) PaintRect(r.rect, usePath:false);

        // 3. Camera Bounds
        if (connectionType == ConnectionType.Portals) {
            foreach (var r in rooms) CreateRoomCollider(r);
        }

        // 4. MST
        var edges = BuildAllEdges(rooms);
        var mst = Kruskal(rooms.Count, edges);
        
        var adjacency = new Dictionary<int, HashSet<int>>();
        void AddAdj(int u, int v) {
            if(!adjacency.ContainsKey(u)) adjacency[u] = new HashSet<int>();
            if(!adjacency.ContainsKey(v)) adjacency[v] = new HashSet<int>();
            adjacency[u].Add(v); adjacency[v].Add(u);
        }
        foreach (var e in mst) AddAdj(e.a, e.b);

        var remain = edges.Except(mst, new EdgeComparer()).OrderBy(e => e.weight).ToList();
        int added = 0;
        foreach (var e in remain) {
            if (added >= extraConnections) break;
            bool tri = false;
            if (adjacency.ContainsKey(e.a) && adjacency.ContainsKey(e.b)) {
                foreach (var n in adjacency[e.a]) if (adjacency[e.b].Contains(n)) { tri = true; break; }
            }
            if (tri) continue;
            mst.Add(e); AddAdj(e.a, e.b); added++;
        }

        if (connectionType == ConnectionType.Corridors) {
            foreach (var e in mst) CarveCorridor(rooms[e.a].Center, rooms[e.b].Center);
        }

        // --- 5. PRE-CARVE STUBS ---
        if (connectionType == ConnectionType.Portals) {
            foreach (var e in mst) PreCarvePortalStubs(rooms[e.a], rooms[e.b]);
        }

        // 6. Walls
        if (surroundWithWalls && wallsTilemap) PlaceDecoratedWalls();

        // 7. Visual Padding
        if (visualFloorPadding > 0) PaintVisualPadding();

        // 8. Portals
        if (connectionType == ConnectionType.Portals) {
            foreach (var e in mst) CreatePortalConnection(rooms[e.a], rooms[e.b]);
        }

        if (fillOutside) FillOutside();

        if (populators != null) {
            for (int i = 0; i < rooms.Count; i++) {
                var data = new RoomData { index = i, rect = rooms[i].rect, center = rooms[i].Center };
                foreach (var p in populators) {
                    try { p.Populate(data, rng, objectsParent, enemiesParent, floorTilemap); }
                    catch (System.Exception ex) { Debug.LogError(ex); }
                }
            }
        }

        if (connectionType == ConnectionType.Corridors) UpdateGlobalConfiner();
    }

    private void PaintVisualPadding()
    {
        foreach (var r in rooms)
        {
            int pad = visualFloorPadding;
            for (int x = r.rect.xMin - pad; x < r.rect.xMax + pad; x++)
            {
                for (int y = r.rect.yMin - pad; y < r.rect.yMax + pad; y++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    if (!floorCells.Contains(pos)) floorTilemap.SetTile(pos, PickGroundTile());
                }
            }
        }
    }

    private void PreCarvePortalStubs(Room roomA, Room roomB)
    {
        Vector3Int dir = roomB.Center - roomA.Center;
        PreCarveSingleStub(roomA, dir);
        PreCarveSingleStub(roomB, -dir);
    }

    private void PreCarveSingleStub(Room room, Vector3Int dir)
    {
        Vector3Int startPos = GetWallPos(room, dir);
        Vector3Int fwd = GetDominantDir(dir);
        Vector3Int right = (fwd.x != 0) ? new Vector3Int(0, 1, 0) : new Vector3Int(1, 0, 0);

        for (int i = 0; i <= portalStubLength; i++)
        {
            Vector3Int center = startPos + fwd * i;
            
            // --- WIDTH LOGIC ---
            AddFloor(center);             // center
            AddFloor(center + right);     // +1 (top / right)
            AddFloor(center - right);     // -1 (bottom / left)

            // Extra width ONLY on the top/right side
            AddFloor(center + right * 2); // +2
            // NOTE: removed center - right * 2
        }
    }


    private void AddFloor(Vector3Int pos)
    {
        if (!floorCells.Contains(pos))
        {
            floorCells.Add(pos);
            floorTilemap.SetTile(pos, PickGroundTile());
        }
    }

    private Vector3Int GetDominantDir(Vector3Int dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return (dir.x > 0) ? Vector3Int.right : Vector3Int.left;
        return (dir.y > 0) ? Vector3Int.up : Vector3Int.down;
    }

    private Vector3Int GetWallPos(Room room, Vector3Int dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) {
            // --- HORIZONTAL WALLS (East/West) ---
            // Tweak 'yCenter' to slide the portal Up/Down along the wall
            int yCenter = (room.rect.y + room.rect.height / 2) - 1; // <--- CHANGE THIS (-1, 0, or +1)
            
            return (dir.x > 0) ? new Vector3Int(room.rect.xMax - 1, yCenter, 0) : new Vector3Int(room.rect.xMin, yCenter, 0);
        } else {
            // --- VERTICAL WALLS (North/South) ---
            // Tweak 'xCenter' to slide the portal Left/Right along the wall
            int xCenter = (room.rect.x + room.rect.width / 2) - 1; // <--- CHANGE THIS (-1, 0, or +1)
            
            return (dir.y > 0) ? new Vector3Int(xCenter, room.rect.yMax - 1, 0) : new Vector3Int(xCenter, room.rect.yMin, 0);
        }
}

    private void CreateRoomCollider(Room room)
    {
        GameObject boundsObj = new GameObject($"RoomBounds_{room.rect.center}");
        boundsObj.transform.SetParent(roomsParent);
        boundsObj.layer = LayerMask.NameToLayer("Ignore Raycast");

        var col = boundsObj.AddComponent<PolygonCollider2D>();
        col.isTrigger = true;
        
        float pad = 0.5f; 
        Vector3 bl = floorTilemap.CellToWorld(new Vector3Int(room.rect.xMin, room.rect.yMin, 0));
        Vector3 tr = floorTilemap.CellToWorld(new Vector3Int(room.rect.xMax, room.rect.yMax, 0));
        
        col.SetPath(0, new Vector2[] {
            new Vector2(bl.x - pad, bl.y - pad),
            new Vector2(bl.x - pad, tr.y + pad),
            new Vector2(tr.x + pad, tr.y + pad),
            new Vector2(tr.x + pad, bl.y - pad)
        });

        if (boundsObj.GetComponent<RoomConfiner>() == null)
             boundsObj.AddComponent<RoomConfiner>().bounds = col;
    }

    private void CreatePortalConnection(Room roomA, Room roomB)
    {
        if (!portalPrefab) return;
        string id = $"Portal_{roomA.Center}_{roomB.Center}";
        Vector3Int dir = roomB.Center - roomA.Center;
        
        PlacePortalAtStubStart(roomA, dir, id, "To_" + roomB.Center);
        PlacePortalAtStubStart(roomB, -dir, id, "To_" + roomA.Center);
    }

    private void PlacePortalAtStubStart(Room room, Vector3Int dir, string id, string debugName)
    {
        Vector3Int startPos = GetWallPos(room, dir);
        Vector3Int domDir   = GetDominantDir(dir);

        // Portal sits on the wall line
        Vector3Int pos = startPos;

        // Rotation Logic
        float zRot = 0f;
        if (Mathf.Abs(domDir.x) > Mathf.Abs(domDir.y))
            zRot = (domDir.x > 0) ? 0f : 180f;
        else
            zRot = (domDir.y > 0) ? 90f : -90f;

        zRot += portalBaseRotation;

        // Axis perpendicular to the wall normal (across the opening)
        Vector3Int widthDir = (domDir.x != 0) ? new Vector3Int(0, 1, 0) : new Vector3Int(1, 0, 0);
        bool isVertical = (domDir.y != 0);

        // --- HOLE PUNCHING ---
        // Portal is exactly 2 tiles tall/wide along widthDir:
        //   opening tiles = pos and pos + widthDir
        List<Vector3Int> holeTiles = new List<Vector3Int>
        {
            pos,
            pos + widthDir
        };

        if (wallsTilemap)
        {
            // Clear just the 2 portal tiles
            foreach (var p in holeTiles)
                wallsTilemap.SetTile(p, null);

            // --- CORNER FIXING ---
            // Corners sit immediately outside the 2-tile portal:
            //   bottom/left edge  = pos - widthDir
            //   top/right edge    = pos + widthDir * 2
            Vector3Int nearEdge = pos - widthDir;        // bottom (vertical) or left (horizontal)
            Vector3Int farEdge  = pos + widthDir * 2;    // top (vertical) or right (horizontal)

            FixCornerTile(nearEdge, domDir, isLeftSide: true);
            FixCornerTile(farEdge,  domDir, isLeftSide: false);

            wallsTilemap.RefreshAllTiles();
        }

        // --- SPAWN OBJECT ---
        Vector3 worldPos = floorTilemap.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0);
        float centerShift = 0.5f;
        if (isVertical) centerShift = 0f; // vertical portals are centered differently
        worldPos += (Vector3)widthDir * centerShift;

        GameObject pObj = Instantiate(portalPrefab, worldPos, Quaternion.Euler(0, 0, zRot), objectsParent);
        pObj.name = $"Portal_{debugName}";

        var exit = pObj.GetComponent<AreaExit>();
        if (exit)
        {
            exit.sceneTransitionName = id;
            var f = typeof(AreaExit).GetField("transitionName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(exit, id);
        }

        var ent = pObj.GetComponentInChildren<AreaEntrance>();
        if (ent)
        {
            var f = typeof(AreaEntrance).GetField("transitionName",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(ent, id);
        }
    }



    private void FixCornerTile(Vector3Int pos, Vector3Int facingDir, bool isLeftSide)
    {
        if (!wallsTilemap) return; // just make sure the tilemap exists

        TileBase corner = null;

        if (facingDir == Vector3Int.up) // North Wall
        {
            corner = isLeftSide ? wallTiles.innerTopLeft : wallTiles.innerTopRight;
        }
        else if (facingDir == Vector3Int.down) // South Wall
        {
            corner = isLeftSide ? wallTiles.innerBottomLeft : wallTiles.innerBottomRight;
        }
        else if (facingDir == Vector3Int.right) // East Wall
        {
            corner = isLeftSide ? wallTiles.innerBottomRight : wallTiles.innerTopRight;
        }
        else if (facingDir == Vector3Int.left) // West Wall
        {
            corner = isLeftSide ? wallTiles.innerBottomLeft : wallTiles.innerTopLeft;
        }

        if (corner != null)
            wallsTilemap.SetTile(pos, corner);
    }


    // --- Helpers ---
    private TileBase PickGroundTile() => (groundTileVariations!=null && groundTileVariations.Length>0 && rng.NextDouble()<0.3) ? groundTileVariations[rng.Next(groundTileVariations.Length)] : groundTile;
    private TileBase PickOutsideTile() => (outsideTiles!=null && outsideTiles.Length>0) ? outsideTiles[rng.Next(outsideTiles.Length)] : PickGroundTile();
    
    private void PaintRect(RectInt rect, bool usePath) {
        var tm = (usePath && pathTilemap && usePathForCorridors) ? pathTilemap : floorTilemap;
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++) {
                var p = new Vector3Int(x, y, 0);
                tm.SetTile(p, PickGroundTile());
                floorCells.Add(p);
            }
    }
    private void CarveCorridor(Vector3Int a, Vector3Int b) {
        if (manhattanCorridors) {
            bool h = rng.NextDouble() < 0.5;
            var p = h ? new Vector3Int(b.x, a.y, 0) : new Vector3Int(a.x, b.y, 0);
            PaintLine(a, p); PaintLine(p, b);
        } else PaintLine(a, b);
    }
    private void PaintLine(Vector3Int f, Vector3Int t) {
        if (f.x!=t.x && f.y!=t.y) { var p = new Vector3Int(t.x, f.y, 0); PaintLine(f,p); PaintLine(p,t); return; }
        var tm = (usePathForCorridors && pathTilemap) ? pathTilemap : floorTilemap;
        int xmin = Mathf.Min(f.x, t.x), xmax = Mathf.Max(f.x, t.x), ymin = Mathf.Min(f.y, t.y), ymax = Mathf.Max(f.y, t.y);
        for (int x = xmin-(corridorWidth-1)/2; x <= xmax+corridorWidth/2; x++)
            for (int y = ymin-(corridorWidth-1)/2; y <= ymax+corridorWidth/2; y++) {
                var p = new Vector3Int(x, y, 0);
                tm.SetTile(p, PickGroundTile());
                floorCells.Add(p);
            }
    }
    private void PlaceDecoratedWalls() {
        Vector3Int UP = new Vector3Int(0, 1, 0), DN = new Vector3Int(0,-1, 0), RT = new Vector3Int(1, 0, 0), LT = new Vector3Int(-1,0, 0);
        bool IsFloor(Vector3Int c) => floorCells.Contains(c);
        wallsTilemap.ClearAllTiles();
        foreach (var p in floorCells) {
            bool n = !IsFloor(p + UP), s = !IsFloor(p + DN), e = !IsFloor(p + RT), w = !IsFloor(p + LT);
            if (!n && !s && !e && !w) continue; 
            bool ne = !IsFloor(p + UP + RT), nw = !IsFloor(p + UP + LT), se = !IsFloor(p + DN + RT), sw = !IsFloor(p + DN + LT);
            wallsTilemap.SetTransformMatrix(p, Matrix4x4.identity);
            TileBase t = null;
            if (n && w && !s && !e) t = wallTiles.cornerTopLeft;
            else if (n && e && !s && !w) t = wallTiles.cornerTopRight;
            else if (s && w && !n && !e) t = wallTiles.cornerBottomLeft;
            else if (s && e && !n && !w) t = wallTiles.cornerBottomRight;
            else if (n && !s) t = (rng.NextDouble()<0.5 ? wallTiles.topA : wallTiles.topB);
            else if (s && !n) t = (rng.NextDouble()<0.5 ? wallTiles.bottomA : wallTiles.bottomB);
            else if (w && !e) t = wallTiles.left ?? wallTiles.topA;
            else if (e && !w) t = wallTiles.right ?? wallTiles.topA;
            else if (n || s || e || w) t = wallTiles.topA;
            if (t) wallsTilemap.SetTile(p, t);
            else {
                if (!n && !w && nw && wallTiles.innerTopLeft) wallsTilemap.SetTile(p, wallTiles.innerTopLeft); 
                else if (!s && !w && sw && wallTiles.innerBottomLeft) wallsTilemap.SetTile(p, wallTiles.innerBottomLeft); 
                else if (!s && !e && se && wallTiles.innerBottomRight) wallsTilemap.SetTile(p, wallTiles.innerBottomRight);  
                else if (!n && !e && ne && wallTiles.innerTopRight) wallsTilemap.SetTile(p, wallTiles.innerTopRight);   
            }
        }
    }
    private void FillOutside() {
        var tm = outsideTilemap ? outsideTilemap : floorTilemap;
        if (floorCells.Count == 0) return;
        int minX = floorCells.Min(p => p.x) - outsidePadding;
        int maxX = floorCells.Max(p => p.x) + outsidePadding;
        int minY = floorCells.Min(p => p.y) - outsidePadding;
        int maxY = floorCells.Max(p => p.y) + outsidePadding;
        for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++) {
            var p = new Vector3Int(x, y, 0);
            if (floorCells.Contains(p)) continue;
            tm.SetTile(p, PickOutsideTile());
        }
    }
    private void UpdateGlobalConfiner() {
        if (!cameraConfiner || floorCells.Count == 0) return;
         int minX = floorCells.Min(p => p.x); int maxX = floorCells.Max(p => p.x);
        int minY = floorCells.Min(p => p.y); int maxY = floorCells.Max(p => p.y);
        minX -= offsetLeftTiles; maxX += offsetRightTiles; minY -= offsetBottomTiles; maxY += offsetTopTiles;
        Vector2 min = floorTilemap.CellToWorld(new Vector3Int(minX, minY, 0));
        Vector2 max = floorTilemap.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, 0));
        min.x -= offsetLeftWorld; max.x += offsetRightWorld; min.y -= offsetBottomWorld; max.y += offsetTopWorld;
        
        cameraConfiner.pathCount = 1;
        cameraConfiner.SetPath(0, new Vector2[] { new Vector2(min.x, min.y), new Vector2(min.x, max.y), new Vector2(max.x, max.y), new Vector2(max.x, min.y) });
        var conf = FindObjectOfType<CinemachineConfiner2D>();
        if(conf) conf.InvalidateCache(); 
    }

    private void MovePlayerAndCamera() {
        if (rooms == null || rooms.Count == 0) return;
        var spawn = (Vector3)rooms[0].Center + new Vector3(0.5f, 0.5f, 0);
        if (PlayerController.Instance != null) {
            PlayerController.Instance.transform.position = spawn;
            var vcam = FindObjectOfType<CinemachineVirtualCamera>(true);
            if (vcam != null) vcam.Follow = PlayerController.Instance.transform;
            CameraController.Instance?.SetPlayerCameraFollow();
            UIFade.Instance?.FadeToClear();
        }
    }
    
    // Classes
    private struct Edge { public int a,b; public float weight; public Edge(int A,int B,float W){a=A;b=B;weight=W;} }
    private class EdgeComparer : IEqualityComparer<Edge> {
        public bool Equals(Edge x, Edge y) => (x.a==y.a && x.b==y.b) || (x.a==y.b && x.b==y.a);
        public int GetHashCode(Edge e) { unchecked { return 17 * 23 + Mathf.Min(e.a,e.b).GetHashCode() * 23 + Mathf.Max(e.a,e.b).GetHashCode(); } }
    }
    private List<Edge> BuildAllEdges(List<Room> rs) {
        var l = new List<Edge>();
        for(int i=0;i<rs.Count;i++) for(int j=i+1;j<rs.Count;j++) l.Add(new Edge(i,j,Vector3Int.Distance(rs[i].Center, rs[j].Center)));
        l.Sort((a,b)=>a.weight.CompareTo(b.weight)); return l;
    }
    private List<Edge> Kruskal(int n, List<Edge> edges) {
        var uf = new UnionFind(n); var res = new List<Edge>();
        foreach(var e in edges) if(uf.Union(e.a,e.b)) res.Add(e);
        return res;
    }
    private class UnionFind {
        int[] p; public UnionFind(int n){p=new int[n];for(int i=0;i<n;i++)p[i]=i;}
        int Find(int x)=>(p[x]==x)?x:(p[x]=Find(p[x]));
        public bool Union(int a,int b){a=Find(a);b=Find(b);if(a!=b){p[b]=a;return true;}return false;}
    }

    private class Leaf {
        public RectInt bounds; public Leaf left,right; public bool IsLeaf => left==null;
        public Leaf(RectInt b){ bounds=b; }
        public bool Split(System.Random rng, int min) {
            if(!IsLeaf) return false;
            bool h = (bounds.width/bounds.height >= 1.25) ? false : (bounds.height/bounds.width >= 1.25) ? true : (rng.NextDouble()<0.5);
            if ((h && bounds.height < min*2) || (!h && bounds.width < min*2)) return false;
            int s = rng.Next(min, (h ? bounds.height : bounds.width) - min + 1);
            left = new Leaf(h ? new RectInt(bounds.x, bounds.y, bounds.width, s) : new RectInt(bounds.x, bounds.y, s, bounds.height));
            right = new Leaf(h ? new RectInt(bounds.x, bounds.y + s, bounds.width, bounds.height - s) : new RectInt(bounds.x + s, bounds.y, bounds.width - s, bounds.height));
            return true;
        }
        public Room CreateRoom(System.Random rng, int min, int max, int margin) {
            int aw = bounds.width - 2 - (margin*2);
            int ah = bounds.height - 2 - (margin*2);
            if(aw<min||ah<min) return null;
            int w = rng.Next(min, Mathf.Min(aw, max)+1);
            int h = rng.Next(min, Mathf.Min(ah, max)+1);
            int x = bounds.x + margin + rng.Next(1, bounds.width - w - margin*2);
            int y = bounds.y + margin + rng.Next(1, bounds.height - h - margin*2);
            return new Room(new RectInt(x, y, w, h));
        }
    }
    private class Room { public RectInt rect; public Vector3Int Center => new Vector3Int((int)rect.center.x, (int)rect.center.y, 0); public Room(RectInt r){rect=r;} }
    private bool HasSplittable(List<Leaf> l) => l.Any(x=>x.IsLeaf && (x.bounds.width >= minLeafSize*2 || x.bounds.height >= minLeafSize*2));
}