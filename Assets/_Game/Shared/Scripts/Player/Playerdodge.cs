using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base: a short i-frame dodge dash in any direction (top-down): along the
/// Move input if the player is moving, otherwise along the aim direction.
/// Upgraded (slashDodgeUnlocked): longer dash that damages every enemy it
/// passes through.
/// Also exposes PerformForcedSlashDodge(), used exclusively by
/// PlayerHealth's Last Stand ability, which always slashes regardless of
/// the upgrade state.
/// </summary>
// FUNCTION SUMMARY
// TryDodge()                — starts a dodge if one isn't already on cooldown or in progress.
// GetDodgeDirection()        — Move direction if there is move input, otherwise the aim direction.
// DodgeRoutine()             — moves the player through the dash over time, granting i-frames and (if upgraded) damaging enemies passed through.
// PerformForcedSlashDodge()  — the guaranteed damaging slash used only by Last Stand, aimed at the nearest enemy.
// CancelDodge()              — stops any running dash and clears its state (used when the player dies).
// DamageEnemiesAtPoint()     — checks for enemies near a given dash position and damages each one exactly once per dash.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float slashCheckRadius = 0.6f;

    [Header("Upgrades")]
    [Tooltip("Unlocked via the upgrade tree: dodge becomes a longer slash that damages enemies it passes through.")]
    public bool slashDodgeUnlocked = false;
    [Tooltip("Set by PlayerUpgradeManager.UnlockAirDodge(). Not read yet: the dodge currently works regardless of this flag.")]
    public bool airDodgeUnlocked = false;

    public bool IsDodging { get; private set; }
    private bool canDodge = true;

    private Rigidbody2D rb;
    private PlayerHealth health;
    private PlayerInputReader input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        input = GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("PlayerDodge requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (input.DodgePressedThisFrame) // "Dodge" action in InputSystem_Actions (default: Space)
        {
            TryDodge();
        }
    }

    public void TryDodge()
    {
        if (!canDodge || IsDodging) return;
        StartCoroutine(DodgeRoutine());
    }

    /// <summary>Move direction while the player is moving, otherwise the aim direction. Always normalized, never zero.</summary>
    public Vector2 GetDodgeDirection()
    {
        if (input.HasMoveInput)
            return input.MoveInput.normalized;
        return input.AimDirection;
    }

    /// <summary>Stops any running dash (manual or forced) and clears its state. Used by PlayerHealth when the player dies.</summary>
    public void CancelDodge()
    {
        StopAllCoroutines();
        IsDodging = false;
        canDodge = true;
        if (health != null) health.IsInvulnerable = false;
    }

    private IEnumerator DodgeRoutine()
    {
        canDodge = false;
        IsDodging = true;
        health.IsInvulnerable = true;

        float distance = stats.dodgeDistance + (slashDodgeUnlocked ? stats.slashDodgeBonusDistance : 0f);
        Vector2 direction = GetDodgeDirection();
        Vector2 start = rb.position;
        Vector2 end = start + direction * distance;

        HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
        float elapsed = 0f;

        while (elapsed < stats.dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stats.dodgeDuration;
            Vector2 newPos = Vector2.Lerp(start, end, t);
            rb.MovePosition(newPos);

            if (slashDodgeUnlocked)
            {
                DamageEnemiesAtPoint(newPos, hitEnemies);
            }

            yield return null;
        }

        // MovePosition leaves the body with the dash velocity; stop it so the player does not slide past the end point.
        rb.linearVelocity = Vector2.zero;

        // Keep i-frames slightly beyond the physical dash for a forgiving dodge window.
        float remainingIFrames = stats.iFrameDuration - stats.dodgeDuration;
        if (remainingIFrames > 0f)
            yield return new WaitForSeconds(remainingIFrames);

        health.IsInvulnerable = false;
        IsDodging = false;

        yield return new WaitForSeconds(stats.dodgeCooldown);
        canDodge = true;
    }

    /// <summary>
    /// Used exclusively by PlayerHealth's Last Stand ability. Always slashes
    /// through enemies regardless of the slashDodgeUnlocked upgrade, aiming
    /// at the nearest enemy in range (or straight ahead if none is found).
    /// Does not touch canDodge/cooldown - it's a separate, forced action.
    /// </summary>
    public IEnumerator PerformForcedSlashDodge()
    {
        IsDodging = true;

        Vector2 start = rb.position;
        float distance = stats.dodgeDistance + stats.slashDodgeBonusDistance;
        Vector2 direction = input.AimDirection;

        // Aim the slash at the nearest enemy in range (any direction in top-down), otherwise along the aim.
        Collider2D[] candidates = Physics2D.OverlapCircleAll(start, distance, enemyLayer);
        float bestSqrDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            Vector2 toEnemy = (Vector2)candidate.transform.position - start;
            if (toEnemy.sqrMagnitude > 0.0001f && toEnemy.sqrMagnitude < bestSqrDistance)
            {
                bestSqrDistance = toEnemy.sqrMagnitude;
                direction = toEnemy.normalized;
            }
        }

        Vector2 end = start + direction * distance;
        HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
        float elapsed = 0f;

        while (elapsed < stats.dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stats.dodgeDuration;
            Vector2 newPos = Vector2.Lerp(start, end, t);
            rb.MovePosition(newPos);
            DamageEnemiesAtPoint(newPos, hitEnemies);
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        IsDodging = false;
    }

    private void DamageEnemiesAtPoint(Vector2 point, HashSet<IDamageable> alreadyHit)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, slashCheckRadius, enemyLayer);
        int damage = Mathf.RoundToInt(stats.gunDamage * stats.slashDodgeDamagePercent);

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !alreadyHit.Contains(damageable))
            {
                alreadyHit.Add(damageable);
                damageable.TakeDamage(damage, gameObject);
            }
        }
    }
}