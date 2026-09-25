using System;
using System.Collections.ObjectModel;

public enum WeaponCompatibility { Normal = 0, Modified = 1, Disabled = 2 }

/// <summary>All listed body slots are required and occupied together. Definitions have no runtime state.</summary>
public sealed class AugmentationDefinition
{
    public string ItemId { get; }
    public ReadOnlyCollection<LoadoutSlot> OccupiedSlots { get; }
    public WeaponCompatibility WeaponCompatibility { get; }

    public AugmentationDefinition(string itemId, WeaponCompatibility weaponCompatibility, params LoadoutSlot[] occupiedSlots)
    {
        if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("An augmentation needs an ID.");
        if (weaponCompatibility < WeaponCompatibility.Normal || weaponCompatibility > WeaponCompatibility.Disabled)
            throw new ArgumentOutOfRangeException(nameof(weaponCompatibility));
        if (occupiedSlots == null || occupiedSlots.Length == 0) throw new ArgumentException("An augmentation needs body slots.");
        var copy = (LoadoutSlot[])occupiedSlots.Clone();
        Array.Sort(copy);
        for (int i = 0; i < copy.Length; i++)
            if ((int)copy[i] < 0 || copy[i] >= LoadoutSlot.Weapon || (i > 0 && copy[i] == copy[i - 1]))
                throw new ArgumentException("Occupied slots must be distinct body slots.");
        ItemId = itemId;
        WeaponCompatibility = weaponCompatibility;
        OccupiedSlots = Array.AsReadOnly(copy);
    }

    public bool Occupies(LoadoutSlot slot) => OccupiedSlots.Contains(slot);
}
