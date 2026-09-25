using System.Collections;
using UnityEngine;

/// <summary>
/// Fires a Projectile along the sole resolved aim output (PlayerInputReader.AimDirection).
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
    private OfficerIdentity officer;
    public int BulletDamage => officer != null && officer.Deployment != null ? officer.Deployment.BulletDamage : stats.gunDamage;
    public float FireInterval => officer != null && officer.Deployment != null ? officer.Deployment.FireInterval : stats.fireRate;
    public int ShotsFired { get; private set; }
    private float nextFireTime;
    private float firePointRadius = 0.5f;
    private bool isFiringVolley;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        officer = GetComponent<OfficerIdentity>();
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
        {
            firePoint.position = GetMuzzlePosition(aim);
            firePoint.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

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
            nextFireTime = Time.time + FireInterval;
        }
    }

    /// <summary>World-space muzzle position for the given (normalized) aim direction.</summary>
    public Vector2 GetMuzzlePosition(Vector2 aimDirection)
    {
        return (Vector2)transform.position + aimDirection * firePointRadius;
    }

    private void Fire(Vector2 direction)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || bulletPrefab == null) return;
        if (officer != null && officer.Deployment != null && !officer.Deployment.CanUseWeapon) return;

        Vector2 spawn = GetMuzzlePosition(direction);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.bulletSpeed, BulletDamage, gameObject);
        ShotsFired++;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFiringVolley = false;
        nextFireTime = 0f;
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

        nextFireTime = Time.time + FireInterval;
        isFiringVolley = false;
    }
}
