using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Populates combat rooms with obstacles, enemies, and decorations
/// </summary>
[System.Serializable]
public class CombatRoomPopulator : IArchetypePopulator
{
    [Header("Combat Settings")]
    [SerializeField] private int minEnemies = 2;
    [SerializeField] private int maxEnemies = 5;
    [SerializeField] private int minObstacles = 2;
    [SerializeField] private int maxObstacles = 6;
    [SerializeField] private float obstaclePlacementMargin = 2f;
    [SerializeField] private float enemyPlacementMargin = 1.5f;

    public void PopulateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Tilemap pathTilemap,
        Transform objectsParent,
        Transform enemiesParent)
    {
        Debug.Log($"Populating Combat Room: Room {roomData.index}");

        HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();

        // 1. Floor tiles (base layer)
        PlaceFloorTiles(roomData, theme, rng, floorTilemap);

        // 2. Paths (terrain, non-blocking) - seeded near portals
        PathGenerator.GeneratePaths(roomData, theme, rng, floorTilemap, pathTilemap, occupiedPositions);

        // 3. Blocking obstacles (avoid paths)
        PlaceBlockingObstacles(roomData, theme, rng, objectsParent, occupiedPositions);

        // 4. Enemies (avoid obstacles, CAN spawn on paths)
        PlaceEnemies(roomData, theme, rng, enemiesParent, occupiedPositions);

        // 5. Decorations (visual variety)
        CADecorator.DecorateRoom(roomData, theme, rng, objectsParent, occupiedPositions);

        Debug.Log($"Combat Room {roomData.index} complete: {occupiedPositions.Count} occupied positions");
    }

    private void PlaceFloorTiles(RoomData roomData, ChapterTheme theme, System.Random rng, Tilemap tilemap)
    {
        if (theme.mainFloorTile == null)
        {
            Debug.LogError("No main floor tile assigned in theme!");
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

    private void PlaceBlockingObstacles(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent,
        HashSet<Vector3Int> occupiedPositions)
    {
        if (theme.blockingObstacles == null || theme.blockingObstacles.Length == 0)
        {
            Debug.LogWarning("No blocking obstacles defined in theme");
            return;
        }

        int count = rng.Next(minObstacles, maxObstacles + 1);
        int attempts = 0;
        int maxAttempts = count * 3;

        for (int i = 0; i < count && attempts < maxAttempts; attempts++)
        {
            Vector3 pos = GetRandomRoomPosition(roomData, rng, obstaclePlacementMargin);

            GameObject prefab = theme.blockingObstacles[rng.Next(theme.blockingObstacles.Length)];

            if (prefab == null)
            {
                Debug.LogWarning("Null obstacle prefab in theme!");
                continue;
            }

            // Get all grid positions this obstacle will occupy
            List<Vector3Int> obstaclePositions = GetObstacleFootprint(prefab, pos);

            // Check if any of these positions are already occupied
            bool canPlace = true;
            foreach (Vector3Int gridPos in obstaclePositions)
            {
                if (occupiedPositions.Contains(gridPos))
                {
                    canPlace = false;
                    break;
                }
            }

            if (!canPlace)
                continue;

            // Place the obstacle
            GameObject obstacle = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            obstacle.name = $"Obstacle_{roomData.index}_{i}";

            // Mark all positions as occupied
            foreach (Vector3Int gridPos in obstaclePositions)
            {
                occupiedPositions.Add(gridPos);
            }

            i++;
        }
    }

    /// <summary>
    /// Calculates all grid positions an obstacle will occupy based on its collider bounds
    /// Includes ALL colliders (building + roof/canopy) and adds padding for visual spacing
    /// </summary>
    private List<Vector3Int> GetObstacleFootprint(GameObject prefab, Vector3 worldPos)
    {
        List<Vector3Int> positions = new List<Vector3Int>();
        float padding = 0.5f; // Add half-tile padding for visual spacing

        // Get ALL colliders (including children like roof/canopy)
        Collider2D[] colliders = prefab.GetComponentsInChildren<Collider2D>();

        if (colliders == null || colliders.Length == 0)
        {
            // If no colliders, just use the center position with padding
            Vector3Int center = Vector3Int.FloorToInt(worldPos);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    positions.Add(new Vector3Int(center.x + x, center.y + y, 0));
                }
            }
            return positions;
        }

        // Process each collider and combine all footprints
        HashSet<Vector3Int> combinedPositions = new HashSet<Vector3Int>();

        foreach (Collider2D collider in colliders)
        {
            Vector3 center = worldPos + (Vector3)collider.offset;

            // For different collider types, calculate the footprint
            if (collider is CircleCollider2D circle)
            {
                float radius = circle.radius + padding;
                int radiusTiles = Mathf.CeilToInt(radius);

                Vector3Int centerTile = Vector3Int.FloorToInt(center);

                // Mark a square around the circle (conservative approach)
                for (int x = -radiusTiles; x <= radiusTiles; x++)
                {
                    for (int y = -radiusTiles; y <= radiusTiles; y++)
                    {
                        combinedPositions.Add(new Vector3Int(centerTile.x + x, centerTile.y + y, 0));
                    }
                }
            }
            else if (collider is BoxCollider2D box)
            {
                Vector2 size = box.size + Vector2.one * padding * 2; // Add padding to all sides
                Vector3 boxCenter = worldPos + (Vector3)box.offset;

                // Calculate min and max bounds
                Vector3Int minTile = Vector3Int.FloorToInt(boxCenter - (Vector3)size / 2);
                Vector3Int maxTile = Vector3Int.FloorToInt(boxCenter + (Vector3)size / 2);

                for (int x = minTile.x; x <= maxTile.x; x++)
                {
                    for (int y = minTile.y; y <= maxTile.y; y++)
                    {
                        combinedPositions.Add(new Vector3Int(x, y, 0));
                    }
                }
            }
            else if (collider is CapsuleCollider2D capsule)
            {
                Vector2 size = capsule.size + Vector2.one * padding * 2;
                Vector3 capsuleCenter = worldPos + (Vector3)capsule.offset;

                // Treat as box for simplicity
                Vector3Int minTile = Vector3Int.FloorToInt(capsuleCenter - (Vector3)size / 2);
                Vector3Int maxTile = Vector3Int.FloorToInt(capsuleCenter + (Vector3)size / 2);

                for (int x = minTile.x; x <= maxTile.x; x++)
                {
                    for (int y = minTile.y; y <= maxTile.y; y++)
                    {
                        combinedPositions.Add(new Vector3Int(x, y, 0));
                    }
                }
            }
            else if (collider is PolygonCollider2D poly)
            {
                // Get the bounds of the polygon
                Bounds polyBounds = poly.bounds;
                Vector3 polyCenter = worldPos + (Vector3)poly.offset;
                Vector3 paddedExtents = polyBounds.extents + Vector3.one * padding;

                Vector3Int minTile = Vector3Int.FloorToInt(polyCenter - paddedExtents);
                Vector3Int maxTile = Vector3Int.FloorToInt(polyCenter + paddedExtents);

                for (int x = minTile.x; x <= maxTile.x; x++)
                {
                    for (int y = minTile.y; y <= maxTile.y; y++)
                    {
                        combinedPositions.Add(new Vector3Int(x, y, 0));
                    }
                }
            }
            else
            {
                // Fallback: use center position with padding
                Vector3Int centerTile = Vector3Int.FloorToInt(center);
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        combinedPositions.Add(new Vector3Int(centerTile.x + x, centerTile.y + y, 0));
                    }
                }
            }
        }

        // Convert HashSet to List
        positions.AddRange(combinedPositions);
        return positions;
    }

    private void PlaceEnemies(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent,
        HashSet<Vector3Int> occupiedPositions)
    {
        if (theme.commonEnemies == null || theme.commonEnemies.Length == 0)
        {
            Debug.LogWarning("No common enemies defined in theme");
            return;
        }

        int count = rng.Next(minEnemies, maxEnemies + 1);
        int attempts = 0;
        int maxAttempts = count * 3;

        for (int i = 0; i < count && attempts < maxAttempts; attempts++)
        {
            Vector3 pos = GetRandomRoomPosition(roomData, rng, enemyPlacementMargin);
            Vector3Int gridPos = Vector3Int.FloorToInt(pos);

            if (occupiedPositions.Contains(gridPos))
                continue;

            GameObject prefab = theme.commonEnemies[rng.Next(theme.commonEnemies.Length)];

            if (prefab == null)
            {
                Debug.LogWarning("Null enemy prefab in theme!");
                continue;
            }

            GameObject enemy = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            enemy.name = $"Enemy_{roomData.index}_{i}";

            occupiedPositions.Add(gridPos);
            i++;
        }
    }

    private Vector3 GetRandomRoomPosition(RoomData roomData, System.Random rng, float margin)
    {
        float minX = roomData.rect.xMin + margin;
        float maxX = roomData.rect.xMax - margin;
        float minY = roomData.rect.yMin + margin;
        float maxY = roomData.rect.yMax - margin;

        if (maxX <= minX) maxX = minX + 1;
        if (maxY <= minY) maxY = minY + 1;

        float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
        float y = Mathf.Lerp(minY, maxY, (float)rng.NextDouble());

        return new Vector3(x + 0.5f, y + 0.5f, 0);
    }
}