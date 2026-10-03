using UnityEngine;

/// <summary>Two-phase foundation. Enemy owns health; this is the only movement/attack writer.</summary>
[RequireComponent(typeof(Enemy), typeof(Rigidbody2D))]
public partial class CyberpsychoBoss : MonoBehaviour
{
    public enum BossPhase { Phase1, PhaseTransition, Phase2, Dead }
    // Preserve old values for historical test code; the old four-attack loop is no longer scheduled.
    public enum AttackState { Arrival, ShotWarning, ShotRecovery, ChargeWarning, Charge, ChargeRecovery,
        SpreadWarning, SpreadBurst, SpreadRecovery, ComboWarning, ComboCharge, ComboGap, ComboRecovery, Stopped,
        BurstWarning, BurstFire, BurstRecovery, MarkedTracking, MarkedLock, MarkedRecovery,
        GhostWarning, GhostMove, GhostRecovery, SweepWarning, SweepFire, SweepRecovery,
        SlashWarning, SlashExecute, SlashRecovery,
        CrosscutWarning, CrosscutFirst, CrosscutGap, CrosscutSecond, CrosscutRecovery,
        DropWarning, DropTarget, DropExecute, DropImpact, DropRecovery,
        BlackoutSignal, BlackoutRun, BlackoutGap, BlackoutMark, BlackoutFinalWarning,
        BlackoutFinalSlash, BlackoutBreakDelay, BlackoutRecovery, CamoReposition, BladeComboRecovery, CamoRushWarning, CamoRushExecute, OpeningLaserCharge, OpeningLaserFire }

    [SerializeField] private PlayerHealth player;
    [SerializeField] private Projectile bulletPrefab;
    [SerializeField] private SpriteRenderer shotWarning;
    [SerializeField] private SpriteRenderer chargeWarning;
    [SerializeField] private SpriteRenderer[] spreadWarnings;
    [Header("TEST phase foundation — temporary values, not final balance")]
    [Range(0.01f, 0.99f)]
    [SerializeField] private float phase2HealthThreshold = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float weaponChangeSeconds = 0.35f;
    [Tooltip("Temporary presentation only; these objects have no damage or physics components.")]
    [SerializeField] private Transform weaponRig;
    [SerializeField] private GameObject gunVisual;
    [SerializeField] private GameObject bladeVisual;
    [Header("Arena presentation bounds")]
    [SerializeField] private float arenaHalfWidth = 15f;
    [SerializeField] private float floorY = -2.5f;
    [SerializeField] private float warningRange = 40f;

    [Header("TEST — reused gun/reposition timing, no difficulty scaling")]
    [SerializeField] private float arrivalDelay = 1.2f;
    [SerializeField] private float shotWindup = 0.45f;
    [SerializeField] private float shotRecovery = 0.25f;
    [SerializeField] private float bulletSpeed = 14f;
    [SerializeField] private int bulletDamage = 12;
    [SerializeField] private float bulletSpawnOffset = 0.9f;
    [SerializeField] private float chargeWindup = 0.5f;
    [SerializeField] private float chargeSpeed = 16f;
    // Retired attack values retain their serialized names; no old dash/contact attack is scheduled.
#pragma warning disable CS0414
    [HideInInspector]
    [SerializeField] private float chargeDuration = 0.5f;
    [HideInInspector]
    [SerializeField] private int chargeDamage = 22;
    [SerializeField] private float chargeRecovery = 0.3f;
    [Header("TEST — sweep timing/projectiles")]
    [SerializeField] private float spreadWindup = 0.6f;
    [HideInInspector]
    [SerializeField] private float spreadAngle = 16f;
    [SerializeField] private float burstInterval = 0.22f;
    [SerializeField] private float spreadSpeed = 12f;
    [SerializeField] private int spreadDamage = 10;
    [SerializeField] private float spreadRecovery = 0.35f;
    [HideInInspector]
    [SerializeField] private float comboWindup = 0.45f;
    [HideInInspector]
    [SerializeField] private float comboSpeed = 18f;
    [HideInInspector]
    [SerializeField] private float comboDuration = 0.4f;
    [HideInInspector]
    [SerializeField] private float comboGap = 0.18f;
    [HideInInspector]
    [SerializeField] private float comboRecovery = 0.4f;
#pragma warning restore CS0414

    [Header("MSP gunfighter — temporary values")]
    [SerializeField, Min(1)] private int burstShots = 3;
    [SerializeField] private float markedTrackSeconds = .75f;
    [SerializeField] private float markedLockSeconds = .3f;
    [SerializeField] private float markedBulletSpeed = 24f;
    [SerializeField] private int markedDamage = 24;
    [SerializeField] private float markedRecovery = .45f;
    [SerializeField] private float repositionX = 9f;
    [SerializeField] private float repositionTimeout = 1.5f;
    [SerializeField, Range(.2f, .8f)] private float ghostAlpha = .35f;
    [SerializeField, Min(1)] private int sweepShots = 5;
    [SerializeField] private float highSweepY = 2.1f;

    [Header("MSP Ghost Slash — temporary ground-based blade attack")]
    [SerializeField] private float slashWindup = .55f;
    [SerializeField] private float slashSpeed = 24f;
    [SerializeField] private float slashDuration = .3f;
    [SerializeField] private float slashRecovery = .65f;
    [SerializeField] private int slashDamage = 22;

    [Header("MSP Crosscut / Predator Drop — TEST values")]
    [SerializeField] private float crosscutWindup = .45f;
    [SerializeField] private float crosscutSpeed = 30f;
    [SerializeField] private float crosscutGap = .3f;
    [SerializeField] private float crosscutRecovery = .6f;
    [SerializeField] private int crosscutDamage = 18;
    [SerializeField] private float dropWindup = .55f;
    [SerializeField] private float dropTargetSeconds = .5f;
    [SerializeField] private float dropHeight = 3.5f;
    [SerializeField] private float dropSpeed = 26f;
    [SerializeField] private float dropRecovery = .7f;
    [SerializeField] private int dropDamage = 26;

    [Header("MSP BLACKOUT — once per attempt, TEST values")]
    [SerializeField, Range(.01f, .49f)] private float blackoutHealthThreshold = .35f;
    [SerializeField] private float blackoutSignalSeconds = .7f;
    [SerializeField] private float blackoutSpeed = 36f;
    [SerializeField] private int blackoutDamage = 32;
    [SerializeField] private float blackoutMarkSeconds = .7f;
    [SerializeField] private float blackoutBreakDelay = .5f;

    public AttackState State { get; private set; } = AttackState.Arrival;
    public BossPhase Phase { get; private set; } = BossPhase.Phase1;
    public float Phase2HealthThreshold => phase2HealthThreshold;
    public bool TransitionPending { get; private set; }
    public int PhaseTransitionsStarted { get; private set; }
    public float PhaseTimeRemaining => phaseTimer;
    public bool GunEquipped => gunVisual != null && gunVisual.activeSelf;
    public bool BladeEquipped => bladeVisual != null && bladeVisual.activeSelf;
    public float StateTimeRemaining => timer;
    public int ShotsFired { get; private set; }
    public int SlashesStarted { get; private set; }
    public int CrosscutSlashesStarted { get; private set; }
    public int DropsStarted { get; private set; }
    public bool BlackoutUsed { get; private set; }
    public int BlackoutsStarted { get; private set; }
    public int DistortionRunsStarted { get; private set; }
    public int BladeParries { get; private set; }
    public string PredictionCue { get; private set; } = "";
    public bool PredictionVisible => predictionLine != null && predictionLine.enabled;
    private SpriteRenderer predictionLine;
    private PlayerCounterArm playerCounter;
    private OfficerIdentity playerOfficer;
    public float SimulationRate => timeSlow != null ? timeSlow.WorldRate : 1f;
    public bool IsHighSweep => highSweep;
    public bool IsGhosting => Phase == BossPhase.Phase1 && State == AttackState.GhostMove;
    private bool Sweeping => State == AttackState.SweepWarning || State == AttackState.SweepFire;
    private float SweepY => highSweep ? highSweepY : floorY + .85f;

    public string Cue => Phase switch
    {
        BossPhase.PhaseTransition => "DISCARD GUN / DRAW BLADE",
        BossPhase.Phase2 => State switch
        {
            AttackState.SlashWarning => "GHOST SLASH — EVADE",
            AttackState.SlashExecute => "GHOST SLASH",
            AttackState.SlashRecovery or AttackState.CrosscutRecovery or AttackState.DropRecovery => "RECOVERY — FIRE",
            AttackState.CrosscutWarning => "CROSSCUT — TWO SLASHES",
            AttackState.CrosscutFirst => "CROSSCUT — FIRST",
            AttackState.CrosscutGap => "CROSSCUT — SECOND INCOMING",
            AttackState.CrosscutSecond => "CROSSCUT — SECOND",
            AttackState.DropWarning => "PREDATOR DROP — LOOK UP",
            AttackState.DropTarget => "PREDATOR DROP — MOVE FROM MARK",
            AttackState.DropExecute or AttackState.DropImpact => "PREDATOR DROP",
            AttackState.BlackoutSignal => "BLACKOUT — BLADE SURGE",
            AttackState.BlackoutRun or AttackState.BlackoutGap => "BLACKOUT — DISTORTION",
            AttackState.BlackoutMark => "BLACKOUT — HIGH C MARKED",
            AttackState.BlackoutFinalWarning => "BLACKOUT — FINAL SLASH / EVADE",
            AttackState.BlackoutFinalSlash => "BLACKOUT — FINAL SLASH",
            AttackState.BlackoutBreakDelay => "HIGH C BREAKING — MOVE",
            AttackState.BlackoutRecovery => "HIGH C DESTROYED — FIGHT ON",
            _ => ""
        },
        BossPhase.Dead => "",
        _ => State switch
    {
        AttackState.Arrival => "GET READY",
        AttackState.BurstWarning or AttackState.BurstFire => "BURST FIRE — KEEP MOVING",
        AttackState.MarkedTracking => "MARKED SHOT — TRACKING",
        AttackState.MarkedLock => "MARKED SHOT — LOCKED / EVADE",
        AttackState.GhostWarning or AttackState.GhostMove => "GHOST REPOSITION",
        AttackState.SweepWarning or AttackState.SweepFire => highSweep ? "HIGH SWEEP — CHANGE HEIGHT" : "LOW SWEEP — JUMP / CLIMB",
        AttackState.BurstRecovery or AttackState.MarkedRecovery or AttackState.GhostRecovery or AttackState.SweepRecovery => "RECOVERY — FIRE",
        _ => ""
    }
    };

    private Enemy health;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private float timer;
    private Vector2 shotDirection;
    private float chargeDirection;
    private bool combatEnabled;
    private PlayerTimeSlow timeSlow;
    private int volleyCount;
    private float phaseTimer;
    private bool highSweep;
    private float repositionTarget;
    private SpriteRenderer[] visualParts;
    private Color[] visualColors;
    private Collider2D playerCollider;
    private Quaternion bladeRestRotation;
    private float camouflageClock;
    private bool slashHit;
    private Vector2 previousSlashPosition;
    private Collider2D bossCollider;
    private bool ignoringPlayer;
    private bool playerCollisionWasIgnored;
    private bool scriptedDrop;
    private RigidbodyType2D previousBodyType;
    private float crosscutEndX;
    private Vector2 dropPoint;
    private Vector2 dropLanding;
    private bool CrosscutExecuting => State == AttackState.CrosscutFirst || State == AttackState.CrosscutSecond;
    private bool CrosscutTelling => State == AttackState.CrosscutWarning || State == AttackState.CrosscutGap;
    private GameObject highCPlatform;
    private SfxSet sfx;
    private AttemptPresentationVfx vfx;
    private Bounds highCBounds;
    private bool BlackoutActive => State >= AttackState.BlackoutSignal && State <= AttackState.BlackoutRecovery;
    private bool HighCMarked => State >= AttackState.BlackoutMark && State <= AttackState.BlackoutBreakDelay;
    private bool BlackoutMoving => false; // BLACKOUT now uses locked world-space cuts, never body contact.
    private bool DropTelling => State == AttackState.DropWarning || State == AttackState.DropTarget;

    private void Awake()
    {
        health = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        bossCollider = GetComponent<Collider2D>();
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
        visualParts = GetComponentsInChildren<SpriteRenderer>(true);
        visualColors = new Color[visualParts.Length];
        for (int i = 0; i < visualParts.Length; i++) visualColors[i] = visualParts[i].color;
        timeSlow = player != null ? player.GetComponent<PlayerTimeSlow>() : null;
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        playerCounter = player != null ? player.GetComponent<PlayerCounterArm>() : null;
        playerOfficer = player != null ? player.GetComponent<OfficerIdentity>() : null;
        bladeRestRotation = bladeVisual != null ? bladeVisual.transform.localRotation : Quaternion.identity;
        timer = arrivalDelay;
        SetWeapons(true, false);
        FacePlayer();
    }

    private void OnEnable() => health.OnDied += OnDied;
    private void OnDisable()
    {
        UnbindLegacyShots();
        health.OnDied -= OnDied;
        StopCombat();
    }

    private void Start() => ShowWarnings();

    private void LateUpdate()
    {
        UpdatePrediction();
        // Re-anchor the surface mark after Rigidbody interpolation/repositioning moves its parent.
        if (combatEnabled && (State == AttackState.DropTarget || State == AttackState.DropExecute || State == AttackState.DropImpact || HighCMarked))
            ShowWarnings();
    }

    private void FixedUpdate()
    {
        if (!combatEnabled) return;
        if (health.IsDead || player == null || player.IsDead)
        {
            StopCombat();
            return;
        }

        if (fixedSingleWave && TickLegacyPolice()) return;
        FacePlayer();
        if (!fixedSingleWave && TickOpeningLaser()) return;
        if (Phase == BossPhase.PhaseTransition)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            phaseTimer = Mathf.Max(0f, phaseTimer - Time.fixedDeltaTime * SimulationRate);
            if (phaseTimer <= 0f)
            {
                Phase = BossPhase.Phase2;
                SetWeapons(false, true);
                BeginSlash();
            }
            return;
        }
        if (Phase == BossPhase.Phase2)
        {
            TickSlash();
            return;
        }
        if (Phase != BossPhase.Phase1) return;
        if (State == AttackState.Stopped) return;

        if (!independentEncounter && !TransitionPending && health.CurrentHealth <= health.MaxHealth * phase2HealthThreshold)
            TransitionPending = true;

        if (State == AttackState.MarkedTracking)
        {
            shotDirection = AimAtPlayer();
            ShowWarnings();
        }
        if (State == AttackState.GhostMove && Mathf.Abs(repositionTarget - rb.position.x) < .25f)
            Enter(AttackState.GhostRecovery, chargeRecovery);
        rb.linearVelocity = new Vector2(IsGhosting ? Mathf.Sign(repositionTarget - rb.position.x) * chargeSpeed * SimulationRate : 0f, rb.linearVelocity.y);
        timer -= Time.fixedDeltaTime * SimulationRate;
        if (timer > 0f) return;

        // Finish the current short burst/sweep/reposition, then transition at recovery.
        // Warnings/tracking can end without starting another attack. Still one timer, no coroutine.
        if (TransitionPending && State != AttackState.BurstFire && State != AttackState.SweepFire
            && State != AttackState.GhostMove)
        {
            BeginPhaseTransition();
            return;
        }

        switch (State)
        {
            case AttackState.Arrival:
                BeginBurst();
                break;
            case AttackState.BurstWarning:
            case AttackState.BurstFire:
                Fire(AimAtPlayer(), bulletSpeed, bulletDamage);
                volleyCount++;
                Enter(volleyCount < burstShots ? AttackState.BurstFire : AttackState.BurstRecovery,
                    volleyCount < burstShots ? burstInterval : shotRecovery);
                break;
            case AttackState.BurstRecovery:
                if (fixedSingleWave) { CompleteLegacyAction(); return; }
                shotDirection = AimAtPlayer();
                Enter(AttackState.MarkedTracking, markedTrackSeconds);
                break;
            case AttackState.MarkedTracking:
                Enter(AttackState.MarkedLock, markedLockSeconds);
                break;
            case AttackState.MarkedLock:
                Fire(shotDirection, markedBulletSpeed, markedDamage);
                Enter(AttackState.MarkedRecovery, .75f);
                break;
            case AttackState.MarkedRecovery:
                if (fixedSingleWave) { CompleteLegacyAction(); return; }
                repositionTarget = arenaCenterX + (rb.position.x >= arenaCenterX ? -repositionX : repositionX);
                Enter(AttackState.GhostWarning, chargeWindup);
                break;
            case AttackState.GhostWarning:
                Enter(AttackState.GhostMove, repositionTimeout);
                break;
            case AttackState.GhostMove:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                Enter(AttackState.GhostRecovery, chargeRecovery);
                break;
            case AttackState.GhostRecovery:
                if (fixedSingleWave) { CompleteLegacyAction(); return; }
                highSweep = false;
                BeginSweep();
                break;
            case AttackState.SweepWarning:
            case AttackState.SweepFire:
                // MSP elevated gun position: horizontal bullets clear the platform top, no cover AI.
                FireFrom(new Vector2(rb.position.x + chargeDirection * bulletSpawnOffset, SweepY),
                    Vector2.right * chargeDirection, spreadSpeed, spreadDamage);
                volleyCount++;
                Enter(volleyCount < sweepShots ? AttackState.SweepFire : AttackState.SweepRecovery,
                    volleyCount < sweepShots ? burstInterval : highSweep ? .75f : spreadRecovery);
                break;
            case AttackState.SweepRecovery:
                if (fixedSingleWave) { CompleteLegacyAction(); return; }
                if (!highSweep) { highSweep = true; BeginSweep(); }
                else BeginBurst();
                break;
        }
    }

    private void BeginSlash() { if (fixedSingleWave) LockSlashWarning(); else BeginP2Opening(); }

    private void LockSlashWarning()
    {
        // Lock horizontal direction at telegraph entry; no execution homing or platform navigation.
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        slashHit = false;
        Enter(AttackState.SlashWarning, .55f);
    }

    private void TickSlash()
    {
        camouflageClock += Time.fixedDeltaTime * SimulationRate;
        float dt = Time.fixedDeltaTime * SimulationRate;
        // Interrupt only a completed normal move; never strand a drop or half of Crosscut.
        if (!fixedSingleWave && arenaBound && timer <= dt && !BlackoutUsed && health.CurrentHealth <= health.MaxHealth * blackoutHealthThreshold
            && (State == AttackState.SlashRecovery || State == AttackState.CrosscutRecovery || State == AttackState.DropRecovery || State == AttackState.BladeComboRecovery))
            BeginBlackout();
        if (!fixedSingleWave && BlackoutActive) { TickBlackout(dt); return; }
        if (!fixedSingleWave && CamoRushActive) { TickCamoRush(dt); return; }
        if (State == AttackState.CamoReposition) { TickCamoReposition(dt); return; }
        rb.linearVelocity = new Vector2(State == AttackState.SlashExecute ? chargeDirection * slashSpeed * SimulationRate
            : CrosscutExecuting ? chargeDirection * crosscutSpeed * SimulationRate
            : BlackoutMoving ? chargeDirection * blackoutSpeed * SimulationRate : 0f, scriptedDrop ? 0f : rb.linearVelocity.y);
        if (State == AttackState.DropExecute)
        {
            // Scripted descent to the locked support surface, not platform navigation.
            rb.position = Vector2.MoveTowards(rb.position, dropLanding, dropSpeed * dt);
            if (Vector2.Distance(rb.position, dropLanding) < .01f)
            {
                RestoreDropBody();
                Enter(AttackState.DropImpact, .15f);
                var impact = new Bounds(dropPoint + Vector2.up * .8f, new Vector3(3.6f, 2f, 1f));
                if (playerCollider != null && impact.Intersects(playerCollider.bounds))
                    player.TakeDamage(dropDamage, gameObject);
                if (player.IsDead) { StopCombat(); return; }
            }
        }
        if (State == AttackState.SlashExecute || CrosscutExecuting)
        {
            // A short blade box, swept over the last physics step; one hit attempt per slash.
            Vector2 delta = rb.position - previousSlashPosition;
            var hitbox = new Bounds((rb.position + previousSlashPosition) * .5f + Vector2.right * chargeDirection * .85f,
                new Vector3(1.8f + Mathf.Abs(delta.x), 1.3f + Mathf.Abs(delta.y), 1f));
            if (!slashHit && playerCollider != null && hitbox.Intersects(playerCollider.bounds))
            {
                slashHit = true;
                // Only normal horizontal blade moves are parryable. Drop and BLACKOUT bypass Counter.
                if (State != AttackState.BlackoutFinalSlash && playerCounter != null && playerCounter.TryCounter())
                {
                    BladeParries++;
                    if (vfx != null && playerCollider != null) vfx.CounterSuccess(playerCollider.ClosestPoint(rb.position), Vector2.right * -chargeDirection);
                    bool crosscut = CrosscutExecuting;
                    RestorePlayerCollision();
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    Enter(crosscut ? AttackState.CrosscutRecovery : AttackState.SlashRecovery, .9f);
                    return;
                }
                player.TakeDamage(State == AttackState.BlackoutFinalSlash ? blackoutDamage : CrosscutExecuting ? crosscutDamage : slashDamage, gameObject);
                if (player.IsDead) { StopCombat(); return; }
            }
            previousSlashPosition = rb.position;
        }
        if (CrosscutExecuting && (crosscutEndX - rb.position.x) * chargeDirection <= 0f) timer = 0f;
        timer -= dt;
        if (timer <= 0f)
        {
            switch (State)
            {
                case AttackState.SlashWarning:
                    previousSlashPosition = rb.position;
                    SlashesStarted++;
                    Enter(AttackState.SlashExecute, slashDuration);
                    break;
                case AttackState.SlashExecute:
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    Enter(AttackState.SlashRecovery, slashRecovery);
                    break;
                case AttackState.SlashRecovery:
                    if (fixedSingleWave) { CompleteLegacyAction(); return; }
                    if (playerCollider != null && bossCollider != null && playerCollider.bounds.min.y > bossCollider.bounds.min.y + .8f)
                        Enter(AttackState.DropWarning, dropWindup);
                    else if (Mathf.Abs(player.transform.position.x - rb.position.x) > 3f)
                    {
                        LockCrosscut();
                        Enter(AttackState.CrosscutWarning, crosscutWindup);
                    }
                    else Enter(AttackState.BladeComboRecovery, Mathf.Max(0, .9f - slashRecovery));
                    break;
                case AttackState.BladeComboRecovery:
                    if (fixedSingleWave) { CompleteLegacyAction(); return; }
                    BeginSlash();
                    break;
                case AttackState.CrosscutWarning:
                    StartCrosscut(AttackState.CrosscutFirst);
                    break;
                case AttackState.CrosscutFirst:
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    // Re-sample once after slash 1; slash 2 does not track during execution.
                    LockCrosscut();
                    Enter(AttackState.CrosscutGap, .45f);
                    break;
                case AttackState.CrosscutGap:
                    StartCrosscut(AttackState.CrosscutSecond);
                    break;
                case AttackState.CrosscutSecond:
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    RestorePlayerCollision();
                    Enter(AttackState.CrosscutRecovery, .9f);
                    break;
                case AttackState.CrosscutRecovery:
                    if (fixedSingleWave) { CompleteLegacyAction(); return; }
                    BeginSlash();
                    break;
                case AttackState.DropWarning:
                    BeginDrop();
                    break;
                case AttackState.DropTarget:
                    DropsStarted++;
                    Enter(AttackState.DropExecute, 2f); // bounded timeout, even if the target is unusual
                    break;
                case AttackState.DropExecute:
                    RestoreDropBody();
                    Enter(AttackState.DropRecovery, .9f);
                    break;
                case AttackState.DropImpact:
                    Enter(AttackState.DropRecovery, .9f);
                    break;
                case AttackState.DropRecovery:
                    if (fixedSingleWave) { CompleteLegacyAction(); return; }
                    RestorePlayerCollision();
                    BeginSlash();
                    break;

            }
        }
        ShowWarnings();
    }

    public void BindHighC(GameObject platform) => highCPlatform = platform;

    private void LockCrosscut()
    {
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        // Cross the locked target with a short overshoot so the next slash normally returns.
        crosscutEndX = Mathf.Clamp(player.transform.position.x + chargeDirection * 2f,
            ArenaLeft + .9f, ArenaRight - .9f);
    }

    private void StartCrosscut(AttackState attack)
    {
        IgnorePlayerCollision();
        previousSlashPosition = rb.position;
        slashHit = false;
        CrosscutSlashesStarted++;
        Enter(attack, Mathf.Max(.1f, Mathf.Abs(crosscutEndX - rb.position.x) / Mathf.Max(1f, crosscutSpeed)));
    }

    private void BeginDrop()
    {
        // Mark the surface immediately below the player's feet, including native one-way High C.
        float x = Mathf.Clamp(player.transform.position.x, ArenaLeft + 1f, ArenaRight - 1f);
        float feet = playerCollider != null ? playerCollider.bounds.min.y : player.transform.position.y;
        var ground = Physics2D.Raycast(new Vector2(x, feet + .15f), Vector2.down, 30f, LayerMask.GetMask("Ground"));
        dropPoint = new Vector2(x, ground.collider != null ? ground.point.y : floorY);
        float halfHeight = bossCollider != null ? bossCollider.bounds.extents.y : .75f;
        // Small inward offset avoids ending with solid actor bodies stacked at the same point.
        dropLanding = new Vector2(Mathf.Clamp(x + (x >= arenaCenterX ? -1.3f : 1.3f), ArenaLeft + .9f, ArenaRight - .9f),
            dropPoint.y + halfHeight + .02f);
        previousBodyType = rb.bodyType;
        scriptedDrop = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        IgnorePlayerCollision();
        rb.position = dropLanding + Vector2.up * dropHeight;
        Enter(AttackState.DropTarget, dropTargetSeconds);
    }

    private void RestoreDropBody()
    {
        if (!scriptedDrop) return;
        rb.bodyType = previousBodyType;
        rb.linearVelocity = Vector2.zero;
        scriptedDrop = false;
    }

    private void IgnorePlayerCollision()
    {
        if (ignoringPlayer || bossCollider == null || playerCollider == null) return;
        playerCollisionWasIgnored = Physics2D.GetIgnoreCollision(bossCollider, playerCollider);
        Physics2D.IgnoreCollision(bossCollider, playerCollider, true);
        ignoringPlayer = true;
    }

    private void RestorePlayerCollision()
    {
        if (!ignoringPlayer) return;
        if (bossCollider != null && playerCollider != null)
            Physics2D.IgnoreCollision(bossCollider, playerCollider, playerCollisionWasIgnored);
        ignoringPlayer = false;
    }

    private Vector2 AimAtPlayer()
    {
        Vector2 delta = (Vector2)player.transform.position - rb.position;
        return delta.sqrMagnitude > .001f ? delta.normalized : Vector2.left;
    }

    private void BeginBurst()
    {
        volleyCount = 0;
        shotDirection = AimAtPlayer();
        Enter(AttackState.BurstWarning, shotWindup);
    }

    private void BeginSweep()
    {
        volleyCount = 0;
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        Enter(AttackState.SweepWarning, spreadWindup);
    }

    private void Enter(AttackState next, float duration)
    {
        State = next;
        timer = duration;
        if (!fixedSingleWave && (next == AttackState.OpeningLaserCharge || next == AttackState.BurstWarning || next == AttackState.SweepWarning
            || next == AttackState.MarkedTracking || next == AttackState.SlashWarning || next == AttackState.CrosscutWarning
            || next == AttackState.CrosscutGap || next == AttackState.DropTarget || next == AttackState.CamoRushWarning))
            InnerAIDangerSense.Signal(player, transform, duration + (next == AttackState.MarkedTracking ? markedLockSeconds : 0));
        ShowWarnings();
        FacePlayer();
        // Presentation only, once per actual state entry. Never an attack, damage or timing authority.
        if (vfx != null)
        {
            vfx.SetGhosting(next == AttackState.GhostMove || next == AttackState.CamoRushExecute);
            if (bladeVisual != null && bladeVisual.activeInHierarchy &&
                (next == AttackState.SlashExecute || next == AttackState.CrosscutFirst || next == AttackState.CrosscutSecond
                 || next == AttackState.DropExecute || next == AttackState.CamoRushExecute))
                vfx.BladeSlash(bladeVisual.transform.position,
                    next == AttackState.DropExecute ? Vector2.down : Vector2.right * chargeDirection, .65f);
        }
        if (sfx == null) return;
        // Feedback only, once per actual state entry. Never an attack, damage or timing authority.
        switch (next)
        {
            case AttackState.SlashExecute: case AttackState.CrosscutFirst: case AttackState.CrosscutSecond:
            case AttackState.DropExecute: case AttackState.CamoRushExecute:
                sfx.Play(Sfx.BladeSwing); break;
            case AttackState.OpeningLaserCharge: case AttackState.BurstWarning: case AttackState.SweepWarning: case AttackState.GhostWarning:
            case AttackState.SlashWarning: case AttackState.CrosscutWarning: case AttackState.DropWarning:
            case AttackState.BlackoutSignal: case AttackState.CamoRushWarning:
                sfx.Play(Sfx.Glitch); break;
        }
    }

    private void Fire(Vector2 direction, float speed, int damage)
        => FireFrom(rb.position + direction * bulletSpawnOffset, direction, speed, damage);

    private void FireFrom(Vector2 origin, Vector2 direction, float speed, int damage)
    {
        if (!combatEnabled || Phase != BossPhase.Phase1 || bulletPrefab == null || health.IsDead || player == null || player.IsDead) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        var bullet = Instantiate(bulletPrefab, origin, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, speed, damage, gameObject, timeSlow,
            State == AttackState.BurstWarning || State == AttackState.BurstFire);
        if (sfx != null) sfx.Play(Sfx.Shot);
        if (vfx != null) vfx.Muzzle(origin, direction);
        ShotsFired++;
        var sprite = bullet.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = Color.white;
    }

    private void ShowWarnings()
    {
        // Normal P2: white body/blade cues during concealed movement, full reveal before execution.
        // BLACKOUT alone uses full concealment; it never disables the target or its colliders.
        bool p2Camo = combatEnabled && Phase == BossPhase.Phase2;
        if (visualParts != null)
            for (int i = 0; i < visualParts.Length; i++)
            {
                var part = visualParts[i];
                if (part == null || part == shotWarning || part == chargeWarning
                    || (spreadWarnings != null && System.Array.IndexOf(spreadWarnings, part) >= 0)) continue;
                Color color = visualColors[i];
                if (p2Camo && BlackoutActive)
                {
                    float alpha = blackoutClock < .4f ? 1f : blackoutClock < .7f ? 1f - (blackoutClock - .4f) / .3f
                        : blackoutClock < finalEnd + .5f ? 0f : Mathf.Clamp01((blackoutClock - finalEnd - .5f) / .1f);
                    color = new Color(1f, 1f, 1f, visualColors[i].a * alpha);
                }
                else if (p2Camo)
                {
                    bool blade = bladeVisual != null && part.transform.IsChildOf(bladeVisual.transform);
                    color = new Color(fixedSingleWave ? visualColors[i].r : 1f, fixedSingleWave ? visualColors[i].g : 1f,
                        fixedSingleWave ? visualColors[i].b : 1f, visualColors[i].a * NormalCamoAlpha(blade));
                }
                else color.a *= IsGhosting ? ghostAlpha : 1f;
                part.color = color;
            }
        if (bladeVisual != null)
            bladeVisual.transform.localRotation = bladeRestRotation * Quaternion.Euler(0, 0,
                p2Camo && (State == AttackState.SlashExecute || CrosscutExecuting || State == AttackState.DropExecute || State == AttackState.CamoRushExecute || BlackoutMoving) ? -75f : 0f);
        if (body != null)
            if (health != null && health.IsDead) body.color = new Color(.35f, .35f, .35f);
        if (fixedSingleWave) { ShowLegacyWarnings(); return; }
        // Production combat keeps the real actor windup and attacks, never predicted damage paths.
        if (shotWarning != null) shotWarning.enabled = false;
        if (chargeWarning != null) chargeWarning.enabled = false;
        if (spreadWarnings != null) foreach (var warning in spreadWarnings) if (warning != null) warning.enabled = false;
    }

    private void UpdatePrediction()
    {
        PredictionCue = "";
        if (predictionLine != null) predictionLine.enabled = false;
    }

    private void SetRay(SpriteRenderer ray, Vector2 direction)
    {
        var hit = Physics2D.Raycast(rb.position, direction, warningRange, LayerMask.GetMask("Ground"));
        float length = hit.collider != null ? hit.distance : warningRange;
        ray.transform.SetPositionAndRotation(rb.position + direction * length * .5f,
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        ray.transform.localScale = new Vector3(length / transform.lossyScale.x, .065f / transform.lossyScale.y, 1f);
    }

    private void BeginPhaseTransition()
    {
        if (Phase != BossPhase.Phase1 || health.IsDead) return;
        Phase = BossPhase.PhaseTransition;
        TransitionPending = false;
        PhaseTransitionsStarted++;
        ClearAttack();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetWeapons(false, false); // Discard the gun; existing bullets finish naturally.
        phaseTimer = Mathf.Max(0.01f, weaponChangeSeconds);
    }

    private void ClearAttack()
    {
        InnerAIDangerSense.Cancel(player, transform);
        ClearOpeningLaser();
        ClearCamoRush();
        ClearBlackout();
        ClearCamoPresentation();
        PredictionCue = "";
        if (predictionLine != null) predictionLine.enabled = false;
        RestoreDropBody();
        RestorePlayerCollision();
        crosscutEndX = 0f;
        dropPoint = dropLanding = Vector2.zero;
        shotDirection = Vector2.zero;
        chargeDirection = 0f;
        volleyCount = 0;
        highSweep = false;
        repositionTarget = 0f;
        slashHit = false;
        previousSlashPosition = Vector2.zero;
        camouflageClock = 0f;
        Enter(AttackState.Stopped, 0f);
        if (visualParts != null)
            for (int i = 0; i < visualParts.Length; i++) if (visualParts[i] != null) visualParts[i].color = visualColors[i];
    }

    private void SetWeapons(bool gun, bool blade)
    {
        if (gunVisual != null) gunVisual.SetActive(gun);
        if (bladeVisual != null) bladeVisual.SetActive(blade);
    }

    private void FacePlayer()
    {
        if (weaponRig == null || player == null) return;
        if (fixedSingleWave && Phase == BossPhase.Phase1) { FaceLegacyGun(); return; }
        if (fixedSingleWave) weaponRig.rotation = Quaternion.identity;
        var scale = weaponRig.localScale;
        bool locked = Sweeping || (Phase == BossPhase.Phase2 && chargeDirection != 0f);
        scale.x = Mathf.Abs(scale.x) * (locked ? chargeDirection : player.transform.position.x < transform.position.x ? -1f : 1f);
        weaponRig.localScale = scale;
        weaponRig.position = new Vector3(transform.position.x, Sweeping ? SweepY : transform.position.y, weaponRig.position.z);
    }

    private void OnDied(Enemy _)
    {
        Phase = BossPhase.Dead;
        StopCombat();
    }

    public void BeginCombat()
    {
        if (fixedSingleWave) { BeginLegacyCombat(); return; }
        // A new officer owns a fresh prefab. Restarting the old actor must never rewind P2.
        if (Phase != BossPhase.Phase1 || health.IsDead || player == null || player.IsDead) return;
        combatEnabled = true;
        ClearAttack();
        TransitionPending = false;
        phaseTimer = 0f;
        ShotsFired = 0;
        SlashesStarted = 0;
        BladeParries = 0;
        CrosscutSlashesStarted = 0;
        DropsStarted = 0;
        BlackoutUsed = false;
        BlackoutsStarted = 0;
        CamoRushesStarted = CamoRushHitAttempts = 0;
        DistortionRunsStarted = 0;
        SetWeapons(true, false);
        FacePlayer();
        Enter(AttackState.Arrival, arrivalDelay);
    }

    public void StopCombat()
    {
        combatEnabled = false;
        if (health != null && health.IsDead) Phase = BossPhase.Dead;
        TransitionPending = false;
        phaseTimer = 0f;
        ClearAttack();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetWeapons(false, false);
    }
}
