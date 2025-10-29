using UnityEngine;

public struct WorldCoordinate {
    public int chunkX;
    public int chunkY;
    public Vector2 localPosition;

    public WorldCoordinate(int x, int y, Vector2 local) {
        chunkX = x;
        chunkY = y;
        localPosition = local;
    }

    public Vector2 ToWorldPosition(int chunkSize) {
        return new Vector2(chunkX * chunkSize, chunkY * chunkSize) + localPosition;
    }

    public static WorldCoordinate FromWorldPosition(Vector2 worldPos, int chunkSize) {
        int cx = Mathf.FloorToInt(worldPos.x / chunkSize);
        int cy = Mathf.FloorToInt(worldPos.y / chunkSize);
        Vector2 local = worldPos - new Vector2(cx * chunkSize, cy * chunkSize);
        return new WorldCoordinate(cx, cy, local);
    }
}
