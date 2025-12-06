using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cellular Automata system for decorative element placement
/// </summary>
public static class CADecorator
{
    public static void DecorateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform decorationsParent,
        HashSet<Vector3Int> occupiedPositions = null)
    {
        if (theme.nonBlockingDecorations == null || theme.nonBlockingDecorations.Length == 0)
        {
            Debug.Log($"No non-blocking decorations defined in theme for room {roomData.index}");
            return;
        }

        occupiedPositions = occupiedPositions ?? new HashSet<Vector3Int>();

        int width = roomData.rect.width;
        int height = roomData.rect.height;

        Debug.Log($"CA Decorator: Decorating room {roomData.index} ({width}x{height}) with {theme.nonBlockingDecorations.Length} decoration types");

        bool[,] grid = CreateInitialGrid(width, height, theme.decorationDensity, rng);

        for (int i = 0; i < theme.caIterations; i++)
        {
            grid = ApplyCARules(grid, theme.caSurviveMin, theme.caBirthMin);
        }

        int decorationsPlaced = PlaceDecorations(grid, roomData, theme, rng, decorationsParent, occupiedPositions);

        Debug.Log($"CA Decorator: Placed {decorationsPlaced} decorations in room {roomData.index}");
    }

    private static bool[,] CreateInitialGrid(int width, int height, float density, System.Random rng)
    {
        bool[,] grid = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = rng.NextDouble() < density;
            }
        }

        return grid;
    }

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

                if (oldGrid[x, y])
                {
                    newGrid[x, y] = neighbors >= surviveMin;
                }
                else
                {
                    newGrid[x, y] = neighbors >= birthMin;
                }
            }
        }

        return newGrid;
    }

    private static int CountNeighbors(bool[,] grid, int x, int y)
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

    private static int PlaceDecorations(
        bool[,] grid,
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent,
        HashSet<Vector3Int> occupiedPositions)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        int placedCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y]) continue;

                Vector3Int worldPos = new Vector3Int(
                    roomData.rect.xMin + x,
                    roomData.rect.yMin + y,
                    0
                );

                if (occupiedPositions.Contains(worldPos))
                    continue;

                if (rng.NextDouble() > 0.6f)
                {
                    GameObject prefab = theme.nonBlockingDecorations[rng.Next(theme.nonBlockingDecorations.Length)];
                    Vector3 spawnPos = new Vector3(worldPos.x + 0.5f, worldPos.y + 0.5f, 0);
                    Quaternion rotation = Quaternion.identity;

                    GameObject decoration = Object.Instantiate(prefab, spawnPos, rotation, parent);
                    decoration.name = $"Decoration_{x}_{y}";

                    occupiedPositions.Add(worldPos);
                    placedCount++;
                }
            }
        }

        return placedCount;
    }
}