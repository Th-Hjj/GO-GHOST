using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Health, damage intake, death, and the "Last Stand" once-per-battle
/// revive: instead of dying, the player's onboard AI forces a slash-dash
/// through the nearest enemy and settles the player at 1 HP.
/// </summary>
// FUNCTION SUMMARY
// TakeDamage()       — reduces health from an attack, or triggers Last Stand instead of dying if it's available.
// Heal()              — restores health up to the max, for pickups/regen.
// LastStandRoutine()  — the one-life-saving slash-dash sequence, called internally when the player would otherwise die.
// ResetLastStand()    — re-arms Last Stand; call this at the start of every new battle.
// UnlockLastStand()   — permanently turns on the Last Stand ability via the upgrade tree.
// Die()               — marks the player dead and fires OnDeath for other systems to react to.
[RequireComponent(typeof(PlayerDodge))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private PlayerStatsSO stats;

    [Header("Last Stand")]
    [Tooltip("Unlocked via the upgrade tree.")]
    [SerializeField] private bool lastStandUnlocked = false;

    public int CurrentHealth { get; private set; }
    public bool IsInvulnerable { get; set; }
    public bool IsDead { get; private set; }

    /// <summary>True once per battle until ResetLastStand() is called again.</summary>
    public bool LastStandAvailable { get; private set; } = true;

    // Hook UI / your partner's systems into these.
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDeath;
    public event Action OnLastStandTriggered;

    private PlayerDodge dodge;

    private void Awake()
    {
        dodge = GetComponent<PlayerDodge>();
        CurrentHealth = stats.maxHealth;
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (IsDead || IsInvulnerable || amount <= 0) return;

        int projectedHealth = CurrentHealth - amount;

        if (projectedHealth <= 0 && lastStandUnlocked && LastStandAvailable)
        {
            LastStandAvailable = false;
            StartCoroutine(LastStandRoutine());
            return;
        }

        CurrentHealth = Mathf.Max(projectedHealth, 0);
        OnHealthChanged?.Invoke(CurrentHealth, stats.maxHealth);

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, stats.maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, stats.maxHealth);
    }

    /// <summary>
    /// "The AI inside his cyborg body chose to save him."
    /// Player becomes invulnerable, slash-dashes through the nearest enemy
    /// (or straight ahead if none is in range) dealing damage, then settles
    /// at 1 HP instead of dying.
    /// </summary>
    private IEnumerator LastStandRoutine()
    {
        IsInvulnerable = true;
        OnLastStandTriggered?.Invoke();

        yield return dodge.PerformForcedSlashDodge();

        CurrentHealth = 1;
        OnHealthChanged?.Invoke(CurrentHealth, stats.maxHealth);
        IsInvulnerable = false;
    }

    /// <summary>Call this whenever a new battle/encounter starts so the ability is armed again.</summary>
    public void ResetLastStand()
    {
        LastStandAvailable = true;
    }

    public void UnlockLastStand()
    {
        lastStandUnlocked = true;
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
        // TODO: disable movement/shooting components, play death animation,
        // trigger your game-over flow, etc.
    }
}