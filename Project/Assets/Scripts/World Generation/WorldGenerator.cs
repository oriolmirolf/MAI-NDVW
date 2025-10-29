using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldGenerator : Singleton<WorldGenerator> {
    [SerializeField] private BiomeConfig[] biomes;
    [SerializeField] private int seed = 12345;
    [SerializeField] private float biomeScale = 0.02f;
    [SerializeField] private float terrainScale = 0.1f;

    private System.Random random;

    protected override void Awake() {
        base.Awake();
        random = new System.Random(seed);
    }

    public void GenerateChunk(Chunk chunk, int chunkSize) {
        BiomeConfig biome = DetermineBiome(chunk.ChunkX, chunk.ChunkY);
        GenerateTerrain(chunk, biome, chunkSize);
        SpawnEntities(chunk, biome, chunkSize);
    }

    private BiomeConfig DetermineBiome(int chunkX, int chunkY) {
        if (biomes.Length == 0) return null;

        float moisture = Mathf.PerlinNoise(chunkX * biomeScale + seed, chunkY * biomeScale + seed);
        float temperature = Mathf.PerlinNoise(chunkX * biomeScale + seed + 1000, chunkY * biomeScale + seed + 1000);

        if (biomes.Length == 1) return biomes[0];

        if (biomes.Length >= 3) {
            if (moisture > 0.6f && temperature > 0.5f) return biomes[2];
            if (moisture < 0.4f) return biomes[1];
        }

        return biomes[0];
    }

    private void GenerateTerrain(Chunk chunk, BiomeConfig biome, int chunkSize) {
        if (biome == null) return;

        Tilemap groundTilemap = chunk.GetGroundTilemap();
        Tilemap canopyTilemap = chunk.GetCanopyTilemap();

        for (int x = 0; x < chunkSize; x++) {
            for (int y = 0; y < chunkSize; y++) {
                Vector3Int tilePos = new Vector3Int(x, y, 0);

                float worldX = chunk.ChunkX * chunkSize + x;
                float worldY = chunk.ChunkY * chunkSize + y;
                float noise = Mathf.PerlinNoise(worldX * terrainScale + seed, worldY * terrainScale + seed);

                if (noise > 0.7f && biome.decorationTiles.Length > 0) {
                    int decorIndex = Mathf.FloorToInt(noise * biome.decorationTiles.Length) % biome.decorationTiles.Length;
                    groundTilemap.SetTile(tilePos, biome.decorationTiles[decorIndex]);
                } else {
                    groundTilemap.SetTile(tilePos, biome.groundTile);
                }

                if (biome.canopyTile != null) {
                    float canopyNoise = Mathf.PerlinNoise(worldX * 0.15f + seed + 500, worldY * 0.15f + seed + 500);
                    if (canopyNoise > (1f - biome.canopyDensity)) {
                        canopyTilemap.SetTile(tilePos, biome.canopyTile);
                    }
                }
            }
        }
    }

    private void SpawnEntities(Chunk chunk, BiomeConfig biome, int chunkSize) {
        if (biome == null) return;

        Vector2 chunkWorldPos = new Vector2(chunk.ChunkX * chunkSize, chunk.ChunkY * chunkSize);

        if (biome.enemySpawns.Length > 0) {
            int enemyCount = Random.Range(
                Mathf.FloorToInt(biome.enemySpawns.Length * biome.enemyDensity * 3),
                Mathf.CeilToInt(biome.enemySpawns.Length * biome.enemyDensity * 6) + 1
            );

            for (int i = 0; i < enemyCount; i++) {
                SpawnEntry entry = GetWeightedRandom(biome.enemySpawns);
                if (entry == null || entry.prefab == null) continue;

                Vector2 spawnPos = chunkWorldPos + new Vector2(
                    Random.Range(2f, chunkSize - 2f),
                    Random.Range(2f, chunkSize - 2f)
                );

                GameObject enemy = Instantiate(entry.prefab, spawnPos, Quaternion.identity);
                ChunkEntity ce = enemy.GetComponent<ChunkEntity>();
                if (ce == null) ce = enemy.AddComponent<ChunkEntity>();
                ce.ParentChunk = chunk;
                ce.OriginalPrefab = entry.prefab;
                chunk.AddEntity(enemy);
            }
        }

        if (biome.objectSpawns.Length > 0) {
            int objectCount = Random.Range(
                Mathf.FloorToInt(biome.objectSpawns.Length * biome.objectDensity * 4),
                Mathf.CeilToInt(biome.objectSpawns.Length * biome.objectDensity * 8) + 1
            );

            for (int i = 0; i < objectCount; i++) {
                SpawnEntry entry = GetWeightedRandom(biome.objectSpawns);
                if (entry == null || entry.prefab == null) continue;

                Vector2 centerPos = chunkWorldPos + new Vector2(
                    Random.Range(2f, chunkSize - 2f),
                    Random.Range(2f, chunkSize - 2f)
                );

                if (entry.spawnInGroups) {
                    int groupCount = Random.Range(entry.minGroupSize, entry.maxGroupSize + 1);
                    for (int g = 0; g < groupCount; g++) {
                        Vector2 offset = Random.insideUnitCircle * entry.groupSpread;
                        Vector2 spawnPos = centerPos + offset;

                        GameObject obj = Instantiate(entry.prefab, spawnPos, Quaternion.identity);
                        ChunkEntity ce = obj.GetComponent<ChunkEntity>();
                        if (ce == null) ce = obj.AddComponent<ChunkEntity>();
                        ce.ParentChunk = chunk;
                        ce.OriginalPrefab = entry.prefab;
                        chunk.AddEntity(obj);
                    }
                } else {
                    GameObject obj = Instantiate(entry.prefab, centerPos, Quaternion.identity);
                    ChunkEntity ce = obj.GetComponent<ChunkEntity>();
                    if (ce == null) ce = obj.AddComponent<ChunkEntity>();
                    ce.ParentChunk = chunk;
                    ce.OriginalPrefab = entry.prefab;
                    chunk.AddEntity(obj);
                }
            }
        }
    }

    private SpawnEntry GetWeightedRandom(SpawnEntry[] entries) {
        if (entries.Length == 0) return null;

        float totalWeight = 0f;
        foreach (var entry in entries) totalWeight += entry.spawnWeight;

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in entries) {
            cumulative += entry.spawnWeight;
            if (randomValue <= cumulative) return entry;
        }

        return entries[0];
    }
}
