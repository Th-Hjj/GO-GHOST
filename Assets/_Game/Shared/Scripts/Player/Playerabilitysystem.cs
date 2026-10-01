using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LEGACY / TEST ONLY. Not part of the formal GO GHOST augmentation architecture
/// (LoadoutDraft -> DeploymentSnapshot -> IAttemptAugmentation); disabled in the formal combat prefab.
/// Handles the old special ability button. On a fresh character this list is
/// empty, so the button does nothing. Abilities added via UnlockAbility()
/// become selectable/usable. Abilities with a maxUses > 0 (e.g. Freeze,
/// meant to be once-per-battle) are also tracked with a limited charge
/// count separate from their cooldown.
/// </summary>
// FUNCTION SUMMARY
// Update()             — listens for the special-ability button and the ability-cycling keys each frame.
// UnlockAbility()       — adds a new ability to the player's usable list (legacy test path only), and grants its starting charges.
// CycleAbility()        — switches which unlocked ability is currently selected.
// UseSelectedAbility()  — activates the currently selected ability if it's off cooldown and has charges left (does nothing if none are unlocked).
// ResetAbilityUses()    — recharges every limited-use ability back to its maxUses; call at the start of a new battle.
public class PlayerAbilitySystem : MonoBehaviour
{
    [Tooltip("Grows as the player unlocks abilities through upgrades. Empty on base.")]
    [SerializeField] private List<PlayerAbilitySO> unlockedAbilities = new List<PlayerAbilitySO>();

    private int selectedIndex = -1;
    private readonly Dictionary<PlayerAbilitySO, float> cooldownTimers = new Dictionary<PlayerAbilitySO, float>();
    private readonly Dictionary<PlayerAbilitySO, int> usesRemaining = new Dictionary<PlayerAbilitySO, int>();

    public IReadOnlyList<PlayerAbilitySO> UnlockedAbilities => unlockedAbilities;
    public PlayerAbilitySO SelectedAbility => selectedIndex >= 0 ? unlockedAbilities[selectedIndex] : null;

    private PlayerInputReader input;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("PlayerAbilitySystem requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (input.AbilityPressedThisFrame) // "Ability" action in InputSystem_Actions (bind to right mouse button)
        {
            UseSelectedAbility();
        }

        // Optional: let the player cycle between unlocked abilities ("Previous"/"Next" actions, default: 1 / 2).
        if (input.PreviousPressedThisFrame) CycleAbility(-1);
        if (input.NextPressedThisFrame) CycleAbility(1);
    }

    public void UnlockAbility(PlayerAbilitySO ability)
    {
        if (ability == null || unlockedAbilities.Contains(ability)) return;
        unlockedAbilities.Add(ability);
        if (ability.maxUses > 0) usesRemaining[ability] = ability.maxUses;
        if (selectedIndex == -1) selectedIndex = 0; // auto-select the first ability ever unlocked
    }

    public void CycleAbility(int direction)
    {
        if (unlockedAbilities.Count == 0) return;
        selectedIndex = (selectedIndex + direction + unlockedAbilities.Count) % unlockedAbilities.Count;
    }

    public void UseSelectedAbility()
    {
        if (unlockedAbilities.Count == 0 || selectedIndex < 0) return; // base character: nothing unlocked yet

        PlayerAbilitySO ability = unlockedAbilities[selectedIndex];

        if (cooldownTimers.TryGetValue(ability, out float readyTime) && Time.time < readyTime)
            return; // still on cooldown

        if (ability.maxUses > 0 && (!usesRemaining.TryGetValue(ability, out int remaining) || remaining <= 0))
            return; // out of charges until ResetAbilityUses() is called

        if (ability.Activate(gameObject))
        {
            cooldownTimers[ability] = Time.time + ability.cooldown;
            if (ability.maxUses > 0) usesRemaining[ability] = usesRemaining[ability] - 1;
        }
    }

    /// <summary>Recharges every limited-use ability back to its maxUses. Call this whenever a new battle/encounter starts.</summary>
    public void ResetAbilityUses()
    {
        foreach (var ability in unlockedAbilities)
        {
            if (ability.maxUses > 0)
                usesRemaining[ability] = ability.maxUses;
        }
    }
}