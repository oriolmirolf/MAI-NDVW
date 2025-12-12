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
    [SerializeField] private int minEnemies = 2;
    [SerializeField] private int maxEnemies = 5;

    public void PopulateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Tilemap pathTilemap,
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

        // STEP 5: Paths (will avoid water by checking occupiedPositions)
        PathGenerator.GeneratePaths(roomData, theme, rng, floorTilemap, pathTilemap, occupiedPositions);

        // STEP 6: Place enemies on shores (not in water)
        PlaceEnemiesOnShore(roomData, theme, rng, enemiesParent, occupiedPositions, waterCells);

        // STEP 7: CA decorations around lake (shore plants)
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

        // Step 4: Clean up orphan tiles (same as paths)
        Debug.Log($"Generated {waterCells.Count} water cells before cleanup");
        waterCells = CleanupOrphanTiles(waterCells, minNeighborsToKeep: 3, maxIterations: 2);
        Debug.Log($"Water cells after cleanup: {waterCells.Count}");

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
        if (theme.waterRuleTile == null)
        {
            Debug.LogWarning("No water RuleTile assigned - skipping water placement");
            return;
        }

        // Use water tilemap if assigned, otherwise fall back to floor tilemap
        Tilemap targetTilemap = waterTilemap != null ? waterTilemap : floorTilemap;

        // Place all water tiles using RuleTile
        foreach (var waterPos in waterCells)
        {
            targetTilemap.SetTile(waterPos, theme.waterRuleTile);
        }

        // Refresh the tilemap to trigger RuleTile border updates
        targetTilemap.RefreshAllTiles();

        Debug.Log($"Placed {waterCells.Count} water tiles with RuleTile on tilemap: {targetTilemap.name}");
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

    /// <summary>
    /// Remove orphan water tiles that don't have enough water neighbors
    /// This prevents RuleTile border rendering artifacts on isolated tiles
    /// Runs iteratively until no more tiles are removed (up to maxIterations)
    /// </summary>
    private HashSet<Vector3Int> CleanupOrphanTiles(HashSet<Vector3Int> waterCells, int minNeighborsToKeep, int maxIterations = 10)
    {
        HashSet<Vector3Int> currentWater = new HashSet<Vector3Int>(waterCells);
        int totalRemoved = 0;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            HashSet<Vector3Int> cleanedWater = new HashSet<Vector3Int>();
            iteration++;

            foreach (var cell in currentWater)
            {
                // Count how many water neighbors this cell has
                int waterNeighbors = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        // Skip center cell
                        if (dx == 0 && dy == 0) continue;

                        Vector3Int neighbor = new Vector3Int(cell.x + dx, cell.y + dy, 0);

                        if (currentWater.Contains(neighbor))
                        {
                            waterNeighbors++;
                        }
                    }
                }

                // Only keep tile if it has enough water neighbors
                if (waterNeighbors >= minNeighborsToKeep)
                {
                    cleanedWater.Add(cell);
                }
            }

            int removedThisPass = currentWater.Count - cleanedWater.Count;

            // If no tiles were removed, we're done
            if (removedThisPass == 0)
            {
                break;
            }

            Debug.Log($"  Lake cleanup iteration {iteration}: removed {removedThisPass} tiles, {cleanedWater.Count} remaining");

            totalRemoved += removedThisPass;
            currentWater = cleanedWater;
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"Lake cleanup complete: removed {totalRemoved} orphan water tiles in {iteration} iteration(s)");
        }

        // Warning if all water was removed
        if (currentWater.Count == 0 && waterCells.Count > 0)
        {
            Debug.LogWarning($"WARNING: All {waterCells.Count} water tiles were removed by cleanup! Consider adjusting lake CA parameters.");
        }

        return currentWater;
    }
}