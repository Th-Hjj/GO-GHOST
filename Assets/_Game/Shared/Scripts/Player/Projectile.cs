using UnityEngine;

/// <summary>
/// Simple straight-line bullet. Requires a Rigidbody2D and a Collider2D
/// marked "Is Trigger" on the prefab.
/// </summary>
// FUNCTION SUMMARY
// Launch()          — sets the bullet's velocity, damage, and owner, and schedules it to self-destruct after its lifetime.
// OnTriggerEnter2D() — damages whatever it hits (if damageable) and destroys the bullet, ignoring its own shooter.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;

    private Rigidbody2D rb;
    private int damage;
    private GameObject owner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter)
    {
        damage = dmg;
        owner = shooter;
        rb.linearVelocity = direction.normalized * speed; // older Unity: use rb.velocity instead
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, owner);
            Destroy(gameObject);
            return;
        }

        // Optional: destroy the bullet on hitting terrain too.
        // if (((1 << other.gameObject.layer) & groundLayer) != 0) Destroy(gameObject);
    }
}