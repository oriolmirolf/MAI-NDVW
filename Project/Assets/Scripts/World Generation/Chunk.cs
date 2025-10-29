using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum ChunkState { Unloaded, Generating, Active, Unloading }

public class Chunk : MonoBehaviour {
    public int ChunkX { get; private set; }
    public int ChunkY { get; private set; }
    public ChunkState State { get; set; }

    private List<GameObject> entities = new List<GameObject>();
    private Tilemap groundTilemap;
    private Tilemap objectTilemap;
    private Tilemap canopyTilemap;

    public void Initialize(int x, int y) {
        ChunkX = x;
        ChunkY = y;
        State = ChunkState.Unloaded;

        Transform ground = transform.Find("Ground");
        if (ground == null) {
            GameObject groundObj = new GameObject("Ground");
            groundObj.transform.parent = transform;
            groundObj.transform.localPosition = Vector3.zero;

            groundObj.AddComponent<Grid>();

            Tilemap tilemap = groundObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = groundObj.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -2;

            ground = groundObj.transform;
        }
        groundTilemap = ground.GetComponent<Tilemap>();

        Transform objects = transform.Find("Objects");
        if (objects == null) {
            GameObject objsObj = new GameObject("Objects");
            objsObj.transform.parent = transform;
            objsObj.transform.localPosition = Vector3.zero;

            Tilemap tilemap = objsObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = objsObj.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -1;

            objects = objsObj.transform;
        }
        objectTilemap = objects.GetComponent<Tilemap>();

        Transform canopy = transform.Find("Canopy");
        if (canopy == null) {
            GameObject canopyObj = new GameObject("Canopy");
            canopyObj.transform.parent = transform;
            canopyObj.transform.localPosition = Vector3.zero;

            Tilemap tilemap = canopyObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = canopyObj.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 5;

            canopy = canopyObj.transform;
        }
        canopyTilemap = canopy.GetComponent<Tilemap>();
    }

    public void AddEntity(GameObject entity) {
        entities.Add(entity);
    }

    public void ClearEntities() {
        foreach (var entity in entities) {
            if (entity != null) {
                ChunkEntity ce = entity.GetComponent<ChunkEntity>();
                if (ce != null) ce.ReturnToPool();
                else Destroy(entity);
            }
        }
        entities.Clear();
    }

    public Tilemap GetGroundTilemap() => groundTilemap;
    public Tilemap GetObjectTilemap() => objectTilemap;
    public Tilemap GetCanopyTilemap() => canopyTilemap;
}
