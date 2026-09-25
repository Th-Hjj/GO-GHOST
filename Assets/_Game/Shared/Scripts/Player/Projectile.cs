using UnityEngine;

/// <summary>
/// Simple straight-line bullet. Requires a Rigidbody2D and a Collider2D
/// marked "Is Trigger" on the prefab. Used by the player and by enemies:
/// it never hits its shooter or anything on the shooter's layer (same team),
/// and it is destroyed by anything on blockingLayers (walls).
/// </summary>
// FUNCTION SUMMARY
// Launch()          — sets the bullet's velocity, damage, and owner (layer cached), and schedules it to self-destruct after its lifetime.
// OnTriggerEnter2D() — damages whatever it hits (if damageable) and destroys the bullet; ignores the shooter and its team; stops on blocking layers.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [Tooltip("Layers that stop the bullet without taking damage (walls). Set on the Bullet prefab.")]
    [SerializeField] private LayerMask blockingLayers;

    /// <summary>Layer of the shooter at launch time (-1 if none). Same-layer objects are never hit.</summary>
    public int OwnerLayer => ownerLayer;

    private Rigidbody2D rb;
    private int damage;
    private GameObject owner;
    private int ownerLayer = -1; // cached in Launch: the owner may be destroyed while the bullet is still flying
    private bool spent;
    private PlayerTimeSlow timeSlow;
    private Vector2 fullVelocity;
    private float remainingLife;
    private bool launched;
    public float SimulationRate => timeSlow != null ? timeSlow.WorldRate : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter, PlayerTimeSlow hostileClock = null)
    {
        damage = dmg;
        owner = shooter;
        ownerLayer = shooter != null ? shooter.layer : -1;
        // The actors' container owns transient bullets too; disposing an attempt removes all of them.
        if (shooter != null) transform.SetParent(shooter.transform.parent, true);
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
            damageable.TakeDamage(damage, owner);
            Retire();
            return;
        }

        if (((1 << other.gameObject.layer) & blockingLayers.value) != 0)
            Retire();
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
