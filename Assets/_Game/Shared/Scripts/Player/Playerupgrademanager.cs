using UnityEngine;

/// <summary>
/// Single entry point for your level-up / skill-tree UI. Keeps the
/// individual player components focused on runtime behaviour rather than
/// progression logic - wire your UI buttons to these methods.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerDodge))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAbilitySystem))]
// FUNCTION SUMMARY
// UnlockDoubleJump()  — turns on the double jump upgrade.
// UnlockSlashDodge()  — turns on the longer, damaging slash-dodge upgrade.
// UnlockLastStand()   — turns on the once-per-battle revive ability.
// UnlockAbility()     — adds a new special ability to the player's usable list.
public class PlayerUpgradeManager : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerDodge dodge;
    private PlayerHealth health;
    private PlayerAbilitySystem abilitySystem;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<PlayerHealth>();
        abilitySystem = GetComponent<PlayerAbilitySystem>();
    }

    public void UnlockDoubleJump() => movement.UnlockDoubleJump();

    public void UnlockSlashDodge() => dodge.slashDodgeUnlocked = true;

    public void UnlockLastStand() => health.UnlockLastStand();

    public void UnlockAbility(PlayerAbilitySO ability) => abilitySystem.UnlockAbility(ability);
}