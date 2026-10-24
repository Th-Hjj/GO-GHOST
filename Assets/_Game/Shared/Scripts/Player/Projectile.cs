using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple straight-line bullet. Requires a Rigidbody2D and a Collider2D
/// marked "Is Trigger" on the prefab. Used by the player and by enemies:
/// it never damages its shooter or team, but living allied Boss bodies stop it.
/// Bullets pass through interior terrain; an attempt's ArenaBoundary2D stops them at its perimeter.
/// Penetrating laser attacks have their own ray executor and do not use Projectile.
/// </summary>
// FUNCTION SUMMARY
// Launch()          — sets the bullet's velocity, damage, and owner (layer cached), and schedules it to self-destruct after its lifetime.
// OnTriggerEnter2D() — resolves guards, opposing damage and allied body blocking; ignores the shooter and terrain.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [Tooltip("Legacy terrain layers, now passed through by every projectile. Ground is always included.")]
    [SerializeField] private LayerMask blockingLayers;

    /// <summary>Layer of the shooter at launch time (-1 if none). Same-layer objects never take damage.</summary>
    public int OwnerLayer => ownerLayer;
    public GameObject Owner => owner;
    private static readonly List<Projectile> active = new List<Projectile>();
    private static readonly List<RaycastHit2D> terrainHits = new List<RaycastHit2D>(16);
    public static IReadOnlyList<Projectile> Active => active;
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
    /// <summary>True once a blade turned this bullet around; it then belongs to the deflecting side.</summary>
    public bool Deflected { get; private set; }
    private PlayerTimeSlow timeSlow;
    private Vector2 fullVelocity;
    private float remainingLife;
    private bool launched;
    private ArenaBoundary2D arena;
    private float boundaryRadius;
    private SfxSet sfx;
    private AttemptPresentationVfx vfx;
    public float SimulationRate => timeSlow != null ? timeSlow.WorldRate : 1f;
    public float Radius
    {
        get
        {
            var circle = GetComponent<CircleCollider2D>();
            return circle != null ? circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) : .125f;
        }
    }

    // Geometric terrain query for sight, melee and navigation. Bullets do not use terrain as cover.
    public static bool TerrainHit(Vector2 from, Vector2 to, float radius, out RaycastHit2D blocker)
    {
        Vector2 delta = to - from;
        var filter = new ContactFilter2D().NoFilter();
        filter.SetLayerMask(1 << LayerMask.NameToLayer("Ground"));
        filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.CircleCast(from, radius, delta.normalized, filter, terrainHits, delta.magnitude);
        foreach (var hit in terrainHits)
            if (hit.collider != null && !hit.collider.isTrigger) { blocker = hit; return true; }
        blocker = default;
        return false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() { active.Clear(); terrainHits.Clear(); }
    private void OnDestroy() => active.Remove(this);

    public void Launch(Vector2 direction, float speed, int dmg, GameObject shooter, PlayerTimeSlow hostileClock = null, bool counterable = false, PlayerShooting accuracyOwner = null,
        ProjectileActor actor = ProjectileActor.Environment, int permissionTier = 0)
    {
        Actor = actor;
        if((actor==ProjectileActor.Player||actor==ProjectileActor.InnerAI)&&TryGetComponent<SpriteRenderer>(out var sprite))sprite.color=NeuralPalette.Primary;
        PermissionTier = permissionTier;
        damage = dmg;
        this.accuracyOwner = accuracyOwner;
        CanBeCountered = counterable;
        owner = shooter;
        ownerLayer = shooter != null ? shooter.layer : -1;
        // The actors' container owns transient bullets too; disposing an attempt removes all of them.
        if (shooter != null) transform.SetParent(shooter.transform.parent, true);
        arena = GetComponentInParent<ArenaBoundary2D>();
        boundaryRadius = Radius * 1.2f; // Include the narrow Boss bullet halo.
        if (TryGetComponent<SpriteRenderer>(out var bodySprite))
            boundaryRadius = Mathf.Max(boundaryRadius, ((Vector2)bodySprite.bounds.extents).magnitude);
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
        timeSlow = hostileClock;
        fullVelocity = direction.normalized * speed;
        rb.linearVelocity = fullVelocity * SimulationRate;
        remainingLife = lifetime;
        launched = true;
        if (!active.Contains(this)) active.Add(this);
        if (arena != null && !ArenaBoundary2D.Contains(arena.Bounds, rb.position, boundaryRadius)) Retire();
    }

    /// <summary>
    /// Blade deflection: the same bullet flies back the way it came and now belongs to <paramref name="newOwner"/>
    /// (team, damage credit). No aiming, homing or damage bonus; it keeps its remaining lifetime, passes through walls
    /// and is never counted as a gun shot. Returns false if it was already spent or deflected.
    /// </summary>
    public bool Deflect(GameObject newOwner) => Deflect(newOwner, ProjectileActor.Player);

    public bool Deflect(GameObject newOwner, ProjectileActor actor)
    {
        if (!launched || spent || Deflected || newOwner == null) return false;
        Deflected = true;
        Actor = actor;
        var identity = newOwner.GetComponent<OfficerIdentity>();
        PermissionTier = identity != null && identity.Deployment != null ? Mathf.Max(0, identity.Deployment.BrainPermissionTier) : 0;
        owner = newOwner;
        ownerLayer = newOwner.layer;
        accuracyOwner = null;
        CanBeCountered = false;
        timeSlow = null; // Reflection keeps the original full speed, independently of its old owner's clock.
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
        // Sweep in contact order before the physics step so fast bullets cannot skip actor bodies.
        // Terrain contacts are ignored, including either side of a one-way platform.
        Vector2 travel = rb.linearVelocity * Time.fixedDeltaTime;
        float distance = travel.magnitude;
        bool reachesBoundary = false;
        if (arena != null)
        {
            float edge = ArenaBoundary2D.ExitDistance(arena.Bounds, rb.position, travel.normalized, boundaryRadius);
            reachesBoundary = edge <= distance;
            distance = Mathf.Min(distance, edge);
        }
        foreach (var hit in Physics2D.CircleCastAll(rb.position, Radius, travel.normalized, distance))
        {
            if (hit.collider == null || hit.collider.gameObject == gameObject) continue;
            var ownerBefore = owner;
            OnTriggerEnter2D(hit.collider);
            if (spent) return;
            // A receiver can reverse this bullet; the remaining hits describe its obsolete forward path.
            if (owner != ownerBefore) { reachesBoundary = false; break; }
        }
        if (reachesBoundary) { Retire(); return; }
        remainingLife -= Time.fixedDeltaTime * SimulationRate;
        if (remainingLife <= 0f) Retire();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        // Unity's overloaded == makes this safe even after the owner was destroyed; never dereference owner here.
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;

        // Owner, team and solid body blocking are separate. Same-team weapons/tells are not bodies.
        var bodyHealth = other.GetComponentInParent<Enemy>();
        bool sameTeam = bodyHealth != null ? bodyHealth.gameObject.layer == ownerLayer
            : other.gameObject.layer == ownerLayer;
        if (sameTeam)
        {
            if (!other.isTrigger && bodyHealth != null && !bodyHealth.IsDead
                && other == bodyHealth.GetComponent<Collider2D>())
            {
                if (vfx != null) vfx.BulletHit(other.ClosestPoint(transform.position), -fullVelocity.normalized);
                Retire();
            }
            return;
        }

        if (!other.isTrigger && (((1 << other.gameObject.layer) & blockingLayers.value) != 0
            || other.gameObject.layer == LayerMask.NameToLayer("Ground")))
        {
            // All bullets cross walls/platforms; keep the serialized layer field for existing assets.
            return;
        }

        var receiver = other.GetComponentInParent<IProjectileReceiver>();
        var previousOwner = owner;
        bool consumed = receiver != null && receiver.ReceiveProjectile(this, other);
        if (owner != previousOwner) return; // Reflection transfers the live bullet instead of consuming it.
        if (consumed)
        {
            Retire();
            return;
        }

        // Guard/tell trigger volume is not a hurtbox when it declines this projectile.
        if (other.isTrigger && other.GetComponentInParent<Enemy>() != null
            && other != other.GetComponentInParent<Enemy>().GetComponent<Collider2D>()) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            spent = true; // Destroy is deferred; reject all additional contacts this step.
            var playerInput = other.GetComponentInParent<PlayerInputReader>();
            var counter = CanBeCountered && damageable is PlayerHealth && (playerInput == null || !playerInput.LegacyCombatActive)
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
                var hitEnemy=damageable as Enemy;
                int healthBefore=hitEnemy!=null?hitEnemy.CurrentHealth:0;
                // Feedback at a real contact with a living enemy body; timeouts and cleanup stay silent.
                if (sfx != null && damage > 0 && damageable is Enemy struck && !struck.IsDead) sfx.Play(Sfx.BulletHit);
                damageable.TakeDamage(damage, owner);
                if(accuracyOwner!=null && hitEnemy!=null && hitEnemy.CurrentHealth<healthBefore)
                    accuracyOwner.RegisterHit(hitEnemy,hitEnemy.IsDead);
            }
            Retire();
        }
        // Unrelated triggers are passed through.
    }

    public void Retire()
    {
        spent = true;
        active.Remove(this);
        if (rb != null) rb.simulated = false;
        var hitbox = GetComponent<Collider2D>();
        if (hitbox != null) hitbox.enabled = false;
        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.enabled = false;
        Destroy(gameObject);
    }
}
