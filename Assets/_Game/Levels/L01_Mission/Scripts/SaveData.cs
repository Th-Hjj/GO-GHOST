using System;
using System.Collections.Generic;

/// <summary>One permanent body commitment: which body slot (LoadoutSlot ordinal) and which catalog item.</summary>
[Serializable]
public class BodyCommit
{
    public int slot;
    public string itemId;
}

/// <summary>
/// SAVE DATA (plain data). Everything written to save.json.
///  - wave:         which boss wave Continue resumes at (1-3).
///  - runNumber:    counts attempts; used as the officer index when deploying.
///  - aiLevel:      0 = none chosen, max 3. Only ever goes up.
///  - aiAbilityIds: committed AI abilities (permanent).
///  - bodyCommits:  committed body abilities as slot + itemId (permanent).
/// Open slots are never saved; they are calculated from these (see AssistRules).
/// </summary>
[Serializable]
public class SaveData
{
    public int wave = 1;
    public int runNumber = 1;
    public int aiLevel = 0;
    public List<string> aiAbilityIds = new List<string>();
    public List<BodyCommit> bodyCommits = new List<BodyCommit>();
}
