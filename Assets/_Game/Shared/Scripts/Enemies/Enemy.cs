using System;
using UnityEngine;

/// <summary>
/// Minimal ranged enemy for the vertical slice. Chases the player until it
/// is within preferredDistance, then holds position, and fires a Projectile
/// at the player every fireInterval while the player is in attackRange.
/// Implements IDamageable so the player's bullets and slash dodge can kill it.
/// Put it on the "Enemy" layer: the player's dodge/abilities find enemies by
/// layer, and enemy bullets never hit objects on their shooter's layer.
/// </summary>
// FUNCTION SUMMARY
// SetTarget()   — gives the enemy the player to chase/shoot (EncounterZone calls this on activation).
// FixedUpdate() — moves toward the player until within preferredDistance.
// Update()      — fires at the player when in range and the fire interval has elapsed.
// TakeDamage()  — applies damage; at 0 health fires OnDied and destroys the enemy.
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 30;
    [Tooltip("0 = one life (default). Above 0: when the first health pool reaches 0 the enemy does not die; it is immune until BeginSecondPhase refills this second, independent pool. TEST value on the Boss.")]
    [SerializeField] private int secondPhaseMaxHealth = 0;

    [Header("Behaviour ownership")]
    [Tooltip("Disable when a level-specific boss controls movement and attacks. Health and damage remain active.")]
    [SerializeField] private bool automaticCombat = true;
    [SerializeField] private bool destroyOnDeath = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Stops approaching once the player is closer than this.")]
    [SerializeField] private float preferredDistance = 4f;

    [Header("Attack")]
    [SerializeField] private Projectile bulletPrefab;
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private float bulletSpeed = 8f;
    [SerializeField] private int bulletDamage = 10;
    [Tooltip("Only fires when the player is within this range.")]
    [SerializeField] private float attackRange = 10f;
    [Tooltip("Distance from the enemy centre where bullets spawn. Keep it larger than the enemy's half size plus the bullet radius.")]
    [SerializeField] private float bulletSpawnOffset = 1.1f;

    /// <summary>Health of the current phase only.</summary>
    public int CurrentHealth { get; private set; }
    public int MaxHealth => PhaseIndex == 2 ? secondPhaseMaxHealth : maxHealth;
    public bool IsDead { get; private set; }
    /// <summary>1, or 2 after BeginSecondPhase.</summary>
    public int PhaseIndex { get; private set; } = 1;
    /// <summary>First pool spent: alive, still targetable, every damage source is rejected until BeginSecondPhase.</summary>
    public bool AwaitingSecondPhase { get; private set; }

    /// <summary>Fired once on death; optional destruction happens afterwards.</summary>
    public event Action<Enemy> OnDied;
    /// <summary>Actual health loss only; level actors may interrupt an uncommitted prepare.</summary>
    public event Action<int, GameObject> OnDamaged;

    private Rigidbody2D rb;
    private PlayerHealth player;
    private float fireRemaining;
    private PlayerTimeSlow hostileClock;
    private bool combatStopped;
    private bool dangerSignalled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CurrentHealth = maxHealth;
    }

    /// <summary>Sets the player this enemy chases and shoots. If never called, the enemy looks for a PlayerHealth in the scene on Start.</summary>
    public void SetTarget(PlayerHealth target)
    {
        player = target;
        hostileClock = target != null ? target.GetComponent<PlayerTimeSlow>() : null;
    }

    private void Start()
    {
        if (player == null)
            SetTarget(FindAnyObjectByType<PlayerHealth>());
        fireRemaining = fireInterval; // no instant shot on activation
    }

    private void FixedUpdate()
    {
        if (!automaticCombat || combatStopped) return;
        if (IsDead || player == null || player.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        rb.linearVelocity = toPlayer.magnitude > preferredDistance ? toPlayer.normalized * moveSpeed * (hostileClock != null ? hostileClock.WorldRate : 1f) : Vector2.zero;
    }

    private void Update()
    {
        if (!automaticCombat || combatStopped || IsDead || player == null || player.IsDead || Time.deltaTime <= 0f) return;
        fireRemaining -= Time.deltaTime * (hostileClock != null ? hostileClock.WorldRate : 1f);

        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        if (toPlayer.magnitude <= attackRange && fireRemaining <= InnerAIDangerSense.LeadTimeFor(player) && !dangerSignalled)
        { InnerAIDangerSense.Signal(player, transform, Mathf.Max(0, fireRemaining)); dangerSignalled = true; }
        if (toPlayer.magnitude <= attackRange && fireRemaining <= 0f)
        {
            Fire(toPlayer);
            fireRemaining = fireInterval;
            dangerSignalled = false;
        }
    }

    private void Fire(Vector2 toPlayer)
    {
        if (bulletPrefab == null) return;

        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        Vector2 spawn = rb.position + direction * bulletSpawnOffset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, bulletSpeed, bulletDamage, gameObject, hostileClock);
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (IsDead || AwaitingSecondPhase || amount <= 0) return;

        int lost = Mathf.Min(CurrentHealth, amount);
        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0);
        OnDamaged?.Invoke(lost, source);
        if (CurrentHealth > 0) return;
        // The first pool ending is not a death: no OnDied, no victory. Only the last pool kills.
        if (PhaseIndex == 1 && secondPhaseMaxHealth > 0) AwaitingSecondPhase = true;
        else Die();
    }

    /// <summary>Called by the owner of the phase change once its transition is over: full second pool, damage allowed again.</summary>
    public void BeginSecondPhase()
    {
        if (IsDead || !AwaitingSecondPhase) return;
        AwaitingSecondPhase = false;
        PhaseIndex = 2;
        CurrentHealth = secondPhaseMaxHealth;
    }

    /// <summary>Fresh fixed-combat actor: one life, no phase gate and no second movement writer.</summary>
    public void ConfigureFixedSingleLife(int healthPoints)
    {
        if (IsDead || CurrentHealth != maxHealth) throw new InvalidOperationException("Configure a fresh enemy before damage.");
        maxHealth = Mathf.Max(1, healthPoints);
        secondPhaseMaxHealth = 0; PhaseIndex = 1; AwaitingSecondPhase = false;
        CurrentHealth = maxHealth; automaticCombat = false; destroyOnDeath = false; combatStopped = false;
    }

    // Runtime developer tuning preserves the current life, AI state and damage-event history.
    public void SetRuntimeHealth(int maximum, int current)
    {
        if(IsDead || PhaseIndex!=1 || secondPhaseMaxHealth>0)return;
        maxHealth=Mathf.Max(1,maximum);
        CurrentHealth=Mathf.Clamp(current,1,maxHealth);
    }

    /// <summary>Fresh Tower actor only: one existing health pool, with no automatic second life.</summary>
    public void ConfigureSingleEncounter(bool secondPhase)
    {
        if (IsDead || CurrentHealth != maxHealth) throw new InvalidOperationException("Configure a fresh enemy before damage.");
        int pool = secondPhase && secondPhaseMaxHealth > 0 ? secondPhaseMaxHealth : maxHealth;
        maxHealth = pool;
        secondPhaseMaxHealth = secondPhase ? pool : 0;
        PhaseIndex = secondPhase ? 2 : 1;
        CurrentHealth = pool;
    }

    public void ResumeCombat() { if (!IsDead) combatStopped = false; }

    public void StopCombat()
    {
        combatStopped = true;
        dangerSignalled = false;
        InnerAIDangerSense.Cancel(player, transform);
        rb.linearVelocity = Vector2.zero;
    }

    private void Die()
    {
        IsDead = true;
        StopCombat();
        OnDied?.Invoke(this);
        if (destroyOnDeath) Destroy(gameObject);
    }
}
