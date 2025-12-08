using UnityEngine;

/// <summary>
/// Defines the different types of rooms that can be generated
/// </summary>
public enum RoomArchetype
{
    CombatRoom,    // Standard room with enemies and obstacles
    BossArena,     // Large open room for boss fight: no blocking obstacles for Bruno ;)
    EnvironmentalHazard, // Room with environmental hazards like lakes

    // Ideas for later:
    // NarrativeSpace,      // Story/NPC room
    // RestArea,            // Safe room with no enemies
}
