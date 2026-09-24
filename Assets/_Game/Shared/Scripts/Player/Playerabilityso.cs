using UnityEngine;

/// <summary>
/// Base class for special abilities. To add a new ability: create a
/// subclass with your effect in Activate(), then Assets > Create > Player >
/// Abilities > [Your Ability] to make an asset from it, and hand that asset
/// to PlayerAbilitySystem.UnlockAbility() when the player levels up. This
/// keeps the growing ability list data-driven instead of one giant switch
/// statement.
/// </summary>
// FUNCTION SUMMARY
// Activate() — runs this ability's effect on the player; every ability subclass implements its own version.
public abstract class PlayerAbilitySO : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description;
    public Sprite icon;
    public float cooldown = 5f;

    [Header("Uses")]
    [Tooltip("0 = unlimited, governed only by cooldown. 1+ = limited charges (e.g. 1 = once per battle); call PlayerAbilitySystem.ResetAbilityUses() to recharge, typically at the start of a new battle.")]
    public int maxUses = 0;

    /// <summary>Runs the ability's effect. Return true if it successfully activated (starts its cooldown and consumes a use).</summary>
    public abstract bool Activate(GameObject player);
}