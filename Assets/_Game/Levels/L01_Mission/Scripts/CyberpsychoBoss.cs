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
        SlashWarning, SlashExecute, SlashRecovery }

    [SerializeField] private PlayerHealth player;
    [SerializeField] private Projectile bulletPrefab;
    [SerializeField] private SpriteRenderer shotWarning;
    [SerializeField] private SpriteRenderer chargeWarning;
    [SerializeField] private SpriteRenderer[] spreadWarnings;
    [SerializeField] private GameObject warningMark;
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
            AttackState.SlashRecovery => "RECOVERY — FIRE",
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

    private void Awake()
    {
        health = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        visualParts = GetComponentsInChildren<SpriteRenderer>(true);
        visualColors = new Color[visualParts.Length];
        for (int i = 0; i < visualParts.Length; i++) visualColors[i] = visualParts[i].color;
        timeSlow = player != null ? player.GetComponent<PlayerTimeSlow>() : null;
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
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
        rb.linearVelocity = new Vector2(State == AttackState.SlashExecute ? chargeDirection * slashSpeed * SimulationRate : 0f,
            rb.linearVelocity.y);
        if (State == AttackState.SlashExecute)
        {
            // A short blade box, swept over the last physics step; one hit attempt per slash.
            Vector2 delta = rb.position - previousSlashPosition;
            var hitbox = new Bounds((rb.position + previousSlashPosition) * .5f + Vector2.right * chargeDirection * .85f,
                new Vector3(1.8f + Mathf.Abs(delta.x), 1.3f + Mathf.Abs(delta.y), 1f));
            if (!slashHit && playerCollider != null && hitbox.Intersects(playerCollider.bounds))
            {
                slashHit = true;
                player.TakeDamage(slashDamage, gameObject);
                if (player.IsDead) { StopCombat(); return; }
            }
            previousSlashPosition = rb.position;
        }
        timer -= Time.fixedDeltaTime * SimulationRate;
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
                    BeginSlash();
                    break;
            }
        }
        ShowWarnings();
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
        bullet.Launch(direction, speed, damage, gameObject, timeSlow);
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
                    || (spreadWarnings != null && System.Array.IndexOf(spreadWarnings, part) >= 0)
                    || (warningMark != null && part.transform.IsChildOf(warningMark.transform))) continue;
                Color color = visualColors[i];
                if (p2Camo)
                {
                    bool blade = bladeVisual != null && part.transform.IsChildOf(bladeVisual.transform);
                    color = Color.Lerp(color, glitch ? new Color(1f, .35f, .8f) : new Color(.2f, .9f, 1f), blade ? .45f : .2f);
                    color.a = blade ? .95f : .6f + flicker * .12f;
                    if (blade && State == AttackState.SlashWarning) color = new Color(1f, .45f, .8f, 1f);
                }
                else color.a *= IsGhosting ? ghostAlpha : 1f;
                part.color = color;
            }
        if (bladeVisual != null)
            bladeVisual.transform.localRotation = bladeRestRotation * Quaternion.Euler(0, 0,
                p2Camo && State == AttackState.SlashExecute ? -75f : 0f);
        if (body != null)
            if (health != null && health.IsDead) body.color = new Color(.35f, .35f, .35f);
        bool marked = State == AttackState.MarkedTracking || State == AttackState.MarkedLock;
        if (warningMark != null) warningMark.SetActive(State == AttackState.BurstWarning || marked
            || State == AttackState.GhostWarning || State == AttackState.SweepWarning || State == AttackState.SlashWarning);
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
            chargeWarning.enabled = Sweeping || slashWarning;
            chargeWarning.color = slashWarning ? new Color(1f, .35f, .8f, .85f)
                : new Color(1f, .65f, .15f, State == AttackState.SweepWarning ? .75f : .25f);
            float end = slashWarning ? Mathf.Clamp(rb.position.x + chargeDirection * slashSpeed * slashDuration,
                -arenaHalfWidth, arenaHalfWidth) : chargeDirection * arenaHalfWidth;
            chargeWarning.transform.position = new Vector3((rb.position.x + end) * .5f, slashWarning ? rb.position.y : SweepY, 0);
            chargeWarning.transform.localScale = new Vector3(Mathf.Max(.1f, Mathf.Abs(end - rb.position.x)) / transform.lossyScale.x, .10f / transform.lossyScale.y, 1);
        }
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
