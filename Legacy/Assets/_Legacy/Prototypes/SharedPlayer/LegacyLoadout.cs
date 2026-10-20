using System;

// Historical draft and Archive choices share the retained deployment contracts.
public static partial class LoadoutRules
{
    public static string NextOption(LoadoutSlot slot, string current)
    {
        if (IsLeg(slot)) return NextLegOption(current);
        if (slot == LoadoutSlot.LeftArm)
            return current == ArmFireRate ? CounterArm
                : current == CounterArm ? (PoliceArchive.Current.HasHiddenAugmentation(DetachedDroneArm) ? DetachedDroneArm : "")
                : current == DetachedDroneArm ? "" : ArmFireRate;
        if (slot == LoadoutSlot.Brain)
            return current == BrainAI ? PredictiveCortex : current == PredictiveCortex ? "" : BrainAI;
        return string.IsNullOrEmpty(current) ? ModuleForSlot(slot) : "";
    }
}

/// <summary>Only an open draft can be edited. Sealing also rejects writes through stale UI references.</summary>
public sealed class LoadoutDraft
{
    private readonly LoadoutEntry[] slots = new LoadoutEntry[LoadoutRules.SlotCount];
    private readonly AugmentationDefinition[] catalog;
    public bool IsLocked { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Only developer drafts may test existing locked items, without writing an Archive unlock.
    internal bool DevelopmentAllowLockedItems { get; set; }
#endif
    public bool AugmentationsAvailable { get; }
    public float BodyModificationPercent => LoadoutRules.BodyModification(slots);
    public string WeaponId => GetSlot(LoadoutSlot.Weapon).ItemId;
    public WeaponCompatibility WeaponCompatibility => LoadoutRules.WeaponPolicy(slots);
    public bool CanUseWeapon => WeaponCompatibility != WeaponCompatibility.Disabled;

    public LoadoutDraft(bool augmentationsAvailable = false) : this(augmentationsAvailable, LoadoutRules.PrototypeCatalog) { }

    // An isolated catalog allows data validation without registering fake items in the player-facing catalog.
    internal LoadoutDraft(bool augmentationsAvailable, AugmentationDefinition[] definitions)
    {
        catalog = (AugmentationDefinition[])(definitions ?? throw new ArgumentNullException(nameof(definitions))).Clone();
        for (int i = 0; i < catalog.Length; i++)
        {
            if (catalog[i] == null) throw new ArgumentException("Null augmentation definition.");
            for (int j = 0; j < i; j++)
                if (catalog[i].ItemId == catalog[j].ItemId)
                    foreach (var slot in catalog[i].OccupiedSlots)
                        if (catalog[j].Occupies(slot)) throw new ArgumentException("Ambiguous augmentation ID/slot.");
        }
        AugmentationsAvailable = augmentationsAvailable;
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new LoadoutEntry((LoadoutSlot)i, i == (int)LoadoutSlot.Weapon ? LoadoutRules.AssaultRifle : "", 0f);
    }

    public LoadoutDraft(DeploymentSnapshot previous, bool augmentationsAvailable)
        : this(augmentationsAvailable, previous != null ? previous.CopyCatalog() : LoadoutRules.PrototypeCatalog)
    {
        if (previous == null) return;
        for (int i = 0; i < slots.Length; i++) slots[i] = previous.GetSlot((LoadoutSlot)i);
    }

    public LoadoutEntry GetSlot(LoadoutSlot slot) => slots[LoadoutRules.Index(slot)];

    public bool TrySetSlot(LoadoutSlot slot, string itemId)
    {
        if (IsLocked || (int)slot < 0 || (int)slot >= slots.Length) return false;
        if (slot == LoadoutSlot.Weapon)
        {
            if (!CanUseWeapon || !LoadoutRules.IsWeapon(itemId)) return false;
            slots[(int)slot] = new LoadoutEntry(slot, itemId, 0f);
            return true;
        }
        if (string.IsNullOrEmpty(itemId))
        {
            ClearInstallation(slot);
            return true;
        }
        if (!AugmentationsAvailable) return false;
        // One Double Jump, one Hover and one Air Dash at most. The other leg is never changed on the player's behalf.
        if (LoadoutRules.IsLeg(slot) && (itemId == LoadoutRules.LegDoubleJump || itemId == LoadoutRules.LegHover || itemId == LoadoutRules.LegAirDash)
            && slots[(int)(slot == LoadoutSlot.LeftLeg ? LoadoutSlot.RightLeg : LoadoutSlot.LeftLeg)].ItemId == itemId) return false;
        if (itemId == LoadoutRules.DetachedDroneArm && !PoliceArchive.Current.HasHiddenAugmentation(itemId)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            && !DevelopmentAllowLockedItems
#endif
        ) return false;
        AugmentationDefinition definition = null;
        foreach (var candidate in catalog)
            if (candidate.ItemId == itemId && candidate.Occupies(slot)) { definition = candidate; break; }
        if (definition == null) return false;

        var replaced = slots[(int)slot];
        // Transactional: never evict a separate installation occupying another required slot.
        foreach (var occupied in definition.OccupiedSlots)
        {
            var existing = slots[(int)occupied];
            if (!string.IsNullOrEmpty(existing.ItemId) &&
                (existing.InstallationSlot != replaced.InstallationSlot || existing.ItemId != replaced.ItemId)) return false;
        }
        ClearInstallation(slot);
        foreach (var occupied in definition.OccupiedSlots)
            slots[(int)occupied] = new LoadoutEntry(occupied, itemId, LoadoutRules.MaximumContribution(occupied),
                definition.OccupiedSlots[0], definition.WeaponCompatibility);
        return true;
    }

    private void ClearInstallation(LoadoutSlot slot)
    {
        var removed = slots[(int)slot];
        for (int i = 0; i < (int)LoadoutSlot.Weapon; i++)
            if (slots[i].InstallationSlot == removed.InstallationSlot && slots[i].ItemId == removed.ItemId)
                slots[i] = new LoadoutEntry((LoadoutSlot)i, "", 0f);
    }

    internal void Lock() => IsLocked = true;

    internal DeploymentSnapshot Deploy(int officerIndex, int brainPermissionTier = -1)
    {
        if (IsLocked) throw new InvalidOperationException("This draft was already deployed.");
        Lock();
        return new DeploymentSnapshot(officerIndex, slots, catalog, brainPermissionTier);
    }
}

public sealed class VictoryRecord
{
    public DeploymentSnapshot WinningDeployment { get; }
    public int OfficerIndex => WinningDeployment.OfficerIndex;
    public float BodyModificationPercent => WinningDeployment.BodyModificationPercent;
    public string WeaponId => WinningDeployment.WeaponId;

    public VictoryRecord(DeploymentSnapshot deployment)
    {
        if (deployment == null) throw new ArgumentNullException(nameof(deployment));
        WinningDeployment = deployment.Copy();
    }
}
