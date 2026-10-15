/// <summary>
/// ABILITY TEXT (small lookup tables for the Upgrade screen).
/// Your LoadoutRules already has labels (ItemLabel) but no descriptions, so descriptions live here.
/// Nothing in this file is gameplay; it is only display text.
/// </summary>
public readonly struct AiAbilityInfo
{
    public string Id { get; }
    public string Label { get; }
    public string Description { get; }
    public AiAbilityInfo(string id, string label, string description) { Id = id; Label = label; Description = description; }
}

/// <summary>
/// AI ABILITY CATALOG. Intentionally EMPTY for now (you said no upgrades yet).
/// To add one later: add a line such as
///     new AiAbilityInfo("ai_last_stand", "Last Stand", "The AI saves you once per attempt."),
/// The id is saved in save.json, so never rename it after players have saves.
/// </summary>
public static class AiAbilityCatalog
{
    public static readonly AiAbilityInfo[] All = new AiAbilityInfo[0];

    public static bool TryFind(string id, out AiAbilityInfo info)
    {
        foreach (var a in All) if (a.Id == id) { info = a; return true; }
        info = default;
        return false;
    }
}

/// <summary>BODY ABILITY DESCRIPTIONS keyed by LoadoutRules item id. Fill in as you like; empty text is fine.</summary>
public static class BodyAbilityText
{
    public static string Description(string itemId) => itemId switch
    {
        LoadoutRules.ArmFireRate => "Faster rate of fire.",
        LoadoutRules.ArmBlade => "A blade built into the arm.",
        LoadoutRules.DetachedDroneArm => "An arm that detaches and fights as a drone.",
        LoadoutRules.LegDoubleJump => "Jump again in mid-air.",
        LoadoutRules.LegHover => "Hover in the air.",
        LoadoutRules.LegAirDash => "Dash while airborne.",
        LoadoutRules.TorsoSlow => "Slow time.",
        LoadoutRules.BrainAI => "Targeting AI assists your aim.",
        _ => ""
    };
}