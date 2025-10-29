using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class SpawnEntry {
    public GameObject prefab;
    public float spawnWeight = 1f;
    public int minCount = 1;
    public int maxCount = 3;
    public bool spawnInGroups = false;
    public int minGroupSize = 2;
    public int maxGroupSize = 4;
    public float groupSpread = 1.5f;
}

[CreateAssetMenu(menuName = "World Generation/Biome Config")]
public class BiomeConfig : ScriptableObject {
    public string biomeName;

    [Header("Tiles")]
    public TileBase groundTile;
    public TileBase[] decorationTiles;
    public TileBase canopyTile;
    [Range(0f, 1f)] public float canopyDensity = 0.3f;

    [Header("Enemy Spawns")]
    public SpawnEntry[] enemySpawns;

    [Header("Object Spawns")]
    public SpawnEntry[] objectSpawns;

    [Header("Spawn Parameters")]
    [Range(0f, 1f)] public float enemyDensity = 0.3f;
    [Range(0f, 1f)] public float objectDensity = 0.5f;
}
