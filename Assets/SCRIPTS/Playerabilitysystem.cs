using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the special ability button. On a fresh character this list is
/// empty, so the button does nothing - exactly as requested. As the player
/// unlocks abilities via PlayerUpgradeManager, they get added here and
/// become selectable/usable.
/// </summary>
// FUNCTION SUMMARY
// Update()             — listens for the special-ability button and the ability-cycling keys each frame.
// UnlockAbility()       — adds a new ability to the player's usable list when it's unlocked via the upgrade tree.
// CycleAbility()        — switches which unlocked ability is currently selected.
// UseSelectedAbility()  — activates the currently selected ability if it's off cooldown (does nothing if none are unlocked).
public class PlayerAbilitySystem : MonoBehaviour
{
    [Tooltip("Grows as the player unlocks abilities through upgrades. Empty on base.")]
    [SerializeField] private List<PlayerAbilitySO> unlockedAbilities = new List<PlayerAbilitySO>();

    private int selectedIndex = -1;
    private readonly Dictionary<PlayerAbilitySO, float> cooldownTimers = new Dictionary<PlayerAbilitySO, float>();

    public IReadOnlyList<PlayerAbilitySO> UnlockedAbilities => unlockedAbilities;
    public PlayerAbilitySO SelectedAbility => selectedIndex >= 0 ? unlockedAbilities[selectedIndex] : null;

    private void Update()
    {
        if (Input.GetButtonDown("Fire2")) // assign your "special ability" button to this axis, or swap for your own input
        {
            UseSelectedAbility();
        }

        // Optional: let the player cycle between unlocked abilities.
        if (Input.GetKeyDown(KeyCode.Q)) CycleAbility(-1);
        if (Input.GetKeyDown(KeyCode.E)) CycleAbility(1);
    }

    public void UnlockAbility(PlayerAbilitySO ability)
    {
        if (ability == null || unlockedAbilities.Contains(ability)) return;
        unlockedAbilities.Add(ability);
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

        if (ability.Activate(gameObject))
        {
            cooldownTimers[ability] = Time.time + ability.cooldown;
        }
    }
}