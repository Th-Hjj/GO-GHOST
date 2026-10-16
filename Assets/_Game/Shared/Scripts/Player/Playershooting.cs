using System.Collections;
using UnityEngine;

/// <summary>
/// Fires Projectiles along the sole resolved aim output (PlayerInputReader.AimDirection).
/// The muzzle is the player's aim origin offset along the aim by the FirePoint
/// child's original distance, so aiming is independent of the movement direction.
/// Deployed officers use the weapon in their locked snapshot (LoadoutRules.Weapon):
/// Assault Rifle fires while Attack is held. Each trigger pull
/// spends one round from a finite magazine; an empty magazine reloads by itself (unlimited reserve,
/// no manual reload, no cancel). Nothing fires while reloading.
/// Without a deployment the legacy behaviour remains: hold Attack to fire at stats.fireRate.
/// LEGACY / TEST ONLY (volleyUnlocked): each Attack press fires a burst of
/// stats.volleyShotCount shots, stats.volleyShotDelay apart. Not a formal augmentation.
/// </summary>
// FUNCTION SUMMARY
// Update()            — keeps the FirePoint child on the muzzle; runs reload and the weapon trigger (or the legacy fire paths).
// GetMuzzlePosition() — world-space spawn point for a given aim direction.
// FireWeapon()        — one trigger pull: spends one round and launches one rifle bullet.
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
    private OfficerVisual visual;
    private bool legacyCombat;
    // The existing Assault Rifle profile, fixed for every new combat actor.
    private static readonly WeaponProfile LegacyRifle = new WeaponProfile(12, .25f, 8, 1.2f);
    private WeaponProfile Weapon => legacyCombat ? LegacyRifle : officer.Deployment.Weapon;
    private bool HasDeployment => officer != null && officer.Deployment != null;
    public int BulletDamage => legacyCombat ? LegacyRifle.Damage : HasDeployment ? officer.Deployment.BulletDamage : stats.gunDamage;
    public float FireInterval => legacyCombat ? LegacyRifle.FireInterval : HasDeployment ? officer.Deployment.FireInterval : stats.fireRate;
    public float BulletSpeed => stats.bulletSpeed;
    public float BulletRadius => bulletPrefab != null ? bulletPrefab.Radius : .125f;
    /// <summary>Player gun projectiles launched / that hit a living enemy.</summary>
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    /// <summary>Successful trigger pulls: one round spent, one shot sound and one muzzle flash each.</summary>
    public int TriggerPulls { get; private set; }
    public int MagazineSize => legacyCombat ? LegacyRifle.MagazineSize : HasDeployment ? officer.Deployment.Weapon.MagazineSize : 0;
    public int AmmoInMagazine { get; private set; }
    public bool IsReloading { get; private set; }
    public float ReloadRemaining => IsReloading ? Mathf.Max(0f, reloadDoneAt - Time.time) : 0f;
    public bool ReloadVisible => legacyCombat && IsReloading && visual != null && visual.ReloadVisible;
    public event System.Action<Vector2, Vector2> ShotFired;
    public event System.Action ReloadStarted;
    public event System.Action ReloadFinished;
    public event System.Action<Enemy,bool> EnemyHit;
    internal void RegisterHit(Enemy target,bool defeated) {ShotsHit++;EnemyHit?.Invoke(target,defeated);}
    public bool CanFireNow => isActiveAndEnabled && !IsReloading && AmmoInMagazine>0 && Time.time>=nextFireTime
        && input!=null && input.GameplayInputEnabled && !input.NarrativePaused;
    /// <summary>Called when the real combat starts so earlier practice shots never count.</summary>
    public void ResetShotCounters() { ShotsFired = 0; ShotsHit = 0; TriggerPulls = 0; }
    private float nextFireTime;
    private float reloadDoneAt;
    private float firePointRadius = 0.5f;
    private bool isFiringVolley;

    // The formal stickman holds its Rifle at chest height, like the gunner Boss.
    // Aim, prediction and projectile spawning must all use this same origin.
    public Vector2 AimOrigin => (Vector2)transform.position + (legacyCombat ? Vector2.up * .28f : Vector2.zero);

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        officer = GetComponent<OfficerIdentity>();
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
        visual = GetComponent<OfficerVisual>();
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

    public void ConfigureLegacyCombat()
    {
        legacyCombat = true;
        volleyUnlocked = false;
        StopAllCoroutines();
        isFiringVolley = false;
        IsReloading = false;
        AmmoInMagazine = LegacyRifle.MagazineSize;
        nextFireTime = reloadDoneAt = 0f;
        ResetShotCounters();
        if (visual != null) visual.SetReloading(false);
    }

    public void CleanupAttempt()
    {
        IsReloading = false;
        if (legacyCombat && visual != null) visual.SetReloading(false);
    }

    private void Update()
    {
        if (input.NarrativePaused) return;
        Vector2 aim = input.AimDirection;

        if (firePoint != null)
        {
            firePoint.position = GetMuzzlePosition(aim);
            firePoint.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        if (legacyCombat || HasDeployment)
        {
            var weapon = Weapon;
            if (IsReloading && Time.time >= reloadDoneAt)
            {
                IsReloading = false;
                AmmoInMagazine = weapon.MagazineSize;
                if (legacyCombat && visual != null) visual.SetReloading(false);
                ReloadFinished?.Invoke();
            }
            // Held Attack resumes the rifle after automatic reload; the permission input gate still owns consent.
            bool autonomous = input.AutomaticFireRequested;
            bool trigger = autonomous || input.AttackHeld;
            var actor = autonomous || (legacyCombat && input.AutomaticFireOwned) ? ProjectileActor.InnerAI : ProjectileActor.Player;
            if (trigger && !IsReloading && AmmoInMagazine > 0 && Time.time >= nextFireTime
                && FireWeapon(aim, weapon, actor))
            {
                nextFireTime = Time.time + FireInterval;
                if (autonomous) input.CompleteAutomaticShot();
            }
            // Automatic reload the moment the magazine is empty; there is no manual reload or cancel.
            if (!IsReloading && AmmoInMagazine <= 0)
            {
                IsReloading = true;
                reloadDoneAt = Time.time + weapon.ReloadSeconds;
                if (legacyCombat && visual != null) visual.SetReloading(true);
                if (legacyCombat && sfx != null) sfx.Play(Sfx.Equip);
                ReloadStarted?.Invoke();
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
        return AimOrigin + aimDirection * firePointRadius;
    }

    // An explicit player choice can request one ordinary shot; failed cadence/reload never creates damage or free ammo.
    public bool RequestPlayerAttackAt(Vector2 point)
    {
        if (legacyCombat || !HasDeployment || IsReloading || AmmoInMagazine <= 0 || Time.time < nextFireTime) return false;
        input.AimPlayerActionAt(point);
        if (!FireWeapon(input.AimDirection, officer.Deployment.Weapon, ProjectileActor.Player)) return false;
        nextFireTime = Time.time + FireInterval;
        return true;
    }

    // One trigger pull = one round, one projectile, one shot sound and one muzzle flash.
    private bool FireWeapon(Vector2 direction, WeaponProfile weapon, ProjectileActor actor)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || input.NarrativePaused || bulletPrefab == null
            || (!legacyCombat && !officer.Deployment.CanUseWeapon)) return false;

        AmmoInMagazine--;
        Vector2 spawn = GetMuzzlePosition(direction);
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, facing));
        bullet.Launch(direction, stats.bulletSpeed, weapon.Damage, gameObject, accuracyOwner: this,
            actor: actor, permissionTier: legacyCombat ? 0 : Mathf.Max(0, officer.Deployment.BrainPermissionTier));
        ShotsFired++;
        TriggerPulls++;
        if (sfx != null) sfx.Play(Sfx.Shot);
        if (vfx != null) vfx.Muzzle(spawn, direction);
        ShotFired?.Invoke(spawn, direction.normalized);
        return true;
    }

    private void Fire(Vector2 direction)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || input.NarrativePaused || bulletPrefab == null) return;

        Vector2 spawn = GetMuzzlePosition(direction);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.bulletSpeed, BulletDamage, gameObject, accuracyOwner: this, actor: ProjectileActor.Player);
        if (sfx != null) sfx.Play(Sfx.Shot);
        if (vfx != null) vfx.Muzzle(spawn, direction);
        ShotsFired++;
        ShotFired?.Invoke(spawn, direction.normalized);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFiringVolley = false;
        nextFireTime = 0f;
        CleanupAttempt();
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
