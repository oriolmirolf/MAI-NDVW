using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;


public interface IRoomPopulator
{
    // Called once for each room after tiles are carved.
    void Populate(RoomData room, System.Random rng, Transform objectsParent, Transform enemiesParent, Tilemap floorTilemap, Tilemap pathTilemap);
}

[System.Serializable]
public struct RoomData
{
    public int index;
    public RectInt rect;
    public Vector3Int center;
    public IReadOnlyList<PortalInfo> portals;
}

public struct PortalInfo
{
    public int otherRoomIndex;   // index of the connected room
    public Vector3Int cell;      // tile cell where the portal sits
    public Vector3 worldPos;     // world position of portal center
    public WallSide wallSide;    // which wall of THIS room
}

public enum WallSide { North, South, East, West }