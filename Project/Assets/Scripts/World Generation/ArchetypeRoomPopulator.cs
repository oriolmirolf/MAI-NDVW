using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Main room populator that selects and applies archetypes
/// </summary>
public class ArchetypeRoomPopulator : MonoBehaviour, IRoomPopulator
{
    [Header("Chapter Theme")]
    [Tooltip("Current chapter theme")]
    [SerializeField] private ChapterTheme currentTheme;

    [Header("Archetype Populators")]
    [SerializeField] private CombatRoomPopulator combatRoomPopulator = new CombatRoomPopulator();
    [SerializeField] private BossArenaPopulator bossArenaPopulator = new BossArenaPopulator();
    [SerializeField] private EnvironmentalHazardPopulator hazardPopulator = new EnvironmentalHazardPopulator();

    [Header("Archetype Assignment")]
    [Tooltip("Which room index is the boss room? (0-based, -1 = none)")]
    [SerializeField] private int bossRoomIndex = -1;

    [Tooltip("Mark specific room indices as boss arenas (comma separated)")]
    [SerializeField] private string additionalBossRooms = "";

    [Tooltip("Mark specific room indices as hazard rooms with lakes (comma separated, e.g. '1,3,5')")]
    [SerializeField] private string hazardRoomIndices = "";

    private void Awake()
    {
        if (currentTheme == null)
        {
            Debug.LogError("ArchetypeRoomPopulator: No ChapterTheme assigned!");
        }

        if (combatRoomPopulator == null)
            combatRoomPopulator = new CombatRoomPopulator();

        if (bossArenaPopulator == null)
            bossArenaPopulator = new BossArenaPopulator();

        if (hazardPopulator == null)
            hazardPopulator = new EnvironmentalHazardPopulator();
    }

    public void Populate(
        RoomData room,
        System.Random rng,
        Transform objectsParent,
        Transform enemiesParent,
        Tilemap floorTilemap,
        Tilemap pathTilemap)
    {
        if (currentTheme == null)
        {
            Debug.LogError($"Cannot populate room {room.index}: No ChapterTheme assigned!");
            return;
        }

        Debug.Log($"=== Populating Room {room.index} ===");

        RoomArchetype archetype = SelectArchetype(room, rng);
        Debug.Log($"Room {room.index} archetype: {archetype}");

        IArchetypePopulator populator = GetPopulator(archetype);

        if (populator != null)
        {
            populator.PopulateRoom(room, currentTheme, rng, floorTilemap, pathTilemap, objectsParent, enemiesParent);
            Debug.Log($"Room {room.index} populated successfully");
        }
        else
        {
            Debug.LogWarning($"No populator found for archetype: {archetype}");
        }
    }

    private RoomArchetype SelectArchetype(RoomData room, System.Random rng)
    {
        if (IsBossRoom(room.index))
            return RoomArchetype.BossArena;
        if (IsHazardRoom(room.index))
            return RoomArchetype.EnvironmentalHazard;
        return RoomArchetype.CombatRoom;
    }

    private bool IsBossRoom(int roomIndex)
    {
        if (bossRoomIndex >= 0 && roomIndex == bossRoomIndex)
            return true;

        if (!string.IsNullOrEmpty(additionalBossRooms))
        {
            string[] indices = additionalBossRooms.Split(',');
            foreach (string indexStr in indices)
            {
                if (int.TryParse(indexStr.Trim(), out int index))
                {
                    if (index == roomIndex)
                        return true;
                }
            }
        }

        return false;
    }

    private bool IsHazardRoom(int roomIndex)
    {
        if (!string.IsNullOrEmpty(hazardRoomIndices))
        {
            string[] indices = hazardRoomIndices.Split(',');
            foreach (string indexStr in indices)
            {
                if (int.TryParse(indexStr.Trim(), out int index))
                {
                    if (index == roomIndex)
                        return true;
                }
            }
        }
        return false;
    }

    private IArchetypePopulator GetPopulator(RoomArchetype archetype)
    {
        switch (archetype)
        {
            case RoomArchetype.CombatRoom:
                return combatRoomPopulator;

            case RoomArchetype.BossArena:
                return bossArenaPopulator;

            case RoomArchetype.EnvironmentalHazard:
                return hazardPopulator;

            default:
                Debug.LogWarning($"No populator for archetype {archetype}");
                return combatRoomPopulator;
        }
    }

    public void SetTheme(ChapterTheme newTheme)
    {
        currentTheme = newTheme;
        Debug.Log($"Theme changed to: {newTheme.name}");
    }

    public void SetBossRoomIndex(int index)
    {
        bossRoomIndex = index;
        Debug.Log($"Boss room set to index: {index}");
    }
}