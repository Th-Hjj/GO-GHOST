using UnityEngine;

/// <summary>TEST single-phase boss. Enemy owns health; this is the only movement/attack writer.</summary>
[RequireComponent(typeof(Enemy), typeof(Rigidbody2D))]
public class CyberpsychoBoss : MonoBehaviour
{
    public enum AttackState { Arrival, ShotWarning, ShotRecovery, ChargeWarning, Charge, ChargeRecovery,
        SpreadWarning, SpreadBurst, SpreadRecovery, ComboWarning, ComboCharge, ComboGap, ComboRecovery, Stopped }

    [SerializeField] private PlayerHealth player;
    [SerializeField] private Projectile bulletPrefab;
    [SerializeField] private SpriteRenderer shotWarning;
    [SerializeField] private SpriteRenderer chargeWarning;
    [SerializeField] private SpriteRenderer[] spreadWarnings;
    [SerializeField] private GameObject warningMark;
    [Header("Arena presentation bounds")]
    [SerializeField] private float arenaHalfWidth = 15f;
    [SerializeField] private float floorY = -2.5f;
    [SerializeField] private float warningRange = 40f;

    [Header("TEST — no difficulty scaling")]
    [SerializeField] private float arrivalDelay = 1.2f;
    [SerializeField] private float shotWindup = 0.45f;
    [SerializeField] private float shotRecovery = 0.25f;
    [SerializeField] private float bulletSpeed = 14f;
    [SerializeField] private int bulletDamage = 12;
    [SerializeField] private float bulletSpawnOffset = 0.9f;
    [SerializeField] private float chargeWindup = 0.5f;
    [SerializeField] private float chargeSpeed = 16f;
    [SerializeField] private float chargeDuration = 0.5f;
    [SerializeField] private int chargeDamage = 22;
    [SerializeField] private float chargeRecovery = 0.3f;
    [Header("TEST-v2: three fan volleys, then a two-dash combo")]
    [SerializeField] private float spreadWindup = 0.6f;
    [SerializeField] private float spreadAngle = 16f;
    [SerializeField] private float burstInterval = 0.22f;
    [SerializeField] private float spreadSpeed = 12f;
    [SerializeField] private int spreadDamage = 10;
    [SerializeField] private float spreadRecovery = 0.35f;
    [SerializeField] private float comboWindup = 0.45f;
    [SerializeField] private float comboSpeed = 18f;
    [SerializeField] private float comboDuration = 0.4f;
    [SerializeField] private float comboGap = 0.18f;
    [SerializeField] private float comboRecovery = 0.4f;

    public AttackState State { get; private set; } = AttackState.Arrival;
    public float StateTimeRemaining => timer;
    public int ShotsFired { get; private set; }
    public float SimulationRate => timeSlow != null ? timeSlow.WorldRate : 1f;
    private bool Charging => State == AttackState.Charge || State == AttackState.ComboCharge;

    public string Cue => State switch
    {
        AttackState.Arrival => "GET READY",
        AttackState.ShotWarning => "SHOT — MOVE / JUMP / DODGE",
        AttackState.ChargeWarning => "CHARGE — JUMP / EVADE",
        AttackState.Charge => "CHARGE",
        AttackState.SpreadWarning => "FAN BURST — LEAVE THE RAYS",
        AttackState.SpreadBurst => "FAN BURST x3",
        AttackState.ComboWarning => $"DOUBLE DASH {dashIndex}/2 — JUMP / EVADE",
        AttackState.ComboCharge or AttackState.ComboGap => $"DOUBLE DASH {dashIndex}/2",
        AttackState.ShotRecovery or AttackState.ChargeRecovery or AttackState.SpreadRecovery or AttackState.ComboRecovery => "RECOVERY — FIRE",
        _ => ""
    };

    private Enemy health;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private float timer;
    private Vector2 shotDirection;
    private float chargeDirection;
    private bool chargeHit;
    private bool combatEnabled;
    private PlayerTimeSlow timeSlow;
    private int volleyCount;
    private int dashIndex;

    private void Awake()
    {
        health = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        timeSlow = player != null ? player.GetComponent<PlayerTimeSlow>() : null;
        timer = arrivalDelay;
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
        if (!combatEnabled || State == AttackState.Stopped) return;
        if (health.IsDead || player == null || player.IsDead)
        {
            StopCombat();
            return;
        }

        float speed = State == AttackState.ComboCharge ? comboSpeed : chargeSpeed;
        rb.linearVelocity = new Vector2(Charging ? chargeDirection * speed * SimulationRate : 0f, rb.linearVelocity.y);
        timer -= Time.fixedDeltaTime * SimulationRate;
        if (timer > 0f) return;

        switch (State)
        {
            case AttackState.Arrival:
            case AttackState.ComboRecovery:
                // Sample once at the START of the visible warning. No tracking after this.
                Vector2 delta = (Vector2)player.transform.position - rb.position;
                shotDirection = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.left;
                Enter(AttackState.ShotWarning, shotWindup);
                break;
            case AttackState.ShotWarning:
                Fire(shotDirection, bulletSpeed, bulletDamage);
                Enter(AttackState.ShotRecovery, shotRecovery);
                break;
            case AttackState.ShotRecovery:
                chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
                Enter(AttackState.ChargeWarning, chargeWindup);
                break;
            case AttackState.ChargeWarning:
                chargeHit = false;
                Enter(AttackState.Charge, chargeDuration);
                break;
            case AttackState.Charge:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                Enter(AttackState.ChargeRecovery, chargeRecovery);
                break;
            case AttackState.ChargeRecovery:
                Vector2 aim = (Vector2)player.transform.position - rb.position;
                shotDirection = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.left;
                volleyCount = 0;
                Enter(AttackState.SpreadWarning, spreadWindup);
                break;
            case AttackState.SpreadWarning:
            case AttackState.SpreadBurst:
                for (int i = -1; i <= 1; i++) Fire(Rotate(shotDirection, i * spreadAngle), spreadSpeed, spreadDamage);
                volleyCount++;
                Enter(volleyCount < 3 ? AttackState.SpreadBurst : AttackState.SpreadRecovery,
                    volleyCount < 3 ? burstInterval : spreadRecovery);
                break;
            case AttackState.SpreadRecovery:
                dashIndex = 1;
                BeginComboWarning();
                break;
            case AttackState.ComboWarning:
                chargeHit = false;
                Enter(AttackState.ComboCharge, comboDuration);
                break;
            case AttackState.ComboCharge:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                Enter(dashIndex < 2 ? AttackState.ComboGap : AttackState.ComboRecovery,
                    dashIndex < 2 ? comboGap : comboRecovery);
                break;
            case AttackState.ComboGap:
                dashIndex++;
                BeginComboWarning();
                break;
        }
    }

    private void BeginComboWarning()
    {
        // Each dash locks its own direction BEFORE its full warning; never steers mid-dash.
        chargeDirection = player.transform.position.x < rb.position.x ? -1f : 1f;
        Enter(AttackState.ComboWarning, comboWindup);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees) => Quaternion.Euler(0f, 0f, degrees) * direction;

    private void Enter(AttackState next, float duration)
    {
        State = next;
        timer = duration;
        ShowWarnings();
    }

    private void Fire(Vector2 direction, float speed, int damage)
    {
        if (bulletPrefab == null || health.IsDead || player.IsDead) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        var bullet = Instantiate(bulletPrefab, rb.position + direction * bulletSpawnOffset,
            Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, speed, damage, gameObject, timeSlow);
        ShotsFired++;
        var sprite = bullet.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = Color.white;
    }

    private void OnCollisionEnter2D(Collision2D collision) => TryChargeHit(collision);
    private void OnCollisionStay2D(Collision2D collision) => TryChargeHit(collision);

    private void TryChargeHit(Collision2D collision)
    {
        if (!combatEnabled || !Charging || chargeHit || health.IsDead || player == null || player.IsDead) return;
        var victim = collision.collider.GetComponentInParent<PlayerHealth>();
        if (victim != player) return;
        chargeHit = true; // Consume even during dodge i-frames; staying in contact cannot hit again.
        victim.TakeDamage(chargeDamage, gameObject);
    }

    private void ShowWarnings()
    {
        if (body != null)
            body.color = health != null && health.IsDead ? new Color(.35f, .35f, .35f) : Color.white;
        if (warningMark != null) warningMark.SetActive(State == AttackState.ShotWarning || State == AttackState.SpreadWarning
            || State == AttackState.ChargeWarning || State == AttackState.ComboWarning || Charging);
        if (shotWarning != null)
        {
            shotWarning.enabled = State == AttackState.ShotWarning;
            SetRay(shotWarning, shotDirection);
        }
        if (spreadWarnings != null)
            for (int i = 0; i < spreadWarnings.Length; i++)
            {
                if (spreadWarnings[i] == null) continue;
                spreadWarnings[i].enabled = State == AttackState.SpreadWarning;
                SetRay(spreadWarnings[i], Rotate(shotDirection, (i - 1) * spreadAngle));
            }
        if (chargeWarning != null)
        {
            chargeWarning.enabled = State == AttackState.ChargeWarning || State == AttackState.ComboWarning;
            float distance = State == AttackState.ComboWarning ? comboSpeed * comboDuration : chargeSpeed * chargeDuration;
            float halfBody = GetComponent<Collider2D>().bounds.extents.x;
            float end = Mathf.Clamp(rb.position.x + chargeDirection * distance, -arenaHalfWidth + halfBody, arenaHalfWidth - halfBody);
            chargeWarning.transform.position = new Vector3((rb.position.x + end) * 0.5f, floorY + .24f, 0f);
            chargeWarning.transform.localScale = new Vector3(Mathf.Max(0.1f, Mathf.Abs(end - rb.position.x)) / transform.lossyScale.x, 0.14f / transform.lossyScale.y, 1f);
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

    private void OnDied(Enemy _) => StopCombat();

    public void BeginCombat()
    {
        if (health.IsDead || player == null || player.IsDead) return;
        combatEnabled = true;
        chargeHit = false;
        shotDirection = Vector2.zero;
        chargeDirection = 0f;
        volleyCount = dashIndex = 0;
        ShotsFired = 0;
        Enter(AttackState.Arrival, arrivalDelay);
    }

    public void StopCombat()
    {
        combatEnabled = false;
        State = AttackState.Stopped;
        timer = 0f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        ShowWarnings();
    }
}
