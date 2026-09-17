using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base: a short i-frame dodge dash.
/// Upgraded (slashDodgeUnlocked): longer dash that damages every enemy it
/// passes through.
/// Also exposes PerformForcedSlashDodge(), used exclusively by
/// PlayerHealth's Last Stand ability, which always slashes regardless of
/// the upgrade state.
/// </summary>
// FUNCTION SUMMARY
// TryDodge()                — starts a dodge if one isn't already on cooldown or in progress.
// DodgeRoutine()             — moves the player through the dash over time, granting i-frames and (if upgraded) damaging enemies passed through.
// PerformForcedSlashDodge()  — the guaranteed damaging slash used only by Last Stand, aimed at the nearest enemy.
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

    public bool IsDodging { get; private set; }
    private bool canDodge = true;

    private Rigidbody2D rb;
    private PlayerHealth health;
    private PlayerMovement movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire3")) // assign your "dodge" button to this axis, or swap for your own input
        {
            TryDodge();
        }
    }

    public void TryDodge()
    {
        if (!canDodge || IsDodging) return;
        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        canDodge = false;
        IsDodging = true;
        health.IsInvulnerable = true;

        float distance = stats.dodgeDistance + (slashDodgeUnlocked ? stats.slashDodgeBonusDistance : 0f);
        Vector2 direction = new Vector2(movement.FacingDirection, 0f);
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
        Vector2 direction = new Vector2(movement.FacingDirection, 0f);

        Collider2D nearest = Physics2D.OverlapCircle(start, distance, enemyLayer);
        if (nearest != null)
        {
            float towardEnemy = nearest.transform.position.x - start.x;
            if (Mathf.Abs(towardEnemy) > 0.01f)
                direction = new Vector2(Mathf.Sign(towardEnemy), 0f); // slash stays horizontal
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