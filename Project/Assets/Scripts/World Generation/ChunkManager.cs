using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : Singleton<ChunkManager> {
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private int loadRadius = 2;
    [SerializeField] private int unloadRadius = 3;
    [SerializeField] private Transform chunkParent;

    private Dictionary<Vector2Int, Chunk> activeChunks = new Dictionary<Vector2Int, Chunk>();
    private Vector2Int currentPlayerChunk = Vector2Int.zero;

    private void Start() {
        if (chunkParent == null) {
            chunkParent = new GameObject("Chunks").transform;
        }

        if (PlayerController.Instance != null) {
            Vector2 playerPos = PlayerController.Instance.transform.position;
            currentPlayerChunk = GetChunkCoordinate(playerPos);
            LoadChunksAroundPlayer();
        }
    }

    private void Update() {
        UpdateChunks();
    }

    private void UpdateChunks() {
        if (PlayerController.Instance == null) return;

        Vector2 playerPos = PlayerController.Instance.transform.position;
        Vector2Int playerChunk = GetChunkCoordinate(playerPos);

        if (playerChunk != currentPlayerChunk) {
            currentPlayerChunk = playerChunk;
            LoadChunksAroundPlayer();
            UnloadDistantChunks();
        }
    }

    private Vector2Int GetChunkCoordinate(Vector2 worldPos) {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.y / chunkSize)
        );
    }

    private void LoadChunksAroundPlayer() {
        for (int x = -loadRadius; x <= loadRadius; x++) {
            for (int y = -loadRadius; y <= loadRadius; y++) {
                Vector2Int chunkCoord = currentPlayerChunk + new Vector2Int(x, y);
                if (!activeChunks.ContainsKey(chunkCoord)) {
                    LoadChunk(chunkCoord);
                }
            }
        }
    }

    private void LoadChunk(Vector2Int coord) {
        GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunkObj.transform.parent = chunkParent;
        chunkObj.transform.position = new Vector3(coord.x * chunkSize, coord.y * chunkSize, 0);

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.Initialize(coord.x, coord.y);

        FillTestTiles(chunk);

        chunk.State = ChunkState.Active;
        activeChunks[coord] = chunk;

        Debug.Log($"Loaded chunk at ({coord.x}, {coord.y})");
    }

    private void FillTestTiles(Chunk chunk) {
        if (WorldGenerator.Instance != null) {
            WorldGenerator.Instance.GenerateChunk(chunk, chunkSize);
        }
    }

    private void UnloadDistantChunks() {
        List<Vector2Int> toUnload = new List<Vector2Int>();

        foreach (var kvp in activeChunks) {
            Vector2Int chunkCoord = kvp.Key;
            float distance = Vector2Int.Distance(chunkCoord, currentPlayerChunk);
            if (distance > unloadRadius) {
                toUnload.Add(chunkCoord);
            }
        }

        foreach (var coord in toUnload) {
            UnloadChunk(coord);
        }
    }

    private void UnloadChunk(Vector2Int coord) {
        if (activeChunks.TryGetValue(coord, out Chunk chunk)) {
            chunk.ClearEntities();
            Destroy(chunk.gameObject);
            activeChunks.Remove(coord);

            Debug.Log($"Unloaded chunk at ({coord.x}, {coord.y})");
        }
    }
}
