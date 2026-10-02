using UnityEngine;

/// <summary>
/// Simple straight-line bullet. Requires a Rigidbody2D and a Collider2D
/// marked "Is Trigger" on the prefab. Used by the player and by enemies:
/// it never hits its shooter or anything on the shooter's layer (same team).
/// CONFIRMED 2026-10-01: every bullet (player, Boss, Drone) passes through walls and platforms;
/// it ends only on a hostile IDamageable or when its lifetime runs out.
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

    private Rigidbody2D rb;
    private int damage;
    private GameObject owner;
    private PlayerShooting accuracyOwner;
    private int ownerLayer = -1; // cached in Launch: the owner may be destroyed while the bullet is still flying
    private bool spent;
    public bool CanBeCountered { get; private set; }
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

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter, PlayerTimeSlow hostileClock = null, bool counterable = false, PlayerShooting accuracyOwner = null)
    {
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

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            spent = true; // Destroy is deferred; reject all additional contacts this step.
            var counter = CanBeCountered && damageable is PlayerHealth
                ? other.GetComponentInParent<PlayerCounterArm>() : null;
            Vector2 contact = other.ClosestPoint(transform.position);
            if (counter != null && counter.TryCounter())
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
