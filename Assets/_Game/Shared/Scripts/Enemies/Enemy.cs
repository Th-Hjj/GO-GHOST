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

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>Fired once, right before the enemy GameObject is destroyed.</summary>
    public event Action<Enemy> OnDied;

    private Rigidbody2D rb;
    private PlayerHealth player;
    private float nextFireTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CurrentHealth = maxHealth;
    }

    /// <summary>Sets the player this enemy chases and shoots. If never called, the enemy looks for a PlayerHealth in the scene on Start.</summary>
    public void SetTarget(PlayerHealth target)
    {
        player = target;
    }

    private void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerHealth>();
        nextFireTime = Time.time + fireInterval; // no instant shot on activation
    }

    private void FixedUpdate()
    {
        if (IsDead || player == null || player.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        rb.linearVelocity = toPlayer.magnitude > preferredDistance ? toPlayer.normalized * moveSpeed : Vector2.zero;
    }

    private void Update()
    {
        if (IsDead || player == null || player.IsDead) return;

        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        if (toPlayer.magnitude <= attackRange && Time.time >= nextFireTime)
        {
            Fire(toPlayer);
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void Fire(Vector2 toPlayer)
    {
        if (bulletPrefab == null) return;

        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        Vector2 spawn = rb.position + direction * bulletSpawnOffset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, bulletSpeed, bulletDamage, gameObject);
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0);
        if (CurrentHealth <= 0)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        rb.linearVelocity = Vector2.zero;
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
