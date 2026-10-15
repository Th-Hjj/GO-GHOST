/// <summary>
/// LOADOUT BRIDGE (connects the save file to your existing LoadoutDraft pipeline).
/// It does not duplicate any of your rules; it replays the saved commitments through
/// LoadoutDraft.TrySetSlot so every rule you already wrote (one Hover, one Double Jump,
/// slot occupancy, Drone Arm unlock) is applied automatically.
///
///  BuildDraft():    new LoadoutDraft filled from SaveData.bodyCommits.
///  CanCommit():     true if a new commitment would be legal AND the slot is currently empty
///                   (commitments are permanent, so a filled slot can never be changed).
///  BuildSnapshot(): locks the draft into a DeploymentSnapshot for Battle (officer index = run number).
///
/// NOTE: LoadoutDraft.Deploy and LoadoutRules.PrototypeCatalog are 'internal', so this file must be
/// in the same assembly as LoadoutState.cs (true unless you use assembly definitions).
/// </summary>
public static class LoadoutBridge
{
    public static LoadoutDraft BuildDraft(SaveData data)
    {
        var draft = new LoadoutDraft(augmentationsAvailable: true);
        foreach (var c in data.bodyCommits)
            draft.TrySetSlot((LoadoutSlot)c.slot, c.itemId);   // replays rules; an invalid old entry is simply skipped
        return draft;
    }

    public static bool CanCommit(SaveData data, LoadoutSlot slot, string itemId)
    {
        if (slot == LoadoutSlot.Weapon || string.IsNullOrEmpty(itemId)) return false;
        var draft = BuildDraft(data);
        if (!string.IsNullOrEmpty(draft.GetSlot(slot).ItemId)) return false;   // permanent: never replace
        return draft.TrySetSlot(slot, itemId);                                 // your own rules decide the rest
    }

    public static DeploymentSnapshot BuildSnapshot(SaveData data)
    {
        int officerIndex = data.runNumber < 1 ? 1 : data.runNumber;
        return BuildDraft(data).Deploy(officerIndex);
    }
}
