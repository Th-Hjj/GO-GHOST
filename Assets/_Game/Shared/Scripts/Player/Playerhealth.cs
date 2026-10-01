using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Health, damage intake, death, and the "Last Stand" once-per-battle
/// revive: instead of dying, the player's onboard AI forces a slash-dash
/// through the nearest enemy and settles the player at 1 HP. Last Stand is LEGACY / TEST ONLY and
/// off in the formal slice; it is not part of the formal loadout architecture.
/// Death stops controls. Legacy scenes can auto-retry; the Boss slice disables
/// that option and owns its temporary result feedback.
/// </summary>
// FUNCTION SUMMARY
// TakeDamage()       — reduces health from an attack, or triggers Last Stand instead of dying if it's available.
// Heal()              — restores health up to the max, for pickups/regen.
// LastStandRoutine()  — the one-life-saving slash-dash sequence, called internally when the player would otherwise die.
// ResetLastStand()    — re-arms Last Stand; call this at the start of every new battle.
// Die()               — marks dead once, fires OnDeath, stops controls and optionally starts legacy retry.
// StopCombat()        — cancels action state and freezes the body, also used on victory.
// RetryRoutine()      — waits retryDelay, then reloads the active scene (only if it is in Build Settings).
[RequireComponent(typeof(PlayerDodge))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private PlayerStatsSO stats;

    [Header("Last Stand (legacy, test only)")]
    [Tooltip("LEGACY / TEST ONLY. Not a formal augmentation; keep off in formal content.")]
    [SerializeField] private bool lastStandUnlocked = false;

    [Header("Death / Retry")]
    [SerializeField] private bool autoRetryOnDeath = true;
    [Tooltip("Seconds between death and reloading the current scene.")]
    [SerializeField] private float retryDelay = 1.5f;
    [Tooltip("Sprite tint while dead (greybox feedback).")]
    [SerializeField] private Color deadTint = new Color(0.3f, 0.3f, 0.3f, 1f);

    public int CurrentHealth { get; private set; }
    public int MaxHealth => stats.maxHealth;
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

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
        StopCombat();

        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = deadTint;

        Debug.Log("[PlayerHealth] Player died.", this);
        if (autoRetryOnDeath) StartCoroutine(RetryRoutine());
    }

    public void StopCombat()
    {
        GetComponent<OfficerIdentity>()?.EndAttempt();
        StopAllCoroutines();
        // Stop acting: cancel a running dodge, cut input and the action components, freeze the body.
        if (dodge != null) dodge.CancelDodge();
        var reader = GetComponent<PlayerInputReader>();
        if (reader != null) reader.SetGameplayInputEnabled(false);
        SetEnabled<PlayerInputReader>(false);
        SetEnabled<PlayerMovement>(false);
        SetEnabled<PlayerShooting>(false);
        SetEnabled<PlayerDodge>(false);
        SetEnabled<PlayerTimeSlow>(false);
        SetEnabled<PlayerAbilitySystem>(false);

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
    }

    private IEnumerator RetryRoutine()
    {
        yield return new WaitForSeconds(retryDelay);

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex); // Single: destroys everything, so health/enemies/encounters reset with the scene
        }
        else
        {
            Debug.LogWarning($"[PlayerHealth] Scene '{scene.name}' is not in Build Settings, so it cannot be reloaded for retry. Stop Play Mode and restart it manually (Development scenes).", this);
        }
    }

    private void SetEnabled<T>(bool value) where T : Behaviour
    {
        var component = GetComponent<T>();
        if (component != null) component.enabled = value;
    }
}
