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
        Transform objectsParent,
        Transform enemiesParent)
    {
        Debug.Log($"Populating Combat Room: Room {roomData.index}");

        HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();

        PlaceFloorTiles(roomData, theme, rng, floorTilemap);
        PlaceBlockingObstacles(roomData, theme, rng, objectsParent, occupiedPositions);
        PlaceEnemies(roomData, theme, rng, enemiesParent, occupiedPositions);
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
            Vector3Int gridPos = Vector3Int.FloorToInt(pos);

            if (occupiedPositions.Contains(gridPos))
                continue;

            GameObject prefab = theme.blockingObstacles[rng.Next(theme.blockingObstacles.Length)];

            if (prefab == null)
            {
                Debug.LogWarning("Null obstacle prefab in theme!");
                continue;
            }

            GameObject obstacle = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            obstacle.name = $"Obstacle_{roomData.index}_{i}";

            occupiedPositions.Add(gridPos);
            i++;
        }
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