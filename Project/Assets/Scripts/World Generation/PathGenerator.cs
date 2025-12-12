using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates paths using Cellular Automata with portal-seeded initial conditions
/// </summary>
public static class PathGenerator
{
    /// <summary>
    /// Generate and place CA-based paths seeded near portals
    /// </summary>
    public static void GeneratePaths(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Tilemap pathTilemap,
        HashSet<Vector3Int> occupiedPositions)
    {
        // Check if paths are enabled
        if (!theme.enablePaths)
        {
            return;
        }

        if (theme.pathRuleTile == null)
        {
            Debug.LogWarning("Path generation enabled but no pathRuleTile assigned in theme!");
            return;
        }

        // Check if room has portals (no portals = no seeded paths)
        if (roomData.portals == null || roomData.portals.Count == 0)
        {
            Debug.Log($"Room {roomData.index} has no portals - skipping path generation");
            return;
        }

        Debug.Log($"Generating paths for room {roomData.index} with {roomData.portals.Count} portals");

        // Generate path cells using CA with portal seeding
        HashSet<Vector3Int> pathCells = GeneratePathsWithCA(roomData, theme, rng, occupiedPositions);
        Debug.Log($"Generated {pathCells.Count} path cells before cleanup");

        // Clean up orphan tiles (tiles with too few path neighbors)
        // Limit to 2 iterations to prevent over-aggressive removal
        pathCells = CleanupOrphanTiles(pathCells, minNeighborsToKeep: 3, maxIterations: 2);
        Debug.Log($"Path cells after cleanup: {pathCells.Count}");

        // Use path tilemap if assigned, otherwise fall back to floor tilemap
        Tilemap targetTilemap = pathTilemap != null ? pathTilemap : floorTilemap;

        // Place floor tiles underneath on floor tilemap
        foreach (var pathPos in pathCells)
        {
            // Ensure floor tile exists on floor tilemap
            if (floorTilemap.GetTile(pathPos) == null)
            {
                floorTilemap.SetTile(pathPos, theme.mainFloorTile);
            }
        }

        // Place path tiles on path tilemap (or floor tilemap if no separate path tilemap)
        foreach (var pathPos in pathCells)
        {
            targetTilemap.SetTile(pathPos, theme.pathRuleTile);
        }

        // Refresh both tilemaps to trigger RuleTile border updates
        floorTilemap.RefreshAllTiles();
        targetTilemap.RefreshAllTiles();

        Debug.Log($"Placed {pathCells.Count} path tiles with RuleTile in room {roomData.index}");
    }

    /// <summary>
    /// Generate path cells using CA with portal seeding
    /// </summary>
    private static HashSet<Vector3Int> GeneratePathsWithCA(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        HashSet<Vector3Int> occupiedPositions)
    {
        // Step 1: Create initial grid with portal-based seeding
        bool[,] grid = CreatePortalSeededGrid(roomData, theme, rng);

        // Step 2: Run CA iterations to smooth into path shapes
        for (int i = 0; i < theme.pathIterations; i++)
        {
            grid = ApplyCARules(grid, theme.pathSurviveMin, theme.pathBirthMin);
        }

        // Step 3: Convert grid to world positions, avoiding occupied positions
        HashSet<Vector3Int> pathCells = new HashSet<Vector3Int>();
        int width = roomData.rect.width;
        int height = roomData.rect.height;

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

                    // Skip if position is already occupied (e.g., by water)
                    if (!occupiedPositions.Contains(worldPos))
                    {
                        pathCells.Add(worldPos);
                    }
                }
            }
        }

        return pathCells;
    }

    /// <summary>
    /// Create initial CA grid with high density near portals, low density elsewhere
    /// </summary>
    private static bool[,] CreatePortalSeededGrid(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng)
    {
        int width = roomData.rect.width;
        int height = roomData.rect.height;
        bool[,] grid = new bool[width, height];

        float radiusSqr = theme.portalSeedRadius * theme.portalSeedRadius;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Convert local grid position to world position for distance calculation
                Vector3Int worldPos = new Vector3Int(
                    roomData.rect.xMin + x,
                    roomData.rect.yMin + y,
                    0
                );

                // Calculate distance to nearest portal
                float distSqr = GetDistanceToNearestPortalSqr(worldPos, roomData);

                // Determine density based on distance to portals
                float density;
                if (distSqr <= radiusSqr)
                {
                    // Near portal: high density
                    density = theme.portalPathDensity;
                }
                else
                {
                    // Away from portals: low density
                    density = theme.ambientPathDensity;
                }

                // Random placement based on density
                grid[x, y] = rng.NextDouble() < density;
            }
        }

        return grid;
    }

    /// <summary>
    /// Calculate squared distance from position to nearest portal
    /// </summary>
    private static float GetDistanceToNearestPortalSqr(Vector3Int gridPos, RoomData roomData)
    {
        float minDistSqr = float.MaxValue;

        foreach (var portal in roomData.portals)
        {
            // Calculate squared distance (avoids sqrt for performance)
            float dx = gridPos.x - portal.cell.x;
            float dy = gridPos.y - portal.cell.y;
            float distSqr = dx * dx + dy * dy;

            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
            }
        }

        return minDistSqr;
    }

    /// <summary>
    /// Apply CA rules for path generation
    /// </summary>
    private static bool[,] ApplyCARules(bool[,] oldGrid, int surviveMin, int birthMin)
    {
        int width = oldGrid.GetLength(0);
        int height = oldGrid.GetLength(1);
        bool[,] newGrid = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbors = CountNeighbors(oldGrid, x, y);

                if (oldGrid[x, y]) // Cell is path
                {
                    // Survive if enough neighbors
                    newGrid[x, y] = neighbors >= surviveMin;
                }
                else // Cell is not path
                {
                    // Be born if enough neighbors
                    newGrid[x, y] = neighbors >= birthMin;
                }
            }
        }

        return newGrid;
    }

    /// <summary>
    /// Count alive neighbors using Moore neighborhood (8 neighbors)
    /// </summary>
    private static int CountNeighbors(bool[,] grid, int x, int y)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                // Skip center cell
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                // Check bounds
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (grid[nx, ny]) count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Remove orphan path tiles that don't have enough path neighbors
    /// This prevents RuleTile border rendering artifacts on isolated tiles
    /// Runs iteratively until no more tiles are removed (up to maxIterations)
    /// </summary>
    private static HashSet<Vector3Int> CleanupOrphanTiles(HashSet<Vector3Int> pathCells, int minNeighborsToKeep, int maxIterations = 10)
    {
        HashSet<Vector3Int> currentPaths = new HashSet<Vector3Int>(pathCells);
        int totalRemoved = 0;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            HashSet<Vector3Int> cleanedPaths = new HashSet<Vector3Int>();
            iteration++;

            foreach (var cell in currentPaths)
            {
                // Count how many path neighbors this cell has
                int pathNeighbors = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        // Skip center cell
                        if (dx == 0 && dy == 0) continue;

                        Vector3Int neighbor = new Vector3Int(cell.x + dx, cell.y + dy, 0);

                        if (currentPaths.Contains(neighbor))
                        {
                            pathNeighbors++;
                        }
                    }
                }

                // Only keep tile if it has enough path neighbors
                if (pathNeighbors >= minNeighborsToKeep)
                {
                    cleanedPaths.Add(cell);
                }
            }

            int removedThisPass = currentPaths.Count - cleanedPaths.Count;

            // If no tiles were removed, we're done
            if (removedThisPass == 0)
            {
                break;
            }

            Debug.Log($"  Cleanup iteration {iteration}: removed {removedThisPass} tiles, {cleanedPaths.Count} remaining");

            totalRemoved += removedThisPass;
            currentPaths = cleanedPaths;
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"Cleanup complete: removed {totalRemoved} orphan path tiles in {iteration} iteration(s)");
        }

        // Warning if all paths were removed
        if (currentPaths.Count == 0 && pathCells.Count > 0)
        {
            Debug.LogWarning($"WARNING: All {pathCells.Count} path tiles were removed by cleanup! Consider adjusting CA parameters or disabling cleanup.");
        }

        return currentPaths;
    }
}
