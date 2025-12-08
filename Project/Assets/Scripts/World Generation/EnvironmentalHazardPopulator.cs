using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Tilemaps;
using static UnityEngine.Rendering.DebugUI.Table;

/// <summary>
/// Populates rooms with environmental hazards like lakes
/// Uses CA to generate natural-looking water bodies
/// </summary>
[System.Serializable]
public class EnvironmentalHazardPopulator : IArchetypePopulator
{
    [Header("Lake Settings")]
    [SerializeField, Range(0f, 1f)] private float lakeDensity = 0.4f;
    [SerializeField, Range(1, 10)] private int lakeIterations = 6;
    [SerializeField, Range(2, 8)] private int lakeSurviveMin = 5;
    [SerializeField, Range(2, 8)] private int lakeBirthMin = 4;

    [Header("Tilemaps")]
    [Tooltip("Water tilemap for placing water tiles with collision")]
    [SerializeField] private Tilemap waterTilemap;

    [Header("Enemy Settings")]
    [SerializeField] private int minEnemies = 1;
    [SerializeField] private int maxEnemies = 3;

    public void PopulateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Transform objectsParent,
        Transform enemiesParent)
    {
        Debug.Log($"Populating Environmental Hazard Room: Room {roomData.index}");

        HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();

        // STEP 1: Place floor tiles everywhere first
        PlaceFloorTiles(roomData, theme, rng, floorTilemap);

        // STEP 2: Generate lake shape with CA
        HashSet<Vector3Int> waterCells = GenerateLakeWithCA(roomData, rng);

        // STEP 3: Place water tiles over floor tiles
        PlaceWaterTiles(roomData, theme, floorTilemap, waterCells);

        // STEP 4: Mark water cells as occupied (can't place things here)
        foreach (var cell in waterCells)
            occupiedPositions.Add(cell);

        // STEP 5: Place enemies on shores (not in water)
        PlaceEnemiesOnShore(roomData, theme, rng, enemiesParent, occupiedPositions, waterCells);

        // STEP 6: CA decorations around lake (shore plants)
        CADecorator.DecorateRoom(roomData, theme, rng, objectsParent, occupiedPositions);

        Debug.Log($"Environmental Hazard Room {roomData.index} complete: {waterCells.Count} water tiles");
    }

    private void PlaceFloorTiles(RoomData roomData, ChapterTheme theme, System.Random rng, Tilemap tilemap)
    {
        if (theme.mainFloorTile == null)
        {
            Debug.LogError("No main floor tile assigned!");
            return;
        }

        for (int x = roomData.rect.xMin; x < roomData.rect.xMax; x++)
        {
            for (int y = roomData.rect.yMin; y < roomData.rect.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                TileBase tile = theme.mainFloorTile;

                if (theme.floorVariations != null && theme.floorVariations.Length > 0)
                {
                    if (rng.NextDouble() < 0.2f)
                    {
                        tile = theme.floorVariations[rng.Next(theme.floorVariations.Length)];
                    }
                }

                tilemap.SetTile(pos, tile);
            }
        }
    }

    /// <summary>
    /// Generate lake shape using Cellular Automata
    /// Returns set of positions that should be water
    /// </summary>
    private HashSet<Vector3Int> GenerateLakeWithCA(RoomData roomData, System.Random rng)
    {
        int width = roomData.rect.width;
        int height = roomData.rect.height;

        // Step 1: Create initial random grid for lake
        bool[,] grid = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = rng.NextDouble() < lakeDensity;
            }
        }

        // Step 2: Run CA iterations to smooth into lake shape
        for (int i = 0; i < lakeIterations; i++)
        {
            grid = ApplyCARules(grid, lakeSurviveMin, lakeBirthMin);
        }

        // Step 3: Convert grid to world positions
        HashSet<Vector3Int> waterCells = new HashSet<Vector3Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y])
                {
                    Vector3Int worldPos = new Vector3Int(
                        roomData.rect.xMin + x,
                        roomData.rect.yMin + y,
                        0
                    );
                    waterCells.Add(worldPos);
                }
            }
        }

        return waterCells;
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

                if (oldGrid[x, y]) // Cell is water
                {
                    newGrid[x, y] = neighbors >= surviveMin;
                }
                else // Cell is ground
                {
                    newGrid[x, y] = neighbors >= birthMin;
                }
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

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (grid[nx, ny]) count++;
                }
            }
        }

        return count;
    }
    private void PlaceWaterTiles(
        RoomData roomData,
        ChapterTheme theme,
        Tilemap floorTilemap,
        HashSet<Vector3Int> waterCells)
    {
        if (theme.hazardTiles == null || theme.hazardTiles.Length == 0)
        {
            Debug.LogWarning("No hazard tiles assigned - skipping water placement");
            return;
        }

        // Use water tilemap if assigned, otherwise fall back to floor tilemap
        Tilemap targetTilemap = waterTilemap != null ? waterTilemap : floorTilemap;

        TileBase waterTile = theme.hazardTiles[0];

        foreach (var waterPos in waterCells)
        {
            targetTilemap.SetTile(waterPos, waterTile);
        }

        Debug.Log($"Placed {waterCells.Count} water tiles on tilemap: {targetTilemap.name}");
    }
    private void PlaceEnemiesOnShore(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent,
        HashSet<Vector3Int> occupiedPositions,
        HashSet<Vector3Int> waterCells)
    {
        if (theme.commonEnemies == null || theme.commonEnemies.Length == 0)
        {
            Debug.LogWarning("No enemies to place");
            return;
        }

        int count = rng.Next(minEnemies, maxEnemies + 1);
        int attempts = 0;
        int maxAttempts = count * 5;

        for (int i = 0; i < count && attempts < maxAttempts; attempts++)
        {
            // Random position in room
            int x = rng.Next(roomData.rect.xMin + 2, roomData.rect.xMax - 2);
            int y = rng.Next(roomData.rect.yMin + 2, roomData.rect.yMax - 2);
            Vector3Int gridPos = new Vector3Int(x, y, 0);

            // Skip if in water or already occupied
            if (waterCells.Contains(gridPos) || occupiedPositions.Contains(gridPos))
                continue;

            Vector3 spawnPos = new Vector3(x + 0.5f, y + 0.5f, 0);
            GameObject prefab = theme.commonEnemies[rng.Next(theme.commonEnemies.Length)];

            if (prefab != null)
            {
                GameObject enemy = Object.Instantiate(prefab, spawnPos, Quaternion.identity, parent);
                enemy.name = $"ShoreEnemy_{i}";
                occupiedPositions.Add(gridPos);
                i++;
            }
        }
    }
}