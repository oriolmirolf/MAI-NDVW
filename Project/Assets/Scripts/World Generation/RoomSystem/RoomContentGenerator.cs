using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates room content matching the original CombatRoomPopulator + CADecorator system
/// </summary>
public class RoomContentGenerator : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private ChapterTheme theme;

    [Header("Blocking Obstacles (Trees, Large Bushes)")]
    [SerializeField] private int minObstacles = 2;
    [SerializeField] private int maxObstacles = 6;
    [SerializeField] private float obstaclePlacementMargin = 2f;

    [Header("Torches")]
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private int minTorches = 1;
    [SerializeField] private int maxTorches = 2;

    [Header("CA Decoration Settings")]
    [SerializeField] private float portalMargin = 2f;

    private HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();
    private Transform decorationsContainer;

    public void GenerateContent(RoomNode node, Vector2Int roomSize, System.Random rng, Transform parent)
    {
        occupiedPositions.Clear();

        decorationsContainer = new GameObject("Decorations").transform;
        decorationsContainer.SetParent(parent);
        decorationsContainer.localPosition = Vector3.zero;

        // Mark borders as occupied (walls are on edges)
        MarkBorders(roomSize);

        // Mark portal and center positions as occupied
        MarkPortalPositions(node, roomSize);
        MarkArea(new Vector2Int(roomSize.x / 2, roomSize.y / 2), 3f);

        // 1. Place blocking obstacles (Trees, large bushes) - same as CombatRoomPopulator
        PlaceBlockingObstacles(roomSize, rng);

        // 2. Place torches
        PlaceTorches(roomSize, rng);

        // 3. CA-based non-blocking decorations (small bushes) - same as CADecorator
        GenerateCADecorations(node, roomSize, rng);
    }

    private void MarkBorders(Vector2Int roomSize)
    {
        // Mark all edge tiles as occupied (where walls are)
        for (int x = 0; x < roomSize.x; x++)
        {
            occupiedPositions.Add(new Vector3Int(x, 0, 0));              // Bottom edge
            occupiedPositions.Add(new Vector3Int(x, roomSize.y - 1, 0)); // Top edge
        }
        for (int y = 0; y < roomSize.y; y++)
        {
            occupiedPositions.Add(new Vector3Int(0, y, 0));              // Left edge
            occupiedPositions.Add(new Vector3Int(roomSize.x - 1, y, 0)); // Right edge
        }
    }

    private void MarkPortalPositions(RoomNode node, Vector2Int roomSize)
    {
        if (node.HasConnection(Direction.North))
            MarkArea(new Vector2Int(roomSize.x / 2, roomSize.y - 1), portalMargin);
        if (node.HasConnection(Direction.South))
            MarkArea(new Vector2Int(roomSize.x / 2, 0), portalMargin);
        if (node.HasConnection(Direction.East))
            MarkArea(new Vector2Int(roomSize.x - 1, roomSize.y / 2), portalMargin);
        if (node.HasConnection(Direction.West))
            MarkArea(new Vector2Int(0, roomSize.y / 2), portalMargin);
    }

    private void MarkArea(Vector2Int center, float radius)
    {
        int r = Mathf.CeilToInt(radius);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    occupiedPositions.Add(new Vector3Int(center.x + x, center.y + y, 0));
                }
            }
        }
    }

    private void PlaceBlockingObstacles(Vector2Int roomSize, System.Random rng)
    {
        GameObject[] obstacles = theme != null ? theme.blockingObstacles : null;
        if (obstacles == null || obstacles.Length == 0) return;

        int count = rng.Next(minObstacles, maxObstacles + 1);
        int placed = 0;
        int maxAttempts = count * 3;

        for (int attempt = 0; attempt < maxAttempts && placed < count; attempt++)
        {
            Vector3 pos = GetRandomPosition(roomSize, rng, obstaclePlacementMargin);
            Vector3Int gridPos = Vector3Int.FloorToInt(pos);

            if (occupiedPositions.Contains(gridPos)) continue;

            GameObject prefab = obstacles[rng.Next(obstacles.Length)];
            if (prefab == null) continue;

            var obstacle = Instantiate(prefab, decorationsContainer);
            obstacle.transform.localPosition = pos;
            obstacle.name = $"Obstacle_{placed}";

            MarkArea(new Vector2Int(gridPos.x, gridPos.y), 1.5f);
            placed++;
        }
    }

    private void PlaceTorches(Vector2Int roomSize, System.Random rng)
    {
        if (torchPrefab == null) return;

        int count = rng.Next(minTorches, maxTorches + 1);
        int placed = 0;
        int maxAttempts = count * 5;

        for (int attempt = 0; attempt < maxAttempts && placed < count; attempt++)
        {
            // Torches near walls
            Vector3 pos = GetWallAdjacentPosition(roomSize, rng);
            Vector3Int gridPos = Vector3Int.FloorToInt(pos);

            if (occupiedPositions.Contains(gridPos)) continue;

            var torch = Instantiate(torchPrefab, decorationsContainer);
            torch.transform.localPosition = pos;
            torch.name = $"Torch_{placed}";

            occupiedPositions.Add(gridPos);
            placed++;
        }
    }

    private Vector3 GetWallAdjacentPosition(Vector2Int roomSize, System.Random rng)
    {
        // Place torches 1-2 tiles from walls
        int side = rng.Next(4);
        float x, y;

        switch (side)
        {
            case 0: // North wall
                x = 2 + (float)rng.NextDouble() * (roomSize.x - 4);
                y = roomSize.y - 2;
                break;
            case 1: // South wall
                x = 2 + (float)rng.NextDouble() * (roomSize.x - 4);
                y = 2;
                break;
            case 2: // East wall
                x = roomSize.x - 2;
                y = 2 + (float)rng.NextDouble() * (roomSize.y - 4);
                break;
            default: // West wall
                x = 2;
                y = 2 + (float)rng.NextDouble() * (roomSize.y - 4);
                break;
        }

        return new Vector3(x + 0.5f, y + 0.5f, 0);
    }

    private void GenerateCADecorations(RoomNode node, Vector2Int roomSize, System.Random rng)
    {
        GameObject[] decorations = theme != null ? theme.nonBlockingDecorations : null;
        if (decorations == null || decorations.Length == 0) return;

        float density = theme != null ? theme.decorationDensity : 0.3f;
        int iterations = theme != null ? theme.caIterations : 5;
        int surviveMin = theme != null ? theme.caSurviveMin : 4;
        int birthMin = theme != null ? theme.caBirthMin : 3;

        // Add margin to avoid spawning on borders (1 tile wall margin)
        int margin = 1;
        int startX = margin;
        int startY = margin;
        int width = roomSize.x - 2 * margin;
        int height = roomSize.y - 2 * margin;

        if (width <= 0 || height <= 0) return;

        // Create initial grid
        bool[,] grid = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = rng.NextDouble() < density;
            }
        }

        // Apply CA rules
        for (int i = 0; i < iterations; i++)
        {
            grid = ApplyCARules(grid, surviveMin, birthMin);
        }

        // Place decorations (same as CADecorator - 40% chance after CA)
        int placedCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y]) continue;

                // Offset by margin to get actual room position
                Vector3Int worldPos = new Vector3Int(startX + x, startY + y, 0);
                if (occupiedPositions.Contains(worldPos)) continue;

                if (rng.NextDouble() > 0.6f)
                {
                    GameObject prefab = decorations[rng.Next(decorations.Length)];
                    Vector3 spawnPos = new Vector3(worldPos.x + 0.5f, worldPos.y + 0.5f, 0);

                    var decoration = Instantiate(prefab, decorationsContainer);
                    decoration.transform.localPosition = spawnPos;
                    decoration.name = $"Decoration_{x}_{y}";

                    occupiedPositions.Add(worldPos);
                    placedCount++;
                }
            }
        }
    }

    private bool[,] ApplyCARules(bool[,] oldGrid, int surviveMin, int birthMin)
    {
        int width = oldGrid.GetLength(0);
        int height = oldGrid.GetLength(1);
        bool[,] newGrid = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbors = CountNeighbors(oldGrid, x, y);
                if (oldGrid[x, y])
                    newGrid[x, y] = neighbors >= surviveMin;
                else
                    newGrid[x, y] = neighbors >= birthMin;
            }
        }

        return newGrid;
    }

    private int CountNeighbors(bool[,] grid, int x, int y)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && grid[nx, ny])
                    count++;
            }
        }

        return count;
    }

    private Vector3 GetRandomPosition(Vector2Int roomSize, System.Random rng, float margin)
    {
        float x = margin + (float)rng.NextDouble() * (roomSize.x - 2 * margin);
        float y = margin + (float)rng.NextDouble() * (roomSize.y - 2 * margin);
        return new Vector3(x + 0.5f, y + 0.5f, 0);
    }

    public Transform DecorationsContainer => decorationsContainer;
}
