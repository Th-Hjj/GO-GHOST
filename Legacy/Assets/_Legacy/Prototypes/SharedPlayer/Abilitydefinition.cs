using UnityEngine;

public enum AbilityKind { AI, Body }

/// <summary>
/// ABILITY DEFINITION (ScriptableObject template).
/// One asset per ability: Right-click in Project -> Create -> Game -> Ability.
/// No abilities exist yet; this is only the shape of the data.
///  - id:        unique and PERMANENT. Saves refer to it, so never rename it after players have saves.
///  - kind:      AI ability or Body ability.
///  - slot:      which body part it belongs to (uses YOUR existing LoadoutSlot enum). Ignored for AI abilities.
///  - Brain = head abilities, Torso = core abilities, arms/legs = limb abilities.
///  - Don't use LoadoutSlot.Weapon for body abilities (the blueprint has no weapon region).
/// </summary>
[CreateAssetMenu(menuName = "Game/Ability")]
public class AbilityDefinition : ScriptableObject
{
    public string id;
    public AbilityKind kind;
    public LoadoutSlot slot;
    public string displayName;
    [TextArea] public string description;
}