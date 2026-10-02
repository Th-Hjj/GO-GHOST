using System.Collections;
using UnityEngine;

/// <summary>
/// Fires Projectiles along the sole resolved aim output (PlayerInputReader.AimDirection).
/// The muzzle is the player's position offset along the aim by the FirePoint
/// child's original distance, so aiming is independent of the movement direction.
/// Deployed officers use the weapon in their locked snapshot (LoadoutRules.Weapon):
/// Assault Rifle fires while Attack is held, Sniper and Shotgun once per press. Each trigger pull
/// spends one round from a finite magazine; an empty magazine reloads by itself (unlimited reserve,
/// no manual reload, no cancel). Nothing fires while reloading.
/// Without a deployment the legacy behaviour remains: hold Attack to fire at stats.fireRate.
/// LEGACY / TEST ONLY (volleyUnlocked): each Attack press fires a burst of
/// stats.volleyShotCount shots, stats.volleyShotDelay apart. Not a formal augmentation.
/// </summary>
// FUNCTION SUMMARY
// Update()            — keeps the FirePoint child on the muzzle; runs reload and the weapon trigger (or the legacy fire paths).
// GetMuzzlePosition() — world-space spawn point for a given aim direction.
// FireWeapon()        — one trigger pull: spends one round and launches the weapon's pellet(s) in a fixed fan.
// Fire()              — legacy single bullet without a deployment.
// FireVolleyRoutine()  — fires stats.volleyShotCount shots stats.volleyShotDelay apart, re-reading aim each shot.
public class PlayerShooting : MonoBehaviour, IAttemptAugmentation
{
    [SerializeField] private PlayerStatsSO stats;
    [Tooltip("Child transform marking the muzzle. Its distance from the player is used as the spawn radius; it is moved to follow the aim.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Projectile bulletPrefab;

    // LEGACY / TEST ONLY: formal augmentations come from DeploymentSnapshot, never this field.
    [Header("Legacy (test only)")]
    [Tooltip("LEGACY / TEST ONLY. Not a formal augmentation: each Attack press fires a 3-shot burst instead of continuous fire.")]
    public bool volleyUnlocked = false;

    private PlayerInputReader input;
    private OfficerIdentity officer;
    private SfxSet sfx;
    private AttemptPresentationVfx vfx;
    private bool HasDeployment => officer != null && officer.Deployment != null;
    public int BulletDamage => HasDeployment ? officer.Deployment.BulletDamage : stats.gunDamage;
    public float FireInterval => HasDeployment ? officer.Deployment.FireInterval : stats.fireRate;
    /// <summary>Player gun projectiles launched / that hit a living enemy. A Shotgun pull adds one per pellet.</summary>
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    /// <summary>Successful trigger pulls: one round spent, one shot sound and one muzzle flash each.</summary>
    public int TriggerPulls { get; private set; }
    public int MagazineSize => HasDeployment ? officer.Deployment.Weapon.MagazineSize : 0;
    public int AmmoInMagazine { get; private set; }
    public bool IsReloading { get; private set; }
    internal void RegisterHit() => ShotsHit++;
    /// <summary>Called when the real combat starts so earlier practice shots never count.</summary>
    public void ResetShotCounters() { ShotsFired = 0; ShotsHit = 0; TriggerPulls = 0; }
    private float nextFireTime;
    private float reloadDoneAt;
    private float firePointRadius = 0.5f;
    private bool isFiringVolley;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        officer = GetComponent<OfficerIdentity>();
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
        if (input == null)
        {
            Debug.LogError("PlayerShooting requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }

        if (firePoint != null && firePoint.localPosition.magnitude > 0.01f)
            firePointRadius = firePoint.localPosition.magnitude;
    }

    // Full magazine, no reload in progress. Also re-applied once at the practice -> combat door.
    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        IsReloading = false;
        AmmoInMagazine = snapshot.Weapon.MagazineSize;
        nextFireTime = 0f;
    }

    public void CleanupAttempt() => IsReloading = false;

    private void Update()
    {
        Vector2 aim = input.AimDirection;

        if (firePoint != null)
        {
            firePoint.position = GetMuzzlePosition(aim);
            firePoint.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        if (HasDeployment)
        {
            var weapon = officer.Deployment.Weapon;
            if (IsReloading && Time.time >= reloadDoneAt)
            {
                IsReloading = false;
                AmmoInMagazine = weapon.MagazineSize;
            }
            // Held Attack resumes an automatic weapon after the reload; Sniper and Shotgun need a new press.
            bool trigger = weapon.Automatic ? input.AttackHeld : input.AttackPressedThisFrame;
            if (trigger && !IsReloading && AmmoInMagazine > 0 && Time.time >= nextFireTime && FireWeapon(aim, weapon))
                nextFireTime = Time.time + FireInterval;
            // Automatic reload the moment the magazine is empty; there is no manual reload or cancel.
            if (!IsReloading && AmmoInMagazine <= 0)
            {
                IsReloading = true;
                reloadDoneAt = Time.time + weapon.ReloadSeconds;
            }
            return;
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

    // One trigger pull = one round, one shot sound and one muzzle flash, however many pellets leave the barrel.
    private bool FireWeapon(Vector2 direction, WeaponProfile weapon)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || bulletPrefab == null || !officer.Deployment.CanUseWeapon) return false;

        AmmoInMagazine--;
        Vector2 spawn = GetMuzzlePosition(direction);
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < weapon.Pellets; i++)
        {
            // Fixed, evenly spaced fan; no random spread.
            float angle = weapon.Pellets > 1
                ? facing + Mathf.Lerp(-weapon.SpreadDegrees * .5f, weapon.SpreadDegrees * .5f, i / (float)(weapon.Pellets - 1))
                : facing;
            var pelletDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
            bullet.Launch(pelletDirection, stats.bulletSpeed, weapon.Damage, gameObject, accuracyOwner: this);
            ShotsFired++;
        }
        TriggerPulls++;
        if (sfx != null) sfx.Play(Sfx.Shot);
        // One flash per accepted pull; only the Sniper's visual length differs.
        if (vfx != null) vfx.Muzzle(spawn, direction, officer.Deployment.WeaponId == LoadoutRules.Sniper ? 1.25f : 1f);
        return true;
    }

    private void Fire(Vector2 direction)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || bulletPrefab == null) return;

        Vector2 spawn = GetMuzzlePosition(direction);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.bulletSpeed, BulletDamage, gameObject, accuracyOwner: this);
        if (sfx != null) sfx.Play(Sfx.Shot);
        if (vfx != null) vfx.Muzzle(spawn, direction);
        ShotsFired++;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFiringVolley = false;
        nextFireTime = 0f;
        IsReloading = false;
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
