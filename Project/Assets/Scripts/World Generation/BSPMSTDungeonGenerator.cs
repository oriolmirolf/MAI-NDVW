using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Cinemachine;

[DisallowMultipleComponent]
public class BSPMSTDungeonGenerator : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap floorTilemap; 
    [SerializeField] private Tilemap pathTilemap;
    [SerializeField] private bool usePathForCorridors = false;
    [SerializeField] private Tilemap wallsTilemap;

    [Header("Background / Outside Fill")]
    [SerializeField] private bool fillOutside = true;
    [SerializeField] private Tilemap outsideTilemap;
    [SerializeField] private TileBase[] outsideTiles;
    [SerializeField, Min(0)] private int outsidePadding = 4;

    [Header("Floor Tiles (same semantics as your RoomGenerator)")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase[] groundTileVariations;

    [System.Serializable]
    public class WallTiles
    {
        public TileBase topA;
        public TileBase topB; 
        public TileBase bottomA;
        public TileBase bottomB;

        public TileBase cornerTopLeft;
        public TileBase cornerTopRight;
        public TileBase cornerBottomLeft;
        public TileBase cornerBottomRight;

        public TileBase left;
        public TileBase right;

        public TileBase innerTopLeft;
        public TileBase innerBottomLeft;
        public TileBase innerBottomRight;
        public TileBase innerTopRight;
    }

    [Header("Walls")]
    [SerializeField] private WallTiles wallTiles;
    [SerializeField] private bool surroundWithWalls = true;

    [Header("Generation")]
    [Min(10)] public int mapWidth  = 120;
    [Min(10)] public int mapHeight = 80;
    [Min(1)]  public int roomCount = 12;
    [Min(0)]  public int extraConnections = 2;
    [Min(8)]  public int minLeafSize = 14;
    [Min(4)]  public int minRoomSize = 6;
    [Min(4)]  public int maxRoomSize = 18;
    [Min(1)]  public int corridorWidth = 2;
    public bool manhattanCorridors = true;
    public bool centerMapAtZero = true;
    public bool clearOnStart = true;
    public bool generateOnStart = true;
    public int seed = 12345;

    [Header("Camera ")]
    [SerializeField] private PolygonCollider2D cameraConfiner;
    [SerializeField] private float confinerPadding = 1.0f;
    [SerializeField] private int confinerInsetTiles = 2;

    [Header("Camera Bounds Tweaks")]
    [SerializeField] private int   offsetLeftTiles   = 0;   // + expands left, - tightens
    [SerializeField] private int   offsetRightTiles  = 0;   // + expands right, - tightens
    [SerializeField] private int   offsetTopTiles    = 0;   // + expands top, - tightens
    [SerializeField] private int   offsetBottomTiles = 0;   // + expands bottom, - tightens

    [SerializeField] private float offsetLeftWorld   = 0f;  // + expands left, - tightens
    [SerializeField] private float offsetRightWorld  = 0f;  // + expands right, - tightens
    [SerializeField] private float offsetTopWorld    = 0f;  // + expands top, - tightens
    [SerializeField] private float offsetBottomWorld = 0f;  // + expands bottom, - tightens

    [Header("Sorting (Background < Floor < Walls)")]
    [SerializeField] private bool forceSortingOrders = true;
    [SerializeField] private int backgroundSortingOrder = -10;
    [SerializeField] private int floorSortingOrder = 0;
    [SerializeField] private int wallsSortingOrder = 10;

    private List<IRoomPopulator> populators;

    private System.Random rng;
    private List<Room> rooms;
    private HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
    private Transform objectsParent, enemiesParent;

    private void Awake()
    {
        populators = GetComponentsInChildren<IRoomPopulator>(includeInactive: true).ToList();
    }

    private void Start()
    {
        objectsParent = new GameObject("Objects").transform;  objectsParent.SetParent(transform);
        enemiesParent = new GameObject("Enemies").transform;  enemiesParent.SetParent(transform);
        rng = new System.Random(seed);

        if (generateOnStart)
        {
            GenerateDungeon();
            MovePlayerAndCamera();
        }
    }

    [ContextMenu("Generate Now")]
    public void GenerateDungeon()
    {
        if (!floorTilemap || !groundTile)
        {
            Debug.LogError("Assign Floor Tilemap and Ground Tile on BSPMSTDungeonGenerator.");
            return;
        }

        if (clearOnStart)
        {
            if (outsideTilemap) outsideTilemap.ClearAllTiles();
            floorTilemap.ClearAllTiles();
            if (pathTilemap)  pathTilemap.ClearAllTiles();
            if (wallsTilemap) wallsTilemap.ClearAllTiles();
            floorCells.Clear();
        }

        var bounds = centerMapAtZero
            ? new RectInt(-mapWidth/2, -mapHeight/2, mapWidth, mapHeight)
            : new RectInt(0, 0, mapWidth, mapHeight);

        var root   = new Leaf(bounds);
        var leaves = new List<Leaf> { root };
        int guard  = 0;
        while (HasSplittable(leaves) && leaves.Count < roomCount && guard++ < 10000)
        {
            var cand = leaves.Where(l => l.IsLeaf && (l.bounds.width >= minLeafSize*2 || l.bounds.height >= minLeafSize*2))
                             .OrderByDescending(l => l.bounds.width * l.bounds.height)
                             .FirstOrDefault();
            if (cand == null) break;
            if (cand.Split(rng, minLeafSize)) { leaves.Add(cand.left); leaves.Add(cand.right); }
        }

        rooms = new List<Room>();
        foreach (var leaf in leaves.Where(l => l.IsLeaf)
                                   .OrderByDescending(l => l.bounds.width * l.bounds.height)
                                   .Take(roomCount))
        {
            var r = leaf.CreateRoom(rng, minRoomSize, maxRoomSize);
            if (r != null) rooms.Add(r);
        }
        if (rooms.Count == 0) { Debug.LogWarning("No rooms generated."); return; }

        foreach (var r in rooms) PaintRect(r.rect, usePath:false);

        var edges = BuildAllEdges(rooms);
        var mst   = Kruskal(rooms.Count, edges);
        var remain = edges.Except(mst, new EdgeComparer()).OrderBy(e => e.weight).ToList();
        for (int i = 0; i < Mathf.Min(extraConnections, remain.Count); i++) mst.Add(remain[i]);

        foreach (var e in mst) CarveCorridor(rooms[e.a].Center, rooms[e.b].Center);

        if (surroundWithWalls && wallsTilemap && wallTiles != null) PlaceDecoratedWalls();

        if (fillOutside) FillOutside(bounds);

        if (populators != null && populators.Count > 0)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var data = new RoomData { index = i, rect = rooms[i].rect, center = rooms[i].Center };
                foreach (var p in populators)
                {
                    try { p.Populate(data, rng, objectsParent, enemiesParent, floorTilemap); }
                    catch (System.Exception ex) { Debug.LogError(ex); }
                }
            }
        }

        UpdateCameraConfiner();
    }

    private void MovePlayerAndCamera()
    {
        if (rooms == null || rooms.Count == 0) return;
        var spawn = (Vector3)rooms[0].Center + new Vector3(0.5f, 0.5f, 0);

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.transform.position = spawn;

            var vcam = FindObjectOfType<CinemachineVirtualCamera>(true);
            if (vcam != null) vcam.Follow = PlayerController.Instance.transform;

            CameraController.Instance?.SetPlayerCameraFollow();
            UIFade.Instance?.FadeToClear();
        }
    }

    private TileBase PickGroundTile()
    {
        if (groundTileVariations == null || groundTileVariations.Length == 0) return groundTile;
        if (rng.NextDouble() < 0.3) return groundTileVariations[rng.Next(groundTileVariations.Length)];
        return groundTile;
    }

    private TileBase PickOutsideTile()
    {
        if (outsideTiles != null && outsideTiles.Length > 0)
            return outsideTiles[rng.Next(outsideTiles.Length)];
        return PickGroundTile();
    }

    private void PaintRect(RectInt rect, bool usePath)
    {
        var tm = (usePath && pathTilemap && usePathForCorridors) ? pathTilemap : floorTilemap;
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                var p = new Vector3Int(x, y, 0);
                tm.SetTile(p, PickGroundTile());
                floorCells.Add(p);
            }
    }

    private void CarveCorridor(Vector3Int a, Vector3Int b)
    {
        if (manhattanCorridors)
        {
            bool horizFirst = rng.NextDouble() < 0.5;
            var pivot = horizFirst ? new Vector3Int(b.x, a.y, 0) : new Vector3Int(a.x, b.y, 0);
            PaintLineRect(a, pivot, corridorWidth, usePath:true);
            PaintLineRect(pivot, b, corridorWidth, usePath:true);
        }
        else PaintLineRect(a, b, corridorWidth, usePath:true);
    }

    private void PaintLineRect(Vector3Int from, Vector3Int to, int thickness, bool usePath)
    {
        if (from.x != to.x && from.y != to.y)
        {
            var pivot = new Vector3Int(to.x, from.y, 0);
            PaintLineRect(from, pivot, thickness, usePath);
            PaintLineRect(pivot, to, thickness, usePath);
            return;
        }

        var tm = (usePath && pathTilemap && usePathForCorridors) ? pathTilemap : floorTilemap;

        int xmin = Mathf.Min(from.x, to.x), xmax = Mathf.Max(from.x, to.x);
        int ymin = Mathf.Min(from.y, to.y), ymax = Mathf.Max(from.y, to.y);

        for (int x = xmin - (thickness - 1) / 2; x <= xmax + thickness / 2; x++)
            for (int y = ymin - (thickness - 1) / 2; y <= ymax + thickness / 2; y++)
            {
                var p = new Vector3Int(x, y, 0);
                tm.SetTile(p, PickGroundTile());
                floorCells.Add(p);
            }
    }

    private void FillOutside(RectInt baseBounds)
    {
        var tm = outsideTilemap ? outsideTilemap : floorTilemap;

        var fill = new RectInt(
            baseBounds.xMin - outsidePadding,
            baseBounds.yMin - outsidePadding,
            baseBounds.width  + outsidePadding * 2,
            baseBounds.height + outsidePadding * 2
        );

        for (int x = fill.xMin; x < fill.xMax; x++)
        {
            for (int y = fill.yMin; y < fill.yMax; y++)
            {
                var p = new Vector3Int(x, y, 0);
                if (floorCells.Contains(p)) continue;           // skip interior
                tm.SetTile(p, PickOutsideTile());
            }
        }
    }

    private void PlaceDecoratedWalls()
    {
        Vector3Int UP = new Vector3Int(0, 1, 0);
        Vector3Int DN = new Vector3Int(0,-1, 0);
        Vector3Int RT = new Vector3Int(1, 0, 0);
        Vector3Int LT = new Vector3Int(-1,0, 0);
        bool IsFloor(Vector3Int c) => floorCells.Contains(c);

        wallsTilemap.ClearAllTiles();

        foreach (var p in floorCells)
        {
            bool voidN = !IsFloor(p + UP);
            bool voidS = !IsFloor(p + DN);
            bool voidE = !IsFloor(p + RT);
            bool voidW = !IsFloor(p + LT);

            bool voidNE = !IsFloor(p + UP + RT);
            bool voidNW = !IsFloor(p + UP + LT);
            bool voidSE = !IsFloor(p + DN + RT);
            bool voidSW = !IsFloor(p + DN + LT);

            wallsTilemap.SetTransformMatrix(p, Matrix4x4.identity);
            TileBase t = null;

            if (voidN && voidW && !voidS && !voidE)          t = wallTiles.cornerTopLeft;
            else if (voidN && voidE && !voidS && !voidW)     t = wallTiles.cornerTopRight;
            else if (voidS && voidW && !voidN && !voidE)     t = wallTiles.cornerBottomLeft;
            else if (voidS && voidE && !voidN && !voidW)     t = wallTiles.cornerBottomRight;

            else if (voidN && !voidS)                        t = (rng.NextDouble()<0.5 ? wallTiles.topA    : wallTiles.topB);
            else if (voidS && !voidN)                        t = (rng.NextDouble()<0.5 ? wallTiles.bottomA : wallTiles.bottomB);
            else if (voidW && !voidE)                        t =  wallTiles.left  ?? wallTiles.topA;
            else if (voidE && !voidW)                        t =  wallTiles.right ?? wallTiles.topA;

            else if (voidN || voidS || voidE || voidW)       t = wallTiles.topA ?? wallTiles.bottomA;

            if (t) wallsTilemap.SetTile(p, t);

            if (!t)
            {
                if (!voidN && !voidW && voidNW && wallTiles.innerTopLeft)
                    wallsTilemap.SetTile(p, wallTiles.innerTopLeft); 
                else if (!voidS && !voidW && voidSW && wallTiles.innerBottomLeft)
                    wallsTilemap.SetTile(p, wallTiles.innerBottomLeft); 
                else if (!voidS && !voidE && voidSE && wallTiles.innerBottomRight)
                    wallsTilemap.SetTile(p, wallTiles.innerBottomRight);  
                else if (!voidN && !voidE && voidNE && wallTiles.innerTopRight)
                    wallsTilemap.SetTile(p, wallTiles.innerTopRight);   
            }
        }
    }

        private void UpdateCameraConfiner()
        {
            if (!cameraConfiner || floorCells.Count == 0) return;

            int minX = floorCells.Min(p => p.x);
            int maxX = floorCells.Max(p => p.x);
            int minY = floorCells.Min(p => p.y);
            int maxY = floorCells.Max(p => p.y);

            minX -= offsetLeftTiles;
            maxX += offsetRightTiles;
            minY -= offsetBottomTiles;
            maxY += offsetTopTiles;

            Vector2 min = floorTilemap.CellToWorld(new Vector3Int(minX,     minY,     0));
            Vector2 max = floorTilemap.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, 0));

            min.x -= offsetLeftWorld;
            max.x += offsetRightWorld;
            min.y -= offsetBottomWorld;
            max.y += offsetTopWorld;

            var vcam   = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>(true);
            float ortho  = vcam ? vcam.m_Lens.OrthographicSize
                                : (Camera.main ? Camera.main.orthographicSize : 7f);
            float aspect = Camera.main ? Camera.main.aspect : 16f / 9f;
            float halfW  = ortho * aspect;
            float halfH  = ortho;

            var conf = FindObjectOfType<Cinemachine.CinemachineConfiner2D>();
            bool confineScreenEdges = false;
            if (conf)
            {
                var t = conf.GetType();
                var p = t.GetProperty("ConfineScreenEdges",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (p != null) confineScreenEdges = (bool)p.GetValue(conf);
                var f = t.GetField("m_ConfineScreenEdges",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) confineScreenEdges = (bool)f.GetValue(conf);
            }

            const float eps = 0.01f;

            if (confineScreenEdges)
            {
                min += Vector2.one * eps;
                max -= Vector2.one * eps;
            }
            else
            {
                min += new Vector2(halfW, halfH);
                max -= new Vector2(halfW, halfH);
            }

            if (max.x <= min.x || max.y <= min.y) return;

            cameraConfiner.pathCount = 1;
            cameraConfiner.SetPath(0, new Vector2[]
            {
                new Vector2(min.x, min.y),
                new Vector2(min.x, max.y),
                new Vector2(max.x, max.y),
                new Vector2(max.x, min.y)
            });

            if (conf)
            {
                var t = conf.GetType();
                var m1 = t.GetMethod("InvalidatePathCache",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var m2 = t.GetMethod("InvalidateCache",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m1 != null) m1.Invoke(conf, null);
                else if (m2 != null) m2.Invoke(conf, null);
                else { conf.enabled = false; conf.enabled = true; }
            }
        }


    private struct Edge { public int a,b; public float weight; public Edge(int A,int B,float W){a=A;b=B;weight=W;} }
    private class EdgeComparer : IEqualityComparer<Edge>
    {
        public bool Equals(Edge x, Edge y) => (x.a==y.a && x.b==y.b) || (x.a==y.b && x.b==y.a);
        public int GetHashCode(Edge e) { unchecked { return (e.a*73856093)^(e.b*19349663);} }
    }
    private List<Edge> BuildAllEdges(List<Room> rs)
    {
        var list = new List<Edge>(rs.Count*rs.Count);
        for (int i=0;i<rs.Count;i++)
            for (int j=i+1;j<rs.Count;j++)
                list.Add(new Edge(i,j, Vector3Int.Distance(rs[i].Center, rs[j].Center)));
        list.Sort((x,y)=>x.weight.CompareTo(y.weight));
        return list;
    }
    private List<Edge> Kruskal(int n, List<Edge> edges)
    {
        var uf = new UnionFind(n);
        var chosen = new List<Edge>(n-1);
        foreach (var e in edges)
            if (uf.Union(e.a,e.b)) { chosen.Add(e); if (chosen.Count==n-1) break; }
        return chosen;
    }
    private class UnionFind
    {
        int[] p,r;
        public UnionFind(int n){ p=new int[n]; r=new int[n]; for(int i=0;i<n;i++) p[i]=i; }
        int Find(int x){ if (p[x]!=x) p[x]=Find(p[x]); return p[x]; }
        public bool Union(int a,int b){
            a=Find(a); b=Find(b); if(a==b) return false;
            if (r[a]<r[b]) { var t=a; a=b; b=t; } p[b]=a; if (r[a]==r[b]) r[a]++; return true;
        }
    }

    private class Leaf
    {
        public RectInt bounds; public Leaf left,right; public Room room;
        public bool IsLeaf => left==null && right==null;
        public Leaf(RectInt b){ bounds=b; }

        public bool Split(System.Random rng,int minLeaf)
        {
            if (!IsLeaf) return false;
            bool splitH;
            if (bounds.width/(float)bounds.height >= 1.25f) splitH = false;
            else if (bounds.height/(float)bounds.width >= 1.25f) splitH = true;
            else splitH = rng.NextDouble() < 0.5;

            if ((splitH && bounds.height < minLeaf*2) || (!splitH && bounds.width < minLeaf*2)) return false;

            if (splitH)
            {
                int s = rng.Next(minLeaf, bounds.height - minLeaf + 1);
                left  = new Leaf(new RectInt(bounds.x, bounds.y, bounds.width, s));
                right = new Leaf(new RectInt(bounds.x, bounds.y + s, bounds.width, bounds.height - s));
            }
            else
            {
                int s = rng.Next(minLeaf, bounds.width - minLeaf + 1);
                left  = new Leaf(new RectInt(bounds.x, bounds.y, s, bounds.height));
                right = new Leaf(new RectInt(bounds.x + s, bounds.y, bounds.width - s, bounds.height));
            }
            return true;
        }

        public Room CreateRoom(System.Random rng,int minRoom,int maxRoom)
        {
            int maxW = Mathf.Clamp(bounds.width -2,  minRoom, maxRoom);
            int maxH = Mathf.Clamp(bounds.height-2,  minRoom, maxRoom);
            if (maxW < minRoom || maxH < minRoom) return null;

            int w = rng.Next(minRoom, maxW+1);
            int h = rng.Next(minRoom, maxH+1);
            int x = bounds.x + rng.Next(1, bounds.width  - w);
            int y = bounds.y + rng.Next(1, bounds.height - h);
            return new Room(new RectInt(x,y,w,h));
        }
    }
    private class Room
    {
        public RectInt rect;
        public Vector3Int Center => new Vector3Int(rect.x + rect.width/2, rect.y + rect.height/2, 0);
        public Room(RectInt r){ rect=r; }
    }

    private bool HasSplittable(List<Leaf> leaves) =>
        leaves.Any(l => l.IsLeaf && (l.bounds.width >= minLeafSize*2 || l.bounds.height >= minLeafSize*2));
}
