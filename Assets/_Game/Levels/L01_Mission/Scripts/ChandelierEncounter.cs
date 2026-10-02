using UnityEngine;

/// <summary>S01 one-shot physical prop. Only projectile contacts release it; landed body is Ground + explicit hostile bullet cover.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ChandelierEncounter : MonoBehaviour, IProjectileReceiver
{
    [SerializeField] private Collider2D suspension;
    [SerializeField] private BoxCollider2D body;
    [SerializeField] private LayerMask releaseLayers;
    [SerializeField] private LayerMask blockedProjectileLayers;
    public event System.Action<ChandelierEncounter, string> OnWorldOutcome;
    private Rigidbody2D physicsBody;
    private Vector3 initialPosition;
    public Transform AimTarget => suspension != null ? suspension.transform : null;
    public bool IsHanging { get; private set; } = true;
    public bool HasLanded { get; private set; }
    public int OutcomeCount { get; private set; }
    public ProjectileActor LastActor { get; private set; }
    public int LastPermissionTier { get; private set; }
    public int BlockedProjectiles { get; private set; }

    private void Awake()
    {
        physicsBody = GetComponent<Rigidbody2D>();
        initialPosition = transform.position;
        ResetEncounter();
    }

    public void ResetEncounter()
    {
        if (physicsBody == null) return;
        physicsBody.simulated = false;
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        transform.position = initialPosition;
        physicsBody.position = initialPosition;
        physicsBody.rotation = 0f;
        physicsBody.linearVelocity = Vector2.zero;
        physicsBody.angularVelocity = 0f;
        body.enabled = false;
        suspension.gameObject.SetActive(true);
        suspension.enabled = true;
        physicsBody.simulated = true;
        IsHanging = true;
        HasLanded = false;
        OutcomeCount = 0;
        BlockedProjectiles = 0;
        LastActor = ProjectileActor.Environment;
        LastPermissionTier = 0;
    }

    public bool ReceiveProjectile(Projectile projectile, Collider2D contact)
    {
        if (IsHanging && contact == suspension && projectile.Damage > 0 && Includes(releaseLayers, projectile.OwnerLayer))
        {
            // First actual contact wins. Pellets arriving afterwards cannot release or record it again.
            IsHanging = false;
            LastActor = projectile.Actor;
            LastPermissionTier = projectile.PermissionTier;
            suspension.gameObject.SetActive(false);
            body.enabled = true;
            physicsBody.bodyType = RigidbodyType2D.Dynamic;
            physicsBody.WakeUp();
            Debug.Log($"[S01] chandelier released by {LastActor}, permission {LastPermissionTier}", this);
            OnWorldOutcome?.Invoke(this, "released");
            return true;
        }
        if (!IsHanging && contact == body && Includes(blockedProjectileLayers, projectile.OwnerLayer))
        {
            BlockedProjectiles++;
            return true;
        }
        return false;
    }

    private static bool Includes(LayerMask mask, int layer) => layer >= 0 && (mask.value & (1 << layer)) != 0;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsHanging || HasLanded || collision.gameObject.layer != LayerMask.NameToLayer("Ground")) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < .5f) continue;
            HasLanded = true;
            physicsBody.linearVelocity = Vector2.zero;
            physicsBody.angularVelocity = 0f;
            physicsBody.bodyType = RigidbodyType2D.Static;
            OutcomeCount = 1;
            OnWorldOutcome?.Invoke(this, "landed");
            Debug.Log($"[S01 fact] eventId=intervention.chandelier outcomeId=landed actor={LastActor} permissionTier={LastPermissionTier}", this);
            break;
        }
    }
}
