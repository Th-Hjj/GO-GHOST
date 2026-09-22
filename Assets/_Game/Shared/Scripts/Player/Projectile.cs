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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter)
    {
        damage = dmg;
        owner = shooter;
        ownerLayer = shooter != null ? shooter.layer : -1;
        rb.linearVelocity = direction.normalized * speed; // older Unity: use rb.velocity instead
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Unity's overloaded == makes this safe even after the owner was destroyed; never dereference owner here.
        if (other.gameObject == owner) return;
        if (other.gameObject.layer == ownerLayer) return; // same team (e.g. enemy bullets pass through other enemies)

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, owner);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & blockingLayers.value) != 0)
            Destroy(gameObject); // hit a wall
    }
}