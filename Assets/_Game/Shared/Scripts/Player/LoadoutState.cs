using System;

public enum LoadoutSlot { LeftArm, RightArm, LeftLeg, RightLeg, Spine, Brain, Weapon }
public enum AimControl { Manual, AI }

/// <summary>Immutable slot value. TEST-v0 contributions belong to equipped items, never installation history.</summary>
public readonly struct LoadoutEntry
{
    public LoadoutSlot Slot { get; }
    public string ItemId { get; }
    public float BodyContributionPercent { get; }

    internal LoadoutEntry(LoadoutSlot slot, string itemId, float contribution)
    {
        Slot = slot;
        ItemId = itemId;
        BodyContributionPercent = slot == LoadoutSlot.Weapon ? 0f : contribution;
    }
}

public static class LoadoutRules
{
    public const int SlotCount = 7;
    public const string BasicGun = "basic_gun";
    public const string ArmFireRate = "test_v2_arm_rate";
    public const string LegMoveSpeed = "test_v2_leg_speed";
    public const string SpineSlow = "test_v2_spine_slow";
    public const string BrainAI = "ai_aim";

    // TEST-v2 retains the six independent capacities. All six slots now have one implemented option.
    public static float MaximumContribution(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm or LoadoutSlot.RightArm or LoadoutSlot.LeftLeg or LoadoutSlot.RightLeg => 10f,
        LoadoutSlot.Spine => 20f, LoadoutSlot.Brain => 40f, _ => 0f
    };

    public static string ModuleForSlot(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm or LoadoutSlot.RightArm => ArmFireRate,
        LoadoutSlot.LeftLeg or LoadoutSlot.RightLeg => LegMoveSpeed,
        LoadoutSlot.Spine => SpineSlow,
        LoadoutSlot.Brain => BrainAI, _ => null
    };

    public static string ItemLabel(string id) => id switch
    {
        ArmFireRate => "Rapid Fire", LegMoveSpeed => "Move Speed", SpineSlow => "Time Slow", BrainAI => "AI Aim",
        BasicGun => "Basic Gun", _ => "Human / Empty"
    };

    public static string WeaponLabel(string id) => id == BasicGun ? "Basic Gun" : id;
    public static string SlotLabel(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => "Left Arm", LoadoutSlot.RightArm => "Right Arm",
        LoadoutSlot.LeftLeg => "Left Leg", LoadoutSlot.RightLeg => "Right Leg",
        _ => slot.ToString()
    };

    // Fixed six body slots. Never normalize against the currently implemented catalog.
    internal static float BodyModification(LoadoutEntry[] slots)
    {
        float total = 0f;
        for (int i = 0; i < (int)LoadoutSlot.Weapon; i++) total += slots[i].BodyContributionPercent;
        return total;
    }

    internal static int Index(LoadoutSlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return index;
    }
}

/// <summary>Only an open draft can be edited. Sealing also rejects writes through stale UI references.</summary>
public sealed class LoadoutDraft
{
    private readonly LoadoutEntry[] slots = new LoadoutEntry[LoadoutRules.SlotCount];
    public bool IsLocked { get; private set; }
    public bool AugmentationsAvailable { get; }
    public float BodyModificationPercent => LoadoutRules.BodyModification(slots);
    public string WeaponId => GetSlot(LoadoutSlot.Weapon).ItemId;

    public LoadoutDraft(bool augmentationsAvailable = false)
    {
        AugmentationsAvailable = augmentationsAvailable;
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new LoadoutEntry((LoadoutSlot)i, i == (int)LoadoutSlot.Weapon ? LoadoutRules.BasicGun : "", 0f);
    }

    public LoadoutDraft(DeploymentSnapshot previous, bool augmentationsAvailable) : this(augmentationsAvailable)
    {
        if (previous == null) return;
        for (int i = 0; i < slots.Length; i++) slots[i] = previous.GetSlot((LoadoutSlot)i);
    }

    public LoadoutEntry GetSlot(LoadoutSlot slot) => slots[LoadoutRules.Index(slot)];

    public bool TrySetSlot(LoadoutSlot slot, string itemId)
    {
        if (IsLocked || (int)slot < 0 || (int)slot >= slots.Length) return false;
        float contribution = 0f;
        if (slot == LoadoutSlot.Weapon)
        {
            if (itemId != LoadoutRules.BasicGun) return false;
        }
        else if (!string.IsNullOrEmpty(itemId))
        {
            if (!AugmentationsAvailable || itemId != LoadoutRules.ModuleForSlot(slot)) return false;
            contribution = LoadoutRules.MaximumContribution(slot);
        }
        slots[(int)slot] = new LoadoutEntry(slot, itemId ?? "", contribution);
        return true;
    }

    internal void Lock() => IsLocked = true;

    internal DeploymentSnapshot Deploy(int officerIndex)
    {
        if (IsLocked) throw new InvalidOperationException("This draft was already deployed.");
        Lock();
        return new DeploymentSnapshot(officerIndex, slots);
    }
}

/// <summary>Private copy of value entries; callers can neither mutate nor retrieve its array.</summary>
public sealed class DeploymentSnapshot
{
    private readonly LoadoutEntry[] slots;
    public int OfficerIndex { get; }
    public float BodyModificationPercent { get; }
    public string WeaponId => GetSlot(LoadoutSlot.Weapon).ItemId;
    public AimControl Aim => GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.BrainAI ? AimControl.AI : AimControl.Manual;
    // TEST slice parameters, recomputed from this deployment only. Shared PlayerStats stays untouched.
    public int BulletDamage => 12;
    public float DodgeStartInterval => 0.8f;
    public bool HasModule(LoadoutSlot slot) => !string.IsNullOrEmpty(GetSlot(slot).ItemId);
    public int ArmCount => (HasModule(LoadoutSlot.LeftArm) ? 1 : 0) + (HasModule(LoadoutSlot.RightArm) ? 1 : 0);
    public int LegCount => (HasModule(LoadoutSlot.LeftLeg) ? 1 : 0) + (HasModule(LoadoutSlot.RightLeg) ? 1 : 0);
    public float ShotsPerSecond => 4f + 3f * ArmCount;
    public float FireInterval => 1f / ShotsPerSecond;
    public float MoveSpeed => 6f + 2f * LegCount;
    public bool HasTimeSlow => GetSlot(LoadoutSlot.Spine).ItemId == LoadoutRules.SpineSlow;

    internal DeploymentSnapshot(int officerIndex, LoadoutEntry[] source)
    {
        if (officerIndex < 1) throw new ArgumentOutOfRangeException(nameof(officerIndex));
        OfficerIndex = officerIndex;
        slots = (LoadoutEntry[])source.Clone();
        BodyModificationPercent = LoadoutRules.BodyModification(slots);
    }

    public LoadoutEntry GetSlot(LoadoutSlot slot) => slots[LoadoutRules.Index(slot)];
    internal DeploymentSnapshot Copy() => new DeploymentSnapshot(OfficerIndex, slots);
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
