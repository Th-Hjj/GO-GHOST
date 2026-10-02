using UnityEngine;

/// <summary>
/// Simple straight-line bullet. Requires a Rigidbody2D and a Collider2D
/// marked "Is Trigger" on the prefab. Used by the player and by enemies:
/// it never hits its shooter or anything on the shooter's layer (same team).
/// Unmarked walls/platforms pass bullets. Explicit IProjectileReceiver objects may consume a real contact.
/// </summary>
// FUNCTION SUMMARY
// Launch()          — sets the bullet's velocity, damage, and owner (layer cached), and schedules it to self-destruct after its lifetime.
// OnTriggerEnter2D() — damages whatever it hits (if damageable) and destroys the bullet; ignores the shooter, its team, walls and platforms.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [Tooltip("UNUSED since 2026-10-01: bullets pass through walls and platforms. Kept only so the Bullet prefab's serialized value survives.")]
    [SerializeField] private LayerMask blockingLayers;

    /// <summary>Layer of the shooter at launch time (-1 if none). Same-layer objects are never hit.</summary>
    public int OwnerLayer => ownerLayer;
    public int Damage => damage;
    public ProjectileActor Actor { get; private set; }
    public int PermissionTier { get; private set; }

    private Rigidbody2D rb;
    private int damage;
    private GameObject owner;
    private PlayerShooting accuracyOwner;
    private int ownerLayer = -1; // cached in Launch: the owner may be destroyed while the bullet is still flying
    private bool spent;
    public bool CanBeCountered { get; private set; }
    /// <summary>True once an Arm-Blade slash turned this bullet around; it then belongs to the deflecting side.</summary>
    public bool Deflected { get; private set; }
    private PlayerTimeSlow timeSlow;
    private Vector2 fullVelocity;
    private float remainingLife;
    private bool launched;
    private SfxSet sfx;
    private AttemptPresentationVfx vfx;
    public float SimulationRate => timeSlow != null ? timeSlow.WorldRate : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter, PlayerTimeSlow hostileClock = null, bool counterable = false, PlayerShooting accuracyOwner = null,
        ProjectileActor actor = ProjectileActor.Environment, int permissionTier = 0)
    {
        Actor = actor;
        PermissionTier = permissionTier;
        damage = dmg;
        this.accuracyOwner = accuracyOwner;
        CanBeCountered = counterable;
        owner = shooter;
        ownerLayer = shooter != null ? shooter.layer : -1;
        // The actors' container owns transient bullets too; disposing an attempt removes all of them.
        if (shooter != null) transform.SetParent(shooter.transform.parent, true);
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
        timeSlow = hostileClock;
        fullVelocity = direction.normalized * speed;
        rb.linearVelocity = fullVelocity * SimulationRate;
        remainingLife = lifetime;
        launched = true;
    }

    /// <summary>
    /// Arm-Blade deflection: the same bullet flies back the way it came and now belongs to <paramref name="newOwner"/>
    /// (team, damage credit). No aiming, homing or damage bonus; it keeps its remaining lifetime, passes through walls
    /// and is never counted as a gun shot. Returns false if it was already spent or deflected.
    /// </summary>
    public bool Deflect(GameObject newOwner)
    {
        if (!launched || spent || Deflected || newOwner == null) return false;
        Deflected = true;
        Actor = ProjectileActor.Player;
        var identity = newOwner.GetComponent<OfficerIdentity>();
        PermissionTier = identity != null && identity.Deployment != null ? Mathf.Max(0, identity.Deployment.BrainPermissionTier) : 0;
        owner = newOwner;
        ownerLayer = newOwner.layer;
        accuracyOwner = null;
        CanBeCountered = false;
        timeSlow = null; // the hostile clock no longer applies to a bullet that now belongs to the player
        fullVelocity = -fullVelocity;
        rb.linearVelocity = fullVelocity;
        transform.Rotate(0f, 0f, 180f);
        return true;
    }

    // A bullet deflected while already overlapping its old team gets no new Enter event; resolve that contact here.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (Deflected) OnTriggerEnter2D(other);
    }

    private void FixedUpdate()
    {
        if (!launched || spent) return;
        rb.linearVelocity = fullVelocity * SimulationRate;
        remainingLife -= Time.fixedDeltaTime * SimulationRate;
        if (remainingLife <= 0f) Retire();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        // Unity's overloaded == makes this safe even after the owner was destroyed; never dereference owner here.
        if (other.gameObject == owner) return;
        if (other.gameObject.layer == ownerLayer) return; // same team (e.g. enemy bullets pass through other enemies)

        var receiver = other.GetComponentInParent<IProjectileReceiver>();
        if (receiver != null && receiver.ReceiveProjectile(this, other))
        {
            Retire();
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            spent = true; // Destroy is deferred; reject all additional contacts this step.
            var counter = CanBeCountered && damageable is PlayerHealth
                ? other.GetComponentInParent<PlayerCounterArm>() : null;
            Vector2 contact = other.ClosestPoint(transform.position);
            // Legacy Counter Arm cancels a compatible bullet on contact. Arm-Blade never does: it deflects inside its slash.
            if (counter != null && counter.NullifiesProjectiles && counter.TryCounter())
            {
                if (vfx != null) vfx.CounterSuccess(contact, -fullVelocity.normalized);
            }
            else
            {
                if (vfx != null) vfx.BulletHit(contact, -fullVelocity.normalized);
                // Only player-fired bullets opt in; Drone and hostile shots never affect accuracy.
                if (accuracyOwner != null && damage > 0 && damageable is Enemy enemy && !enemy.IsDead)
                    accuracyOwner.RegisterHit();
                // Feedback at a real contact with a living enemy body; timeouts and cleanup stay silent.
                if (sfx != null && damage > 0 && damageable is Enemy struck && !struck.IsDead) sfx.Play(Sfx.BulletHit);
                damageable.TakeDamage(damage, owner);
            }
            Retire();
        }
        // Anything else (walls, platforms, triggers) is passed through.
    }

    public void Retire()
    {
        spent = true;
        if (rb != null) rb.simulated = false;
        var hitbox = GetComponent<Collider2D>();
        if (hitbox != null) hitbox.enabled = false;
        Destroy(gameObject);
    }
}
