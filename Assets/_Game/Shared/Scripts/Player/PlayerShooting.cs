using System.Collections;
using UnityEngine;

/// <summary>
/// Formal combat: precise single/continuous fire and a weapon-owned committed Revolver burst.
/// Each bullet spends one round, uses final resolved Aim (plus burst spread) and the actual muzzle.
/// Empty magazines reload automatically with unlimited reserve. Historical deployment/test paths remain.
/// </summary>
// FUNCTION SUMMARY
// Update()            — keeps the FirePoint child on the muzzle; runs reload and the weapon trigger (or the legacy fire paths).
// GetMuzzlePosition() — world-space spawn point for a given aim direction.
// FireWeapon()        — spends one round and launches one physical bullet.
// Fire()              — legacy single bullet without a deployment.
// FireVolleyRoutine()  — fires stats.volleyShotCount shots stats.volleyShotDelay apart, re-reading aim each shot.
public partial class PlayerShooting : MonoBehaviour, IAttemptAugmentation
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
    // Retained API name; formal combat uses the six-round Revolver.
    public static readonly WeaponProfile LegacyRifle = new WeaponProfile(28, .45f, 6, 1.80f, .10f, 14f);
    private WeaponProfile? runtimeRifle;
    private float runtimeBulletSpeed = -1;
    private WeaponProfile Weapon => legacyCombat ? runtimeRifle ?? LegacyRifle : officer.Deployment.Weapon;
    private bool HasDeployment => officer != null && officer.Deployment != null;
    public int BulletDamage => legacyCombat ? Weapon.Damage : HasDeployment ? officer.Deployment.BulletDamage : stats.gunDamage;
    public float FireInterval => legacyCombat ? Weapon.FireInterval : HasDeployment ? officer.Deployment.FireInterval : stats.fireRate;
    public float BulletSpeed => legacyCombat && runtimeBulletSpeed > 0 ? runtimeBulletSpeed : stats.bulletSpeed;
    public float ReloadSeconds => legacyCombat || HasDeployment ? Weapon.ReloadSeconds : 0;
    public float BurstInterval => Weapon.BurstInterval;
    public float BurstSpread => Weapon.BurstSpread;
    public float BulletRadius => bulletPrefab != null ? bulletPrefab.Radius : .125f;
    /// <summary>Player gun projectiles launched / that hit a living enemy.</summary>
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    /// <summary>Successful trigger pulls: one round spent, one shot sound and one muzzle flash each.</summary>
    public int TriggerPulls { get; private set; }
    public int MagazineSize => legacyCombat || HasDeployment ? Weapon.MagazineSize : 0;
    // No free reload or cadence reset. Capacity increases fill on the next normal reload.
    public void SetRuntimeWeapon(WeaponProfile weapon, float speed, bool fresh = false)
    {
        runtimeRifle=weapon; runtimeBulletSpeed=speed;
        // An accepted burst has already committed these real rounds. Clamp only after it exits.
        if (fresh) { CancelBurst(); AmmoInMagazine=weapon.MagazineSize; }
        else if (!IsBursting) AmmoInMagazine=Mathf.Min(AmmoInMagazine,weapon.MagazineSize);
    }
    public int AmmoInMagazine { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsFiring { get; private set; }
    public bool IsBursting { get; private set; }
    public int BurstBudget { get; private set; }
    public int BurstShotsRemaining { get; private set; }
    // Immutable per-shot provenance, including every subsequent bullet of a committed burst.
    public string LastShotActionId { get; private set; }
    public ProjectileActor LastShotActor { get; private set; }
    public float ReloadRemaining => IsReloading ? Mathf.Max(0f, reloadDoneAt - Time.time) : 0f;
    public bool ReloadVisible => legacyCombat && IsReloading && visual != null && visual.ReloadVisible;
    public event System.Action<Vector2, Vector2> ShotFired;
    public event System.Action ReloadStarted;
    public event System.Action ReloadFinished;
    public event System.Action BurstFinished;
    public event System.Action<Enemy,bool> EnemyHit;
    internal void RegisterHit(Enemy target,bool defeated) {ShotsHit++;EnemyHit?.Invoke(target,defeated);}
    public bool CanFireNow => isActiveAndEnabled && !IsReloading && !IsBursting && AmmoInMagazine>0 && Time.time>=nextFireTime
        && input!=null && input.GameplayInputEnabled && !input.NarrativePaused;
    public float NextShotReadyAt => nextFireTime;
    /// <summary>Called when the real combat starts so earlier practice shots never count.</summary>
    public void ResetShotCounters() { ShotsFired = 0; ShotsHit = 0; TriggerPulls = 0; }
    private float nextFireTime;
    private float reloadDoneAt;
    private float firePointRadius = 0.5f;
    private bool isFiringVolley;
    private WeaponProfile burstWeapon;
    private float burstBulletSpeed, nextBurstAt;
    private string burstActionId;
    private ProjectileActor burstActor;
    private WeaponFireRequest lastFireRequest;
    private PlayerHealth health;

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
        health = GetComponent<PlayerHealth>();
        if (health != null) health.OnDeath += CleanupAttempt;
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
        CancelBurst();
        IsFiring = false;
        IsReloading = false;
        AmmoInMagazine = snapshot.Weapon.MagazineSize;
        nextFireTime = 0f;
    }

    public void ConfigureLegacyCombat()
    {
        CancelBurst();
        IsFiring = false;
        legacyCombat = true;
        volleyUnlocked = false;
        StopAllCoroutines();
        isFiringVolley = false;
        IsReloading = false;
        AmmoInMagazine = LegacyRifle.MagazineSize;
        nextFireTime = reloadDoneAt = 0f;
        ResetShotCounters();
        if (visual != null) visual.SetReloading(false);
        ApplyMuzzlePose();
    }

    public void CleanupAttempt()
    {
        CancelBurst();
        IsFiring = false;
        IsReloading = false;
        if (legacyCombat && visual != null) visual.SetReloading(false);
    }

    private void Update()
    {
        if (input == null || input.NarrativePaused || Time.deltaTime <= 0 || (health != null && health.IsDead)) return;
        Vector2 aim = input.AimDirection;
        ApplyMuzzlePose();

        if (legacyCombat)
        {
            UpdateRevolver();
            return;
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
            if (!trigger || IsReloading || !input.GameplayInputEnabled) IsFiring = false;
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
                IsFiring = false;
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

    private void UpdateRevolver()
    {
        if (IsReloading && Time.time >= reloadDoneAt)
        {
            IsReloading = false;
            // Adopt the latest resolved Aim before ammo, callbacks or another shot become available.
            ApplyMuzzlePose();
            AmmoInMagazine = Weapon.MagazineSize;
            if (visual != null) visual.SetReloading(false);
            ReloadFinished?.Invoke();
        }
        if (!input.GameplayInputEnabled) { CancelBurst(); IsFiring=false; return; }
        var request = input.FireRequest;
        if (IsBursting)
        {
            if (Time.time >= nextBurstAt) FireBurstRound();
        }
        else
        {
            if (IsReloading || Time.time >= nextFireTime
                || lastFireRequest == WeaponFireRequest.Continuous && request != WeaponFireRequest.Continuous)
                IsFiring = false;
            if (request != WeaponFireRequest.None && CanFireNow && bulletPrefab != null)
            {
                string action = input.AutomaticFireOwned ? input.ConditionalFrame.FireActionId : null;
                var actor = input.AutomaticFireOwned ? ProjectileActor.InnerAI : ProjectileActor.Player;
                if (request == WeaponFireRequest.Burst)
                {
                    burstWeapon=Weapon; burstBulletSpeed=BulletSpeed;
                    BurstBudget=BurstShotsRemaining=AmmoInMagazine;
                    burstActionId=action; burstActor=actor;
                    nextBurstAt=Time.time; IsBursting=true;
                    FireBurstRound();
                }
                else
                {
                    lastFireRequest=request;
                    nextFireTime=Time.time+FireInterval;
                    LastShotActionId=action;
                    FireWeapon(CurrentBarrelDirection,Weapon,actor);
                }
            }
        }
        if (!IsReloading && !IsBursting && AmmoInMagazine <= 0) BeginReload();
    }

    private void FireBurstRound()
    {
        if (AmmoInMagazine <= 0 || BurstShotsRemaining <= 0) { CancelBurst(); return; }
        Vector2 direction=Quaternion.Euler(0,0,Random.Range(-burstWeapon.BurstSpread,burstWeapon.BurstSpread))*CurrentBarrelDirection;
        LastShotActionId=burstActionId;
        // Set cadence before public callbacks; no callback may observe a free second trigger.
        nextFireTime=Time.time+burstWeapon.FireInterval;
        nextBurstAt+=Mathf.Max(.01f,burstWeapon.BurstInterval);
        lastFireRequest=WeaponFireRequest.Burst;
        // Half-up rounding keeps the integer damage contract (30 * .75 = 22.5 -> 23).
        int burstDamage = Mathf.FloorToInt(burstWeapon.Damage * .75f + .5f);
        if (!FireWeapon(direction,burstWeapon,burstActor,burstBulletSpeed,burstDamage)) { CancelBurst(); return; }
        if (!IsBursting) return; // A lifecycle callback may have canceled the actor.
        BurstShotsRemaining--;
        if (BurstShotsRemaining > 0) return;
        IsBursting=false; IsFiring=false;
        BurstFinished?.Invoke();
        AmmoInMagazine=Mathf.Min(AmmoInMagazine,Weapon.MagazineSize);
    }

    private void CancelBurst()
    {
        if (IsBursting && legacyCombat) AmmoInMagazine=Mathf.Min(AmmoInMagazine,Weapon.MagazineSize);
        IsBursting=false; BurstShotsRemaining=0; burstActionId=null;
        IsFiring=false;
    }

    private void BeginReload()
    {
        IsReloading=true; IsFiring=false;
        reloadStartedAt=Time.time; reloadDuration=Mathf.Max(.01f,Weapon.ReloadSeconds);
        reloadDoneAt=reloadStartedAt+reloadDuration;
        reloadFromDirection=CurrentBarrelDirection;
        reloadSide = Mathf.Abs(reloadFromDirection.x) > .01f ? Mathf.Sign(reloadFromDirection.x)
            : GetComponent<PlayerMovement>().FacingDirection;
        if (visual != null) visual.SetReloading(true);
        if (sfx != null) sfx.Play(Sfx.Equip);
        ReloadStarted?.Invoke();
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
    private bool FireWeapon(Vector2 direction, WeaponProfile weapon, ProjectileActor actor, float speed = -1, int damage = -1)
    {
        if (!isActiveAndEnabled || !input.GameplayInputEnabled || input.NarrativePaused || bulletPrefab == null
            || (!legacyCombat && !officer.Deployment.CanUseWeapon)) return false;

        AmmoInMagazine--;
        Vector2 spawn = legacyCombat ? CurrentMuzzlePosition : GetMuzzlePosition(direction);
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, facing));
        bullet.Launch(direction, speed > 0 ? speed : BulletSpeed, damage >= 0 ? damage : weapon.Damage, gameObject, accuracyOwner: this,
            actor: actor, permissionTier: legacyCombat ? 0 : Mathf.Max(0, officer.Deployment.BrainPermissionTier));
        ShotsFired++;
        TriggerPulls++;
        IsFiring = true;
        LastShotActor = actor;
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

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= CleanupAttempt;
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
