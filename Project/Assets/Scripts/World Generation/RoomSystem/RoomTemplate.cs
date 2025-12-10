using UnityEngine;

[CreateAssetMenu(fileName = "RoomTemplate", menuName = "Dungeon/Room Template")]
public class RoomTemplate : ScriptableObject
{
    [Header("Room Dimensions")]
    [Tooltip("Size in tiles (e.g., 32x24)")]
    public Vector2Int size = new Vector2Int(32, 24);

    [Header("Room Prefab")]
    [Tooltip("The prefab containing tilemap, colliders, and RoomInstance component")]
    public GameObject prefab;

    [Header("Door Configuration")]
    [Tooltip("Which sides have portal openings")]
    public DoorMask availableDoors = DoorMask.All;

    [Header("Room Properties")]
    public RoomArchetype archetype = RoomArchetype.CombatRoom;

    [Tooltip("Difficulty rating for room selection (higher = harder)")]
    [Range(1, 10)]
    public int difficulty = 1;

    [Tooltip("Weight for random selection (higher = more likely)")]
    [Range(1, 10)]
    public int selectionWeight = 5;

    [Header("Population Settings")]
    [Tooltip("If true, uses procedural population. If false, room is pre-populated in prefab")]
    public bool useProceduralPopulation = true;

    [Tooltip("Min enemies to spawn (if procedural)")]
    public int minEnemies = 2;

    [Tooltip("Max enemies to spawn (if procedural)")]
    public int maxEnemies = 5;

    public bool MatchesDoorRequirements(DoorMask required)
    {
        return (availableDoors & required) == required;
    }
}
