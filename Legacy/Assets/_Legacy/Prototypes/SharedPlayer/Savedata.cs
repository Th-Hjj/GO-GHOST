using System;
using System.Collections.Generic;

/// <summary>
/// One permanent body commitment: WHICH body slot, and WHICH item from LoadoutRules.PrototypeCatalog.
/// Slot is stored as an int (the LoadoutSlot ordinal) so JsonUtility serializes it safely.
/// </summary>
[Serializable]
public class BodyCommit
{
    public int slot;        // (int)LoadoutSlot
    public string itemId;   // e.g. LoadoutRules.LegHover
}

/// <summary>
/// SAVE DATA (plain data, no logic). Everything written to save.json.
/// Only the LEVEL and the COMMITMENTS are stored. Open slots are always calculated (see AssistRules).
///  - aiLevel:       0 = none chosen yet, max 3. Only ever goes up.
///  - aiAbilityIds:  committed AI abilities (permanent, see AiAbilityCatalog).
///  - bodyCommits:   committed body abilities as slot + itemId pairs (permanent).
/// </summary>
[Serializable]
public class SaveData
{
    public int runNumber = 1;
    public int aiLevel = 0;
    public List<string> aiAbilityIds = new List<string>();
    public List<BodyCommit> bodyCommits = new List<BodyCommit>();
}