using UnityEngine;

/// <summary>Two-phase foundation. Enemy owns health; this is the only movement/attack writer.</summary>
[RequireComponent(typeof(Enemy), typeof(Rigidbody2D))]
public class CyberpsychoBoss : MonoBehaviour
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
        BlackoutFinalSlash, BlackoutBreakDelay, BlackoutRecovery }

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
    [SerializeField, Range(.01f, .49f)] private float blackoutHealthThreshold = .25f;
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
    private Bounds highCBounds;
    private bool BlackoutActive => State >= AttackState.BlackoutSignal && State <= AttackState.BlackoutRecovery;
    private bool HighCMarked => State >= AttackState.BlackoutMark && State <= AttackState.BlackoutBreakDelay;
    private bool BlackoutMoving => State == AttackState.BlackoutRun || State == AttackState.BlackoutFinalSlash;
    private bool DropTelling => State == AttackState.DropWarning || State == AttackState.DropTarget;

    private void Awake()
    {
        health = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        bossCollider = GetComponent<Collider2D>();
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

        FacePlayer();
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

        if (!TransitionPending && health.CurrentHealth <= health.MaxHealth * phase2HealthThreshold)
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
                shotDirection = AimAtPlayer();
                Enter(AttackState.MarkedTracking, markedTrackSeconds);
                break;
            case AttackState.MarkedTracking:
                Enter(AttackState.MarkedLock, markedLockSeconds);
                break;
            case AttackState.MarkedLock:
                Fire(shotDirection, markedBulletSpeed, markedDamage);
                Enter(AttackState.MarkedRecovery, markedRecovery);
                break;
            case AttackState.MarkedRecovery:
                repositionTarget = rb.position.x >= 0 ? -repositionX : repositionX;
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
                    volleyCount < sweepShots ? burstInterval : spreadRecovery);
                break;
            case AttackState.SweepRecovery:
                if (!highSweep) { highSweep = true; BeginSweep(); }
                else BeginBurst();
                break;
        }
    }

    private void BeginSlash()
    {
        // Lock horizontal direction at telegraph entry; no execution homing or platform navigation.
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        slashHit = false;
        Enter(AttackState.SlashWarning, slashWindup);
    }

    private void TickSlash()
    {
        camouflageClock += Time.fixedDeltaTime * SimulationRate;
        float dt = Time.fixedDeltaTime * SimulationRate;
        // Interrupt only a completed normal move; never strand a drop or half of Crosscut.
        if (!BlackoutUsed && health.CurrentHealth <= health.MaxHealth * blackoutHealthThreshold
            && (State == AttackState.SlashRecovery || State == AttackState.CrosscutRecovery || State == AttackState.DropRecovery))
            BeginBlackout();
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
        if (State == AttackState.SlashExecute || CrosscutExecuting || State == AttackState.BlackoutFinalSlash)
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
                    LockCrosscut();
                    Enter(AttackState.CrosscutWarning, crosscutWindup);
                    break;
                case AttackState.CrosscutWarning:
                    StartCrosscut(AttackState.CrosscutFirst);
                    break;
                case AttackState.CrosscutFirst:
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    // Re-sample once after slash 1; slash 2 does not track during execution.
                    LockCrosscut();
                    Enter(AttackState.CrosscutGap, crosscutGap);
                    break;
                case AttackState.CrosscutGap:
                    StartCrosscut(AttackState.CrosscutSecond);
                    break;
                case AttackState.CrosscutSecond:
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    RestorePlayerCollision();
                    Enter(AttackState.CrosscutRecovery, crosscutRecovery);
                    break;
                case AttackState.CrosscutRecovery:
                    Enter(AttackState.DropWarning, dropWindup);
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
                    Enter(AttackState.DropRecovery, dropRecovery);
                    break;
                case AttackState.DropImpact:
                    Enter(AttackState.DropRecovery, dropRecovery);
                    break;
                case AttackState.DropRecovery:
                    RestorePlayerCollision();
                    BeginSlash();
                    break;
                case AttackState.BlackoutSignal:
                    BeginBlackoutPass(false);
                    break;
                case AttackState.BlackoutRun:
                    rb.linearVelocity = Vector2.zero;
                    Enter(AttackState.BlackoutGap, .18f);
                    break;
                case AttackState.BlackoutGap:
                    if (DistortionRunsStarted < 2) BeginBlackoutPass(false);
                    else Enter(AttackState.BlackoutMark, blackoutMarkSeconds);
                    break;
                case AttackState.BlackoutMark:
                    BeginBlackoutPass(true);
                    break;
                case AttackState.BlackoutFinalWarning:
                    previousSlashPosition = rb.position;
                    slashHit = false;
                    Enter(AttackState.BlackoutFinalSlash, 2f * (arenaHalfWidth - 1f) / Mathf.Max(1f, blackoutSpeed));
                    break;
                case AttackState.BlackoutFinalSlash:
                    RestoreDropBody();
                    RestorePlayerCollision();
                    Enter(AttackState.BlackoutBreakDelay, blackoutBreakDelay);
                    break;
                case AttackState.BlackoutBreakDelay:
                    // This exact scene platform is attempt-local; no HP, debris or persistence.
                    if (highCPlatform != null) highCPlatform.SetActive(false);
                    Enter(AttackState.BlackoutRecovery, .7f);
                    break;
                case AttackState.BlackoutRecovery:
                    BeginSlash();
                    break;
            }
        }
        ShowWarnings();
    }

    public void BindHighC(GameObject platform) => highCPlatform = platform;

    private void BeginBlackout()
    {
        RestoreDropBody();
        RestorePlayerCollision();
        BlackoutUsed = true;
        BlackoutsStarted++;
        DistortionRunsStarted = 0;
        var platformCollider = highCPlatform != null ? highCPlatform.GetComponent<Collider2D>() : null;
        highCBounds = platformCollider != null ? platformCollider.bounds : new Bounds(Vector3.zero, Vector3.zero);
        rb.linearVelocity = Vector2.zero;
        Enter(AttackState.BlackoutSignal, blackoutSignalSeconds);
    }

    private void BeginBlackoutPass(bool final)
    {
        // Temporary kinematic arena pass at sampled player height, with no navigation/homing.
        if (!scriptedDrop)
        {
            previousBodyType = rb.bodyType;
            scriptedDrop = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        IgnorePlayerCollision();
        float side = final ? (player.transform.position.x >= 0f ? -1f : 1f) : (rb.position.x >= 0f ? 1f : -1f);
        chargeDirection = -side;
        float height = Mathf.Clamp(player.transform.position.y, floorY + .8f, floorY + 7f);
        rb.linearVelocity = Vector2.zero;
        rb.position = new Vector2(side * (arenaHalfWidth - 1f), height);
        previousSlashPosition = rb.position;
        if (final) Enter(AttackState.BlackoutFinalWarning, .4f);
        else
        {
            DistortionRunsStarted++;
            Enter(AttackState.BlackoutRun, 2f * (arenaHalfWidth - 1f) / Mathf.Max(1f, blackoutSpeed));
        }
    }

    private void LockCrosscut()
    {
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        // Cross the locked target with a short overshoot so the next slash normally returns.
        crosscutEndX = Mathf.Clamp(player.transform.position.x + chargeDirection * 2f,
            -arenaHalfWidth + .9f, arenaHalfWidth - .9f);
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
        float x = Mathf.Clamp(player.transform.position.x, -arenaHalfWidth + 1f, arenaHalfWidth - 1f);
        float feet = playerCollider != null ? playerCollider.bounds.min.y : player.transform.position.y;
        var ground = Physics2D.Raycast(new Vector2(x, feet + .15f), Vector2.down, 30f, LayerMask.GetMask("Ground"));
        dropPoint = new Vector2(x, ground.collider != null ? ground.point.y : floorY);
        float halfHeight = bossCollider != null ? bossCollider.bounds.extents.y : .75f;
        // Small inward offset avoids ending with solid actor bodies stacked at the same point.
        dropLanding = new Vector2(Mathf.Clamp(x + (x >= 0f ? -1.3f : 1.3f), -arenaHalfWidth + .9f, arenaHalfWidth - .9f),
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
        ShowWarnings();
        FacePlayer();
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
        ShotsFired++;
        var sprite = bullet.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = State == AttackState.MarkedLock ? Color.yellow : Color.white;
    }

    private void ShowWarnings()
    {
        // P1 has brief, steady transparency. P2 stays translucent with an uneven cyan/pink flicker.
        // Keep the blade and telegraph bright; never change physics transforms for this effect.
        bool p2Camo = combatEnabled && Phase == BossPhase.Phase2;
        float flicker = Mathf.Sin(camouflageClock * 19f) * Mathf.Sin(camouflageClock * 7f);
        bool glitch = Mathf.FloorToInt(camouflageClock * 13f) % 5 == 0;
        if (visualParts != null)
            for (int i = 0; i < visualParts.Length; i++)
            {
                var part = visualParts[i];
                if (part == null || part == shotWarning || part == chargeWarning
                    || (spreadWarnings != null && System.Array.IndexOf(spreadWarnings, part) >= 0)) continue;
                Color color = visualColors[i];
                if (p2Camo)
                {
                    bool blade = bladeVisual != null && part.transform.IsChildOf(bladeVisual.transform);
                    color = Color.Lerp(color, glitch ? new Color(1f, .35f, .8f) : new Color(.2f, .9f, 1f), blade ? .45f : BlackoutActive ? .65f : .2f);
                    color.a = blade ? .95f : BlackoutActive ? .4f + flicker * .1f : .6f + flicker * .12f;
                    if (blade && (State == AttackState.SlashWarning || CrosscutTelling || DropTelling || BlackoutActive)) color = new Color(1f, .45f, .8f, 1f);
                }
                else color.a *= IsGhosting ? ghostAlpha : 1f;
                part.color = color;
            }
        if (bladeVisual != null)
            bladeVisual.transform.localRotation = bladeRestRotation * Quaternion.Euler(0, 0,
                p2Camo && (State == AttackState.SlashExecute || CrosscutExecuting || State == AttackState.DropExecute || BlackoutMoving) ? -75f : 0f);
        if (body != null)
            if (health != null && health.IsDead) body.color = new Color(.35f, .35f, .35f);
        bool marked = State == AttackState.MarkedTracking || State == AttackState.MarkedLock;
        if (shotWarning != null)
        {
            shotWarning.enabled = State == AttackState.BurstWarning || marked;
            shotWarning.color = marked ? new Color(1f, .3f, .15f, .9f) : new Color(1, 1, 1, .7f);
            SetRay(shotWarning, shotDirection);
        }
        if (spreadWarnings != null)
            for (int i = 0; i < spreadWarnings.Length; i++)
            {
                if (spreadWarnings[i] == null) continue;
                spreadWarnings[i].enabled = false;
            }
        if (chargeWarning != null)
        {
            bool slashWarning = State == AttackState.SlashWarning;
            chargeWarning.enabled = Sweeping || slashWarning || CrosscutTelling || State == AttackState.DropTarget
                || State == AttackState.DropExecute || State == AttackState.DropImpact || HighCMarked;
            chargeWarning.color = slashWarning || CrosscutTelling ? new Color(1f, .35f, .8f, .85f)
                : new Color(1f, .65f, .15f, State == AttackState.SweepWarning ? .75f : .25f);
            float end = slashWarning ? Mathf.Clamp(rb.position.x + chargeDirection * slashSpeed * slashDuration,
                -arenaHalfWidth, arenaHalfWidth) : CrosscutTelling ? crosscutEndX : chargeDirection * arenaHalfWidth;
            chargeWarning.transform.position = new Vector3((rb.position.x + end) * .5f, slashWarning || CrosscutTelling ? rb.position.y : SweepY, 0);
            chargeWarning.transform.localScale = new Vector3(Mathf.Max(.1f, Mathf.Abs(end - rb.position.x)) / transform.lossyScale.x, .10f / transform.lossyScale.y, 1);
            if (State == AttackState.DropTarget || State == AttackState.DropExecute || State == AttackState.DropImpact)
            {
                chargeWarning.color = new Color(1f, .65f, .15f, .9f);
                chargeWarning.transform.position = new Vector3(dropPoint.x, dropPoint.y + .08f, 0);
                chargeWarning.transform.localScale = new Vector3(3.6f / transform.lossyScale.x, .16f / transform.lossyScale.y, 1);
            }
            if (HighCMarked && highCPlatform != null)
            {
                chargeWarning.color = new Color(1f, .15f, .5f, .65f + .3f * Mathf.Abs(Mathf.Sin(camouflageClock * 24f)));
                chargeWarning.transform.position = new Vector3(highCBounds.center.x, highCBounds.max.y + .08f, 0);
                chargeWarning.transform.localScale = new Vector3((highCBounds.size.x + .4f) / transform.lossyScale.x, .2f / transform.lossyScale.y, 1);
            }
        }
    }

    private void UpdatePrediction()
    {
        PredictionCue = "";
        if (predictionLine != null) predictionLine.enabled = false;
        if (!combatEnabled || player == null || player.IsDead || playerOfficer == null
            || playerOfficer.Deployment == null || !playerOfficer.Deployment.HasPredictiveCortex) return;

        Vector2 from, to;
        if (Phase == BossPhase.Phase1 && (State == AttackState.BurstRecovery
            || State == AttackState.MarkedTracking || State == AttackState.MarkedLock))
        {
            // An early projection of the existing shot tell, not simulated player prediction.
            var direction = State == AttackState.MarkedLock ? shotDirection : AimAtPlayer();
            var hit = Physics2D.Raycast(rb.position, direction, warningRange, LayerMask.GetMask("Ground"));
            from = rb.position;
            to = from + direction * (hit.collider != null ? hit.distance : warningRange);
            PredictionCue = "PREDICT: MARKED SHOT PATH";
        }
        else if (Phase == BossPhase.Phase2 && (State == AttackState.SlashWarning || CrosscutTelling))
        {
            from = rb.position;
            float end = CrosscutTelling ? crosscutEndX : Mathf.Clamp(from.x + chargeDirection * slashSpeed * slashDuration,
                -arenaHalfWidth, arenaHalfWidth);
            to = new Vector2(end, from.y);
            PredictionCue = CrosscutTelling ? "PREDICT: CROSSCUT PATH" : "PREDICT: GHOST SLASH PATH";
        }
        else if (Phase == BossPhase.Phase2 && (State == AttackState.BlackoutSignal
            || State == AttackState.BlackoutRun || State == AttackState.BlackoutGap) && highCPlatform != null)
        {
            from = new Vector2(highCBounds.min.x, highCBounds.max.y + .28f);
            to = new Vector2(highCBounds.max.x, from.y);
            PredictionCue = "PREDICT: HIGH C WILL BREAK";
        }
        else return;

        if (predictionLine == null)
        {
            var mark = new GameObject("Prediction_TEST");
            mark.transform.SetParent(transform, false);
            predictionLine = mark.AddComponent<SpriteRenderer>();
            predictionLine.sprite = chargeWarning.sprite;
            predictionLine.sharedMaterial = chargeWarning.sharedMaterial;
            predictionLine.sortingLayerID = chargeWarning.sortingLayerID;
            predictionLine.sortingOrder = chargeWarning.sortingOrder + 1;
        }
        // Offset from the ordinary warning so both remain visible; never outline/reveal the Boss.
        var delta = to - from;
        predictionLine.transform.SetPositionAndRotation((from + to) * .5f + Vector2.up * .14f,
            Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        predictionLine.transform.localScale = new Vector3(Mathf.Max(.1f, delta.magnitude) / transform.lossyScale.x,
            .13f / transform.lossyScale.y, 1);
        predictionLine.color = new Color(.2f, 1f, 1f, .55f);
        predictionLine.enabled = true;
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
    }

    private void SetWeapons(bool gun, bool blade)
    {
        if (gunVisual != null) gunVisual.SetActive(gun);
        if (bladeVisual != null) bladeVisual.SetActive(blade);
    }

    private void FacePlayer()
    {
        if (weaponRig == null || player == null) return;
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
