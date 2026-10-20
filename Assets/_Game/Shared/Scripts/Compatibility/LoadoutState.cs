using System;

// Keep serialized ordinal 4: Torso replaces the old Spine concept without shifting any slot.
public enum LoadoutSlot { LeftArm = 0, RightArm = 1, LeftLeg = 2, RightLeg = 3, Torso = 4, Brain = 5, Weapon = 6 }
public enum AimControl { Manual, AI }

/// <summary>Immutable slot value. TEST-v0 contributions belong to equipped items, never installation history.</summary>
public readonly struct LoadoutEntry
{
    public LoadoutSlot Slot { get; }
    public string ItemId { get; }
    public float BodyContributionPercent { get; }
    public LoadoutSlot InstallationSlot { get; }
    public WeaponCompatibility WeaponCompatibility { get; }

    internal LoadoutEntry(LoadoutSlot slot, string itemId, float contribution,
        LoadoutSlot? installationSlot = null, WeaponCompatibility weaponCompatibility = WeaponCompatibility.Normal)
    {
        Slot = slot;
        ItemId = itemId;
        BodyContributionPercent = slot == LoadoutSlot.Weapon ? 0f : contribution;
        InstallationSlot = installationSlot ?? slot;
        WeaponCompatibility = weaponCompatibility;
    }
}

public static partial class LoadoutRules
{
    public const int SlotCount = 7;
    public const string AssaultRifle = "assault_rifle";
    /// <summary>The Weapon slot choices offered before deployment, in display order.</summary>
    public static readonly string[] OfferedWeapons = { AssaultRifle };
    public static bool IsWeapon(string id) => id == AssaultRifle;

    // TEST values. Assault Rifle keeps the previous gun damage and rate (12 damage, 4 shots/s) with a 10-round magazine.
    public static WeaponProfile Weapon(string id) => IsWeapon(id)
        ? new WeaponProfile(12, .25f, 10, 1.2f)
        : throw new ArgumentException("Unsupported weapon ID.", nameof(id));
    public const string ArmFireRate = "test_v2_arm_rate";
    public const string LegMoveSpeed = "test_v2_leg_speed";
    public const string LegDoubleJump = "test_leg_double_jump";
    public const string LegHover = "test_leg_hover";
    public const string LegAirDash = "leg_air_dash";
    // Preserve the existing item ID; this prototype Torso option has no assigned final tier.
    public const string TorsoSlow = "test_v2_spine_slow";
    public const string BrainAI = "ai_aim";
    public const string DetachedDroneArm = "detached_drone_arm";
    public const string CounterArm = "test_counter_arm"; // hidden from the current catalog; its function lives on in Arm-Blade
    public const string ArmBlade = "arm_blade";
    public const string PredictiveCortex = "test_predictive_cortex";

    internal static readonly AugmentationDefinition[] PrototypeCatalog =
    {
        new AugmentationDefinition(ArmFireRate, WeaponCompatibility.Normal, LoadoutSlot.LeftArm),
        new AugmentationDefinition(ArmFireRate, WeaponCompatibility.Normal, LoadoutSlot.RightArm),
        new AugmentationDefinition(LegMoveSpeed, WeaponCompatibility.Normal, LoadoutSlot.LeftLeg),
        new AugmentationDefinition(LegMoveSpeed, WeaponCompatibility.Normal, LoadoutSlot.RightLeg),
        new AugmentationDefinition(LegDoubleJump, WeaponCompatibility.Normal, LoadoutSlot.LeftLeg),
        new AugmentationDefinition(LegDoubleJump, WeaponCompatibility.Normal, LoadoutSlot.RightLeg),
        new AugmentationDefinition(LegHover, WeaponCompatibility.Normal, LoadoutSlot.LeftLeg),
        new AugmentationDefinition(LegHover, WeaponCompatibility.Normal, LoadoutSlot.RightLeg),
        new AugmentationDefinition(TorsoSlow, WeaponCompatibility.Normal, LoadoutSlot.Torso),
        new AugmentationDefinition(BrainAI, WeaponCompatibility.Normal, LoadoutSlot.Brain),
        new AugmentationDefinition(CounterArm, WeaponCompatibility.Normal, LoadoutSlot.LeftArm),
        new AugmentationDefinition(PredictiveCortex, WeaponCompatibility.Normal, LoadoutSlot.Brain),
        new AugmentationDefinition(DetachedDroneArm, WeaponCompatibility.Normal, LoadoutSlot.LeftArm),
        new AugmentationDefinition(ArmBlade, WeaponCompatibility.Normal, LoadoutSlot.LeftArm),
        new AugmentationDefinition(LegAirDash, WeaponCompatibility.Normal, LoadoutSlot.LeftLeg),
        new AugmentationDefinition(LegAirDash, WeaponCompatibility.Normal, LoadoutSlot.RightLeg)
    };

    /// <summary>
    /// The upgrade choices shown before deployment this round. Hidden items (Left Arm Rapid Fire, Move Speed, Counter Arm,
    /// Predictive Cortex) keep their IDs, labels and behaviour for older records and fixtures; they are only not listed.
    /// </summary>
    public static bool IsOffered(LoadoutSlot slot, string itemId) => slot switch
    {
        LoadoutSlot.LeftArm => itemId == ArmBlade || itemId == DetachedDroneArm,
        LoadoutSlot.RightArm => itemId == ArmFireRate,
        LoadoutSlot.LeftLeg or LoadoutSlot.RightLeg => itemId == LegDoubleJump || itemId == LegHover || itemId == LegAirDash,
        LoadoutSlot.Torso => itemId == TorsoSlow,
        LoadoutSlot.Brain => itemId == BrainAI,
        _ => false
    };

    internal static WeaponCompatibility WeaponPolicy(LoadoutEntry[] slots)
    {
        var policy = WeaponCompatibility.Normal;
        // Most restrictive declaration wins. Effect details for Modified belong to future weapon behavior.
        for (int i = 0; i < (int)LoadoutSlot.Weapon; i++)
            if (slots[i].WeaponCompatibility > policy) policy = slots[i].WeaponCompatibility;
        return policy;
    }

    // Prototype capacities are per occupied body slot, independent of the chosen replacement.
    public static float MaximumContribution(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm or LoadoutSlot.RightArm or LoadoutSlot.LeftLeg or LoadoutSlot.RightLeg => 10f,
        LoadoutSlot.Torso => 20f, LoadoutSlot.Brain => 40f, _ => 0f
    };

    public static string ModuleForSlot(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm or LoadoutSlot.RightArm => ArmFireRate,
        LoadoutSlot.LeftLeg or LoadoutSlot.RightLeg => LegMoveSpeed,
        LoadoutSlot.Torso => TorsoSlow,
        LoadoutSlot.Brain => BrainAI, _ => null
    };

    public static string ItemLabel(string id) => id switch
    {
        ArmFireRate => "Rapid Fire", LegMoveSpeed => "Move Speed", TorsoSlow => "Time Slow", BrainAI => "Targeting AI",
        CounterArm => "Counter Arm", PredictiveCortex => "Predictive Cortex", DetachedDroneArm => "Detached Drone Arm",
        LegDoubleJump => "Double Jump", LegHover => "Hover", LegAirDash => "Air Dash", ArmBlade => "Arm-Blade",
        AssaultRifle => "Assault Rifle", _ => "Human / Empty"
    };

    public static bool IsLeg(LoadoutSlot slot) => slot == LoadoutSlot.LeftLeg || slot == LoadoutSlot.RightLeg;
    // Existing leg buttons cycle replacement choices; no tier tree or hidden-item UI.
    public static string NextLegOption(string current) => current switch
    {
        LegMoveSpeed => LegDoubleJump, LegDoubleJump => LegHover, LegHover => "", _ => LegMoveSpeed
    };

    public static string WeaponLabel(string id) => id switch
    {
        AssaultRifle => "Assault Rifle", _ => id
    };
    public static string SlotLabel(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => "Left Arm", LoadoutSlot.RightArm => "Right Arm",
        LoadoutSlot.LeftLeg => "Left Leg", LoadoutSlot.RightLeg => "Right Leg",
        _ => slot.ToString()
    };

    // CONFIRMED 2026-10-01: this is the one Cyberization Percentage (0-100). Sum of the six currently equipped body
    // slots (TEST weights 10/10/10/10/20/40); Weapon is never counted. Never normalize against the implemented catalog.
    internal static float BodyModification(LoadoutEntry[] slots)
    {
        float total = 0f;
        for (int i = 0; i < (int)LoadoutSlot.Weapon; i++) total += slots[i].BodyContributionPercent;
        return total < 0f ? 0f : total > 100f ? 100f : total;
    }

    internal static int Index(LoadoutSlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return index;
    }
}

/// <summary>Private copy of value entries; callers can neither mutate nor retrieve its array.</summary>
public sealed class DeploymentSnapshot
{
    private readonly LoadoutEntry[] slots;
    private readonly AugmentationDefinition[] catalog;
    public int OfficerIndex { get; }
    /// <summary>S01 DEV: -1 keeps legacy Brain behaviour; 0..3 selects the permission prototype, with manual base aim.</summary>
    public int BrainPermissionTier { get; }
    public float BodyModificationPercent { get; }
    public string WeaponId => GetSlot(LoadoutSlot.Weapon).ItemId;
    public WeaponCompatibility WeaponCompatibility => LoadoutRules.WeaponPolicy(slots);
    public bool CanUseWeapon => WeaponCompatibility != WeaponCompatibility.Disabled;
    public AimControl Aim => BrainPermissionTier < 0 && GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.BrainAI ? AimControl.AI : AimControl.Manual;
    // TEST slice parameters, recomputed from this deployment only. Shared PlayerStats stays untouched.
    public WeaponProfile Weapon => LoadoutRules.Weapon(WeaponId);
    public int BulletDamage => Weapon.Damage;
    public float DodgeStartInterval => 0.8f;
    public bool HasModule(LoadoutSlot slot) => !string.IsNullOrEmpty(GetSlot(slot).ItemId);
    public int ArmCount => (GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.ArmFireRate ? 1 : 0)
        + (GetSlot(LoadoutSlot.RightArm).ItemId == LoadoutRules.ArmFireRate ? 1 : 0);
    public int LegCount => (GetSlot(LoadoutSlot.LeftLeg).ItemId == LoadoutRules.LegMoveSpeed ? 1 : 0)
        + (GetSlot(LoadoutSlot.RightLeg).ItemId == LoadoutRules.LegMoveSpeed ? 1 : 0);
    // Rapid Fire scales the equipped weapon's own base interval (Assault Rifle: 4 -> 7 -> 10 shots/s as before).
    public float ShotsPerSecond => (1f + .75f * ArmCount) / Weapon.FireInterval;
    public float FireInterval => 1f / ShotsPerSecond;
    public float MoveSpeed => 7f + 2f * LegCount; // TEST: base raised from 6 after Thomas's hands-on play
    public bool HasTimeSlow => GetSlot(LoadoutSlot.Torso).ItemId == LoadoutRules.TorsoSlow;
    public bool HasDetachedDrone => GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.DetachedDroneArm;
    public bool HasCounterArm => GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.CounterArm;
    public bool HasArmBlade => GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.ArmBlade;
    public bool HasPredictiveCortex => GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.PredictiveCortex;
    private bool HasLegOption(string id) => GetSlot(LoadoutSlot.LeftLeg).ItemId == id || GetSlot(LoadoutSlot.RightLeg).ItemId == id;
    public bool HasDoubleJump => HasLegOption(LoadoutRules.LegDoubleJump);
    public bool HasHover => HasLegOption(LoadoutRules.LegHover);
    public bool HasAirDash => HasLegOption(LoadoutRules.LegAirDash);

    internal DeploymentSnapshot(int officerIndex, LoadoutEntry[] source, AugmentationDefinition[] definitions = null, int brainPermissionTier = -1)
    {
        if (officerIndex < 1) throw new ArgumentOutOfRangeException(nameof(officerIndex));
        if (brainPermissionTier < -1 || brainPermissionTier > 3) throw new ArgumentOutOfRangeException(nameof(brainPermissionTier));
        OfficerIndex = officerIndex;
        BrainPermissionTier = brainPermissionTier;
        slots = (LoadoutEntry[])source.Clone();
        if (!LoadoutRules.IsWeapon(slots[(int)LoadoutSlot.Weapon].ItemId))
            throw new ArgumentException("Only Assault Rifle can be deployed.", nameof(source));
        catalog = (AugmentationDefinition[])(definitions ?? LoadoutRules.PrototypeCatalog).Clone();
        BodyModificationPercent = LoadoutRules.BodyModification(slots);
    }

    public LoadoutEntry GetSlot(LoadoutSlot slot) => slots[LoadoutRules.Index(slot)];
    internal AugmentationDefinition[] CopyCatalog() => (AugmentationDefinition[])catalog.Clone();
    internal DeploymentSnapshot Copy() => new DeploymentSnapshot(OfficerIndex, slots, catalog, BrainPermissionTier);
}
