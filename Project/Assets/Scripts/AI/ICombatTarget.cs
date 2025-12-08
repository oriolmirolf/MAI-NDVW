using UnityEngine;

/// <summary>
/// Interface for entities that can be targeted in combat by ML Agents.
/// Both PlayerController and AgentController implement this interface.
/// </summary>
public interface ICombatTarget
{
    /// <summary>
    /// The transform of the combat target (for position tracking)
    /// </summary>
    Transform Transform { get; }
    
    /// <summary>
    /// The Rigidbody2D of the combat target (for velocity tracking)
    /// </summary>
    Rigidbody2D Rigidbody { get; }
    
    /// <summary>
    /// Whether this target is facing left
    /// </summary>
    bool FacingLeft { get; }
    
    /// <summary>
    /// Whether this target is dead
    /// </summary>
    bool IsDead { get; }
    
    /// <summary>
    /// Get normalized health value [0, 1]
    /// </summary>
    float GetNormalizedHealth();
    
    /// <summary>
    /// Get normalized weapon index [0, 1] (for agents with multiple weapons)
    /// For player, this can return 0 or be based on current weapon
    /// </summary>
    float GetNormalizedWeaponIndex();
    
    /// <summary>
    /// Get normalized cooldown remaining [0, 1]
    /// 0 = ready to attack, 1 = just attacked
    /// </summary>
    float GetNormalizedCooldownRemaining();
    
    /// <summary>
    /// Get the current aim angle in degrees [-180, 180]
    /// For player, this is based on mouse position
    /// </summary>
    float CurrentAimAngle { get; }
}
