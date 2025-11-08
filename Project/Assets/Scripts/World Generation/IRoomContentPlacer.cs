using UnityEngine;
using UnityEngine.Tilemaps;

public interface IRoomContentPlacer {
    void Populate(RoomData room, System.Random rng, Tilemap floorTilemap, Transform parent);
}
