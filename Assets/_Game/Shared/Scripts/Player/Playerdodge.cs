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
using System.Collections;
using UnityEngine;

/// <summary>
/// Fires a Projectile along the mouse aim direction (PlayerInputReader.AimDirection).
/// The muzzle is the player's position offset along the aim by the FirePoint
/// child's original distance, so aiming is independent of the movement direction.
/// Base: hold Attack to fire continuously, governed by stats.fireRate.
/// Upgraded (volleyUnlocked): each Attack press fires a burst of
/// stats.volleyShotCount shots, stats.volleyShotDelay apart.
/// </summary>
// FUNCTION SUMMARY
// Update()            — keeps the FirePoint child on the muzzle; fires (continuous or volley burst) based on Attack input and the Volley upgrade.
// GetMuzzlePosition() — world-space spawn point for a given aim direction.
// Fire()              — spawns a single bullet at the muzzle, rotated to the aim, and launches it along the aim direction.
// FireVolleyRoutine()  — fires stats.volleyShotCount shots stats.volleyShotDelay apart, re-reading aim each shot.
public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [Tooltip("Child transform marking the muzzle. Its distance from the player is used as the spawn radius; it is moved to follow the aim.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Projectile bulletPrefab;

    [Header("Upgrades")]
    [Tooltip("Unlocked via the upgrade tree: each Attack press fires a 3-shot burst instead of continuous fire.")]
    public bool volleyUnlocked = false;

    private PlayerInputReader input;
    private float nextFireTime;
    private float firePointRadius = 0.5f;
    private bool isFiringVolley;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("PlayerShooting requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }

        if (firePoint != null && firePoint.localPosition.magnitude > 0.01f)
            firePointRadius = firePoint.localPosition.magnitude;
    }

    private void Update()
    {
        Vector2 aim = input.AimDirection;

        if (firePoint != null)
            firePoint.position = GetMuzzlePosition(aim);

        if (volleyUnlocked)
        {
            if (!isFiringVolley && input.AttackPressedThisFrame && Time.time >= nextFireTime)
            {
                StartCoroutine(FireVolleyRoutine());
            }
        }
        else if (input.AttackHeld && Time.time >= nextFireTime)
        {
            Fire(aim);
            nextFireTime = Time.time + stats.fireRate;
        }
    }

    /// <summary>World-space muzzle position for the given (normalized) aim direction.</summary>
    public Vector2 GetMuzzlePosition(Vector2 aimDirection)
    {
        return (Vector2)transform.position + aimDirection * firePointRadius;
    }

    private void Fire(Vector2 direction)
    {
        if (bulletPrefab == null) return;

        Vector2 spawn = GetMuzzlePosition(direction);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.bulletSpeed, stats.gunDamage, gameObject);
    }

    private IEnumerator FireVolleyRoutine()
    {
        isFiringVolley = true;

        for (int i = 0; i < stats.volleyShotCount; i++)
        {
            Fire(input.AimDirection); // re-read aim each shot so the burst can be swept across a target
            if (i < stats.volleyShotCount - 1)
                yield return new WaitForSeconds(stats.volleyShotDelay);
        }

        nextFireTime = Time.time + stats.fireRate;
        isFiringVolley = false;
    }
}