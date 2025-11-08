using UnityEngine;
using UnityEngine.Tilemaps;

public interface IRoomPopulator
{
    // Called once for each room after tiles are carved.
    void Populate(RoomData room, System.Random rng, Transform objectsParent, Transform enemiesParent, Tilemap floorTilemap);
}

[System.Serializable]
public struct RoomData
{
    public int index;
    public RectInt rect;
    public Vector3Int center;
}
