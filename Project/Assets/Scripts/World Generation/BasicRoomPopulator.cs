using UnityEngine;
using UnityEngine.Tilemaps;

public class BasicRoomPopulator : MonoBehaviour, IRoomPopulator
{
    [Header("Object Spawning (per room)")]
    [SerializeField] private GameObject[] objectPrefabs;
    [SerializeField] private int minObjects = 3;
    [SerializeField] private int maxObjects = 8;

    [Header("Enemy Spawning (per room)")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private int minEnemies = 2;
    [SerializeField] private int maxEnemies = 5;

    [Header("Placement")]
    [SerializeField] private float margin = 1.0f; // keep spawns away from walls

    public void Populate(RoomData room, System.Random rng,
                         Transform objectsParent, Transform enemiesParent, Tilemap floorTilemap)
    {
        // Objects
        if (objectPrefabs != null && objectPrefabs.Length > 0 && maxObjects > 0)
        {
            int count = rng.Next(minObjects, maxObjects + 1);
            for (int i = 0; i < count; i++)
            {
                var prefab = objectPrefabs[rng.Next(objectPrefabs.Length)];
                if (!prefab) continue;

                var pos = RandomPoint(room.rect, rng, margin);
                Instantiate(prefab, pos, Quaternion.identity, objectsParent);
            }
        }

        // Enemies
        if (enemyPrefabs != null && enemyPrefabs.Length > 0 && maxEnemies > 0)
        {
            int count = rng.Next(minEnemies, maxEnemies + 1);
            for (int i = 0; i < count; i++)
            {
                var prefab = enemyPrefabs[rng.Next(enemyPrefabs.Length)];
                if (!prefab) continue;

                var pos = RandomPoint(room.rect, rng, margin);
                Instantiate(prefab, pos, Quaternion.identity, enemiesParent);
            }
        }
    }

    private static Vector3 RandomPoint(RectInt rect, System.Random rng, float m)
    {
        float minX = rect.xMin + m, maxX = rect.xMax - 1 - m;
        float minY = rect.yMin + m, maxY = rect.yMax - 1 - m;
        float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
        float y = Mathf.Lerp(minY, maxY, (float)rng.NextDouble());
        return new Vector3(x + 0.5f, y + 0.5f, 0);
    }
}
