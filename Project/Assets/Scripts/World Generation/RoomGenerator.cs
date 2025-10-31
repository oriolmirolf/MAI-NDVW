using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomGenerator : MonoBehaviour {
    [Header("Tilemaps")]
    [SerializeField] private Tilemap floorTilemap;

    [Header("Floor Tiles")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase[] groundTileVariations;

    [Header("Object Spawning")]
    [SerializeField] private GameObject[] objectPrefabs;
    [SerializeField] private int minObjects = 3;
    [SerializeField] private int maxObjects = 8;

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private int minEnemies = 2;
    [SerializeField] private int maxEnemies = 5;

    [Header("Room Settings")]
    [SerializeField] private int roomWidth = 40;
    [SerializeField] private int roomHeight = 40;
    [SerializeField] private int seed = 12345;

    private Transform objectParent;
    private Transform enemyParent;
    private System.Random random;

    private void Start() {
        objectParent = new GameObject("Objects").transform;
        enemyParent = new GameObject("Enemies").transform;
        random = new System.Random(seed);

        GenerateRoom();
        SpawnPlayer();
    }

    private void GenerateRoom() {
        int halfWidth = roomWidth / 2;
        int halfHeight = roomHeight / 2;

        for (int x = -halfWidth; x < halfWidth; x++) {
            for (int y = -halfHeight; y < halfHeight; y++) {
                Vector3Int tilePos = new Vector3Int(x, y, 0);

                TileBase tile = SelectGroundTile();
                floorTilemap?.SetTile(tilePos, tile);

                // Canopy disabled - will implement later with proper sprite ordering
                // if (random.NextDouble() < canopyDensity) {
                //     canopyTilemap?.SetTile(tilePos, canopyRuleTile);
                // }
            }
        }

        SpawnObjects(halfWidth, halfHeight);
        SpawnEnemies(halfWidth, halfHeight);
    }

    private TileBase SelectGroundTile() {
        if (groundTileVariations == null || groundTileVariations.Length == 0) {
            return groundTile;
        }

        if (random.NextDouble() < 0.3) {
            return groundTileVariations[random.Next(groundTileVariations.Length)];
        }

        return groundTile;
    }

    private void SpawnObjects(int halfWidth, int halfHeight) {
        if (objectPrefabs == null || objectPrefabs.Length == 0) return;

        int count = random.Next(minObjects, maxObjects + 1);

        for (int i = 0; i < count; i++) {
            GameObject prefab = objectPrefabs[random.Next(objectPrefabs.Length)];
            if (prefab == null) continue;

            float x = (float)(random.NextDouble() * (roomWidth - 10) - halfWidth + 5);
            float y = (float)(random.NextDouble() * (roomHeight - 10) - halfHeight + 5);

            GameObject obj = Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity);
            obj.transform.parent = objectParent;
        }
    }

    private void SpawnEnemies(int halfWidth, int halfHeight) {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int count = random.Next(minEnemies, maxEnemies + 1);

        for (int i = 0; i < count; i++) {
            GameObject prefab = enemyPrefabs[random.Next(enemyPrefabs.Length)];
            if (prefab == null) continue;

            float x = (float)(random.NextDouble() * (roomWidth - 10) - halfWidth + 5);
            float y = (float)(random.NextDouble() * (roomHeight - 10) - halfHeight + 5);

            GameObject enemy = Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity);
            enemy.transform.parent = enemyParent;
        }
    }

    private void SpawnPlayer() {
        if (PlayerController.Instance != null) {
            PlayerController.Instance.transform.position = Vector3.zero;
        }
    }
}
