using UnityEngine;

/// <summary>
/// Defines the different types of rooms that can be generated
/// </summary>
public enum RoomArchetype
{
    CombatRoom,    // Standard room with enemies and obstacles
    BossArena,     // Large open room for boss fight: no blocking obstacles for Bruno ;)

    // Ideas for later:
    // NarrativeSpace,      // Story/NPC room
    // EnvironmentalHazard, // Rooms with special hazards
    // RestArea,            // Safe room with no enemies
}
