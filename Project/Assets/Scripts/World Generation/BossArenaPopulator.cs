using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Populates boss arena rooms - large open spaces with only boss and decorations
/// </summary>
[System.Serializable]
public class BossArenaPopulator : IArchetypePopulator
{
    [Header("Boss Settings")]
    [SerializeField] private float bossCenterOffset = 0f;

    [Header("Decoration Settings")]
    [SerializeField] private bool useHeavyDecoration = true;
    [SerializeField] private float decorationMultiplier = 1.5f;

    public void PopulateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Tilemap pathTilemap,
        Transform objectsParent,
        Transform enemiesParent)
    {
        Debug.Log($"Populating Boss Arena: Room {roomData.index}");

        HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();

        // 1. Floor tiles (base layer)
        PlaceFloorTiles(roomData, theme, rng, floorTilemap);

        // 2. Paths (terrain, non-blocking) - seeded near portals
        PathGenerator.GeneratePaths(roomData, theme, rng, floorTilemap, pathTilemap, occupiedPositions);

        // 3. Boss placement
        PlaceBoss(roomData, theme, rng, enemiesParent, occupiedPositions);

        // 4. Decorations with optional multiplier
        if (useHeavyDecoration)
        {
            float originalDensity = theme.decorationDensity;
            theme.decorationDensity *= decorationMultiplier;

            CADecorator.DecorateRoom(roomData, theme, rng, objectsParent, occupiedPositions);

            theme.decorationDensity = originalDensity;
        }
        else
        {
            CADecorator.DecorateRoom(roomData, theme, rng, objectsParent, occupiedPositions);
        }

        // 5. Particle effects
        PlaceParticleEffects(roomData, theme, rng, objectsParent);

        Debug.Log($"Boss Arena {roomData.index} complete");
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
                    if (rng.NextDouble() < 0.3f)
                    {
                        tile = theme.floorVariations[rng.Next(theme.floorVariations.Length)];
                    }
                }

                tilemap.SetTile(pos, tile);
            }
        }
    }

    private void PlaceBoss(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent,
        HashSet<Vector3Int> occupiedPositions)
    {
        if (theme?.bossPrefab == null)
            return;

        Vector3 centerPos = new Vector3(
            roomData.center.x + 0.5f + bossCenterOffset,
            roomData.center.y + 0.5f + bossCenterOffset,
            0
        );

        GameObject boss = Object.Instantiate(theme.bossPrefab, centerPos, Quaternion.identity, parent);
        boss.name = $"Boss_{roomData.index}";

        // Configure AgentInferenceSetup with room bounds
        var agentSetup = boss.GetComponent<AgentInferenceSetup>();
        if (agentSetup != null)
        {
            // Calculate arena bounds from room rect (with small padding)
            Vector2 arenaMin = new Vector2(roomData.rect.xMin + 1f, roomData.rect.yMin + 1f);
            Vector2 arenaMax = new Vector2(roomData.rect.xMax - 1f, roomData.rect.yMax - 1f);
            agentSetup.SetArenaBounds(arenaMin, arenaMax);
            agentSetup.SetWaitForPlayer(true, roomData.rect);
            agentSetup.SetIsBoss(true); // Mark as boss to spawn exit portal on death
            Debug.Log($"Boss arena bounds set: {arenaMin} to {arenaMax}");
        }

        Vector3Int gridPos = Vector3Int.FloorToInt(centerPos);
        occupiedPositions.Add(gridPos);

        Debug.Log($"Boss placed at {centerPos}");
    }

    private void PlaceParticleEffects(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Transform parent)
    {
        if (theme.particleEffects == null || theme.particleEffects.Length == 0)
            return;

        int particleCount = rng.Next(4, 9);

        for (int i = 0; i < particleCount; i++)
        {
            float edge = (float)rng.NextDouble();
            Vector3 pos;

            if (edge < 0.25f)
                pos = new Vector3(
                    Mathf.Lerp(roomData.rect.xMin, roomData.rect.xMax, (float)rng.NextDouble()),
                    roomData.rect.yMax - 1,
                    0);
            else if (edge < 0.5f)
                pos = new Vector3(
                    Mathf.Lerp(roomData.rect.xMin, roomData.rect.xMax, (float)rng.NextDouble()),
                    roomData.rect.yMin + 1,
                    0);
            else if (edge < 0.75f)
                pos = new Vector3(
                    roomData.rect.xMin + 1,
                    Mathf.Lerp(roomData.rect.yMin, roomData.rect.yMax, (float)rng.NextDouble()),
                    0);
            else
                pos = new Vector3(
                    roomData.rect.xMax - 1,
                    Mathf.Lerp(roomData.rect.yMin, roomData.rect.yMax, (float)rng.NextDouble()),
                    0);

            GameObject prefab = theme.particleEffects[rng.Next(theme.particleEffects.Length)];
            GameObject particle = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            particle.name = $"ParticleEffect_{i}";
        }
    }
}