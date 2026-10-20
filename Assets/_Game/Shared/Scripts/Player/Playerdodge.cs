using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Horizontal dash (ground and air in formal combat), along movement input or the last
/// horizontal movement facing. Mouse aim never selects the dash direction.
/// LEGACY / TEST ONLY (slashDodgeUnlocked): longer dash that damages every enemy it
/// passes through. Not part of the formal GO GHOST loadout architecture.
/// Also exposes PerformForcedSlashDodge(), used exclusively by
/// PlayerHealth's Last Stand ability, which always slashes regardless of
/// the upgrade state.
/// </summary>
// FUNCTION SUMMARY
// TryDodge()                — starts a dodge if one isn't already on cooldown or in progress.
// GetDodgeDirection()        — horizontal input, otherwise the last movement facing.
// DodgeRoutine()             — moves the player through the dash over time, granting i-frames and (if upgraded) damaging enemies passed through.
// PerformForcedSlashDodge()  — the guaranteed damaging slash used only by Last Stand, aimed at the nearest enemy.
// CancelDodge()              — stops any running dash and clears its state (used when the player dies).
// DamageEnemiesAtPoint()     — checks for enemies near a given dash position and damages each one exactly once per dash.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float slashCheckRadius = 0.6f;

    // LEGACY / TEST ONLY: old pre-loadout toggles. Formal augmentations come from DeploymentSnapshot, never these fields.
    [Header("Legacy (test only)")]
    [Tooltip("LEGACY / TEST ONLY. Not a formal augmentation: dodge becomes a longer slash that damages enemies it passes through.")]
    public bool slashDodgeUnlocked = false;
    [Tooltip("LEGACY / TEST ONLY. Air dodge is not enabled in the current side-view slice.")]
    public bool airDodgeUnlocked = false;

    public bool IsDodging { get; private set; }
    private float committedMovementEndsAt;
    public float CommittedHorizontalSpeed { get; private set; }
    public float MovementRemaining => IsDodging ? Mathf.Max(0,committedMovementEndsAt-Time.time) : 0;
    public event System.Action DodgeFinished;
    private float nextDodgeTime;
    // Formal Air Dash (leg upgrade in the DeploymentSnapshot): one horizontal dash per airtime, same cooldown as the
    // ground dash. The base Human stays ground-only; the legacy airDodgeUnlocked field is not a progression path.
    private bool airDashAvailable = true;
    private bool legacyCombat;
    public const float LegacyCooldown = 2f, LegacyImmunity = .2f;
    private float runtimeCooldown = -1, runtimeDistance = -1, runtimeDuration = -1, runtimeImmunity = -1;
    public bool HasAirDash => !legacyCombat && officer != null && officer.Deployment != null && officer.Deployment.HasAirDash;
    public bool AirDashAvailable => HasAirDash && airDashAvailable;
    private OfficerIdentity officer;
    public float DodgeStartInterval => legacyCombat ? (runtimeCooldown >= 0 ? runtimeCooldown : LegacyCooldown) : officer != null && officer.Deployment != null ? officer.Deployment.DodgeStartInterval : stats.dodgeCooldown;
    public float CooldownRemaining => Mathf.Max(0f, nextDodgeTime - Time.time);
    public float LastDodgeStartedAt { get; private set; } = float.NegativeInfinity;
    public float Distance => legacyCombat && runtimeDistance > 0 ? runtimeDistance : stats.dodgeDistance;
    public float Duration => Mathf.Max(.02f, legacyCombat && runtimeDuration > 0 ? runtimeDuration : stats.dodgeDuration);
    public float InvulnerabilityDuration => legacyCombat ? (runtimeImmunity >= 0 ? runtimeImmunity : LegacyImmunity) : stats.iFrameDuration;
    // Existing actions and their cooldown deadline stay committed; edits affect the next acceptance.
    public void SetRuntimeDodge(float distance, float duration, float cooldown, float immunity)
    { runtimeDistance=distance; runtimeDuration=duration; runtimeCooldown=cooldown; runtimeImmunity=immunity; }
    public bool CanDodge => isActiveAndEnabled && CooldownRemaining <= 0 && !IsDodging
        && health != null && !health.IsInvulnerable && input != null && input.GameplayInputEnabled
        && !input.NarrativePaused && movement != null && (legacyCombat || movement.IsGrounded || AirDashAvailable);

    private Rigidbody2D rb;
    private PlayerHealth health;
    private PlayerInputReader input;
    private PlayerMovement movement;
    private Collider2D bodyShape;
    private Collider2D[] passThroughBodies = System.Array.Empty<Collider2D>();
    private readonly List<Collider2D> ignoredBodies = new List<Collider2D>(2);
    private readonly RaycastHit2D[] travelHits = new RaycastHit2D[32];

    // The encounter supplies bodies, keeping Shared independent of level-specific Boss types.
    public void ConfigurePassThroughBodies(Collider2D[] bodies)
    {
        RestoreBodyCollisions(true);
        passThroughBodies = bodies ?? System.Array.Empty<Collider2D>();
        if (IsDodging) IgnoreBodyCollisions();
    }

    public bool CanPassThroughBody(Collider2D other)
        => other != null && System.Array.IndexOf(passThroughBodies, other) >= 0;

    // Automatic direction admission uses the same registered pass-through bodies as the real Dodge.
    // Ordinary movement deliberately does not use this query.
    public float AvailableTravel(float direction, float distance)
    {
        if (bodyShape == null || Mathf.Abs(direction) < .01f || distance <= 0) return 0;
        Vector2 axis = Vector2.right * Mathf.Sign(direction);
        float available = distance;
        var filter = new ContactFilter2D { useTriggers = false };
        int count = bodyShape.Cast(axis, filter, travelHits, distance);
        for (int i = 0; i < count; i++)
        {
            var hit = travelHits[i];
            var other = hit.collider;
            if (other == null || other.isTrigger || other.attachedRigidbody == rb || CanPassThroughBody(other)
                || Physics2D.GetIgnoreCollision(bodyShape, other) || Vector2.Dot(hit.normal, axis) >= -.2f) continue;
            var effector = other.usedByEffector ? other.GetComponent<PlatformEffector2D>() : null;
            if (effector != null && effector.enabled && effector.useOneWay) continue;
            available = Mathf.Min(available, Mathf.Max(0, hit.distance - .015f));
        }
        return available;
    }

    private void IgnoreBodyCollisions()
    {
        if (bodyShape == null) return;
        foreach (var other in passThroughBodies)
            if (other != null && other.enabled && other.gameObject.activeInHierarchy
                && !Physics2D.GetIgnoreCollision(bodyShape, other))
            {
                Physics2D.IgnoreCollision(bodyShape, other, true);
                ignoredBodies.Add(other);
            }
    }

    private void RestoreBodyCollisions(bool force)
    {
        for (int i = ignoredBodies.Count - 1; i >= 0; i--)
        {
            var other = ignoredBodies[i];
            // If movement ends inside a body, restore once separated rather than ejecting either actor.
            // This grants no extra movement or invulnerability.
            if (!force && bodyShape != null && other != null && other.enabled && other.gameObject.activeInHierarchy
                && Physics2D.Distance(bodyShape, other).isOverlapped) continue;
            if (bodyShape != null && other != null) Physics2D.IgnoreCollision(bodyShape, other, false);
            ignoredBodies.RemoveAt(i);
        }
    }

    private void LateUpdate()
    {
        if (!IsDodging && ignoredBodies.Count > 0) RestoreBodyCollisions(false);
    }

    public void ConfigureLegacyCombat()
    {
        legacyCombat = true;
        slashDodgeUnlocked = airDodgeUnlocked = false;
        ResetCooldown();
        LastDodgeStartedAt = float.NegativeInfinity;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyShape = GetComponent<Collider2D>();
        health = GetComponent<PlayerHealth>();
        input = GetComponent<PlayerInputReader>();
        movement = GetComponent<PlayerMovement>();
        officer = GetComponent<OfficerIdentity>();
        if (input == null)
        {
            Debug.LogError("PlayerDodge requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (legacyCombat) return; // Formal Move/Jump/Dodge acceptance is ordered in PlayerMovement.
        if (input.NarrativePaused) return;
        // Landing restores the one air use; it never touches the shared cooldown.
        if (movement != null && movement.IsGrounded) airDashAvailable = true;
        if (input.DodgePressedThisFrame) // "Dodge" action: Left Shift
        {
            TryDodge();
        }
    }

    public void TryDodge()
    {
        TryDodge(0f);
    }

    /// <summary>Zero uses manual facing; Spine supplies an explicit horizontal direction without changing Move.</summary>
    public bool TryDodge(float direction)
    {
        if (!CanDodge) return false;
        if (!legacyCombat && !movement.IsGrounded)
        {
            airDashAvailable = false;
        }
        LastDodgeStartedAt = Time.time;
        nextDodgeTime = Time.time + DodgeStartInterval;
        StartCoroutine(DodgeRoutine(Mathf.Abs(direction) > .01f ? Vector2.right * Mathf.Sign(direction) : GetDodgeDirection()));
        return true;
    }

    /// <summary>Horizontal input or last movement facing. Always left or right.</summary>
    public Vector2 GetDodgeDirection()
    {
        float horizontal = input.MoveInput.x;
        int direction = Mathf.Abs(horizontal) > 0.01f ? (horizontal > 0f ? 1 : -1)
            : (movement != null ? movement.MovementFacingDirection : 1);
        return Vector2.right * direction;
    }

    /// <summary>Stops any running dash (manual or forced) and clears its state. Used by PlayerHealth when the player dies.</summary>
    public void CancelDodge()
    {
        StopAllCoroutines();
        IsDodging = false;
        CommittedHorizontalSpeed = 0;
        RestoreBodyCollisions(true);
        if (health != null) health.IsInvulnerable = false;
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    /// <summary>Ends a running dash and clears the cooldown deadline. Used once when practice hands over to the real combat.</summary>
    public void ResetCooldown()
    {
        CancelDodge();
        nextDodgeTime = 0f;
        airDashAvailable = true;
    }

    private void OnDisable() => CancelDodge();

    private void OnApplicationFocus(bool focused)
    {
        // The three-wave flow freezes the current action on focus loss and resumes its remaining time.
        if (!focused && !legacyCombat) CancelDodge();
    }

    private IEnumerator DodgeRoutine(Vector2 direction)
    {
        IsDodging = true;
        IgnoreBodyCollisions();

        float distance = Distance + (slashDodgeUnlocked ? stats.slashDodgeBonusDistance : 0f);
        float duration = Duration;
        float immunity = InvulnerabilityDuration;
        float speed = distance / duration;
        health.IsInvulnerable = immunity > 0;
        CommittedHorizontalSpeed = direction.x * speed;
        committedMovementEndsAt = Time.time + duration;

        HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
        float elapsed = 0f;

        while (elapsed + 0.0001f < duration)
        {
            // One physics writer during the dash. Gravity and solid collisions stay enabled.
            rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);

            if (slashDodgeUnlocked)
            {
                DamageEnemiesAtPoint(rb.position, hitEnemies);
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            if (elapsed >= immunity) health.IsInvulnerable = false;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        IsDodging = false;
        CommittedHorizontalSpeed = 0;
        RestoreBodyCollisions(false);
        DodgeFinished?.Invoke();

        // Immunity has its own deadline; it need not cover the entire movement.
        float remainingIFrames = immunity - elapsed;
        if (remainingIFrames > 0f)
            yield return new WaitForSeconds(remainingIFrames);

        health.IsInvulnerable = false;
        IsDodging = false;

        // Cooldown deadline was set at dodge START; dash/i-frames do not add another wait.
    }

    /// <summary>
    /// Used exclusively by PlayerHealth's Last Stand ability. Always slashes
    /// through enemies regardless of the slashDodgeUnlocked upgrade, aiming
    /// at the nearest enemy in range (or straight ahead if none is found).
    /// Does not touch the cooldown deadline - it's a separate, forced action.
    /// </summary>
    public IEnumerator PerformForcedSlashDodge()
    {
        IsDodging = true;

        Vector2 start = rb.position;
        float distance = stats.dodgeDistance + stats.slashDodgeBonusDistance;
        Vector2 direction = input.AimDirection;

        // Aim the slash at the nearest enemy in range (any direction in top-down), otherwise along the aim.
        Collider2D[] candidates = Physics2D.OverlapCircleAll(start, distance, enemyLayer);
        float bestSqrDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            Vector2 toEnemy = (Vector2)candidate.transform.position - start;
            if (toEnemy.sqrMagnitude > 0.0001f && toEnemy.sqrMagnitude < bestSqrDistance)
            {
                bestSqrDistance = toEnemy.sqrMagnitude;
                direction = toEnemy.normalized;
            }
        }

        Vector2 end = start + direction * distance;
        HashSet<IDamageable> hitEnemies = new HashSet<IDamageable>();
        float elapsed = 0f;

        while (elapsed < stats.dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stats.dodgeDuration;
            Vector2 newPos = Vector2.Lerp(start, end, t);
            rb.MovePosition(newPos);
            DamageEnemiesAtPoint(newPos, hitEnemies);
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        IsDodging = false;
    }

    private void DamageEnemiesAtPoint(Vector2 point, HashSet<IDamageable> alreadyHit)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, slashCheckRadius, enemyLayer);
        int damage = Mathf.RoundToInt(stats.gunDamage * stats.slashDodgeDamagePercent);

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !alreadyHit.Contains(damageable))
            {
                alreadyHit.Add(damageable);
                damageable.TakeDamage(damage, gameObject);
            }
        }
    }
}
