using UnityEngine;

/// <summary>Static stickman parts, colored once from the sealed deployment. No animator or mutable draft.</summary>
public sealed class OfficerVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer leftArm;
    [SerializeField] private SpriteRenderer rightArm;
    [SerializeField] private SpriteRenderer leftLeg;
    [SerializeField] private SpriteRenderer rightLeg;
    [SerializeField] private SpriteRenderer torso;
    [SerializeField] private SpriteRenderer head;
    [SerializeField] private SpriteRenderer weapon;
    [SerializeField] private SpriteRenderer weaponGrip;
    private bool reloading;
    public bool ReloadVisible => reloading && weapon != null && weapon.enabled && weapon.gameObject.activeInHierarchy;

    public void ConfigureLegacyCombat()
    {
        foreach (var part in new[] { leftArm, rightArm, leftLeg, rightLeg, torso, head, weapon, weaponGrip })
            if (part != null) part.color = Color.white;
        SetReloading(false);
    }

    // Physical weapon feedback, shared by the player and the observer. It never changes muzzle aim.
    public void SetReloading(bool value)
    {
        reloading = value;
        UpdateReloadVisual();
    }

    private void LateUpdate()
    {
        if (reloading) UpdateReloadVisual();
    }

    private void UpdateReloadVisual()
    {
        float shade = reloading ? .32f + .18f * Mathf.Sin(Time.time * 16f) : 1f;
        Color tint = new Color(shade, shade, shade, 1f);
        if (weapon != null) weapon.color = tint;
        if (weaponGrip != null) weaponGrip.color = tint;
    }
    // The UI and actor share this palette. Brightened indigo stays readable against black.
    public static Color SlotColor(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => new Color32(255, 90, 95, 255),
        LoadoutSlot.RightArm => new Color32(255, 172, 74, 255),
        LoadoutSlot.LeftLeg => new Color32(255, 228, 94, 255),
        LoadoutSlot.RightLeg => new Color32(88, 222, 131, 255),
        LoadoutSlot.Torso => new Color32(72, 174, 255, 255),
        LoadoutSlot.Brain => new Color32(132, 130, 255, 255),
        LoadoutSlot.Weapon => new Color32(209, 132, 255, 255),
        _ => Color.white
    };
    public static Color EquipmentColor(LoadoutSlot slot, string itemId) =>
        string.IsNullOrEmpty(itemId) || slot == LoadoutSlot.Weapon // guns keep the plain white placeholder
            ? Color.white : SlotColor(slot);
    public static Color AugmentedColor => SlotColor(LoadoutSlot.LeftArm); // historical developer fixture compatibility

    public SpriteRenderer Part(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => leftArm, LoadoutSlot.RightArm => rightArm,
        LoadoutSlot.LeftLeg => leftLeg, LoadoutSlot.RightLeg => rightLeg,
        LoadoutSlot.Torso => torso, LoadoutSlot.Brain => head, LoadoutSlot.Weapon => weapon, _ => null
    };

    public void Apply(DeploymentSnapshot snapshot)
    {
        for (int i = 0; i < LoadoutRules.SlotCount; i++)
        {
            var part = Part((LoadoutSlot)i);
            if (part != null) part.color = EquipmentColor((LoadoutSlot)i, snapshot.GetSlot((LoadoutSlot)i).ItemId);
        }
        if (weaponGrip != null) weaponGrip.color = EquipmentColor(LoadoutSlot.Weapon, snapshot.WeaponId);
    }
}
