using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Interface for populating a room based on its archetype
/// </summary>
public interface IArchetypePopulator
{
    /// <summary>
    /// Populate a room with tiles, objects, and enemies based on archetype and theme
    /// </summary>
    void PopulateRoom(
        RoomData roomData,
        ChapterTheme theme,
        System.Random rng,
        Tilemap floorTilemap,
        Transform objectsParent,
        Transform enemiesParent
    );
}