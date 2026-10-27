using UnityEngine;

/// <summary>
/// Side-scroller movement: horizontal velocity from Move input, real
/// gravity-driven jump (Space), snapshot-owned extra jump and bounded descent hover. Facing
/// follows horizontal movement in conditional combat; the weapon aims independently.
/// A short forward/back Move retains its starting facing. Yields control of the
/// Rigidbody2D to PlayerDodge until its movement ends or a legal formal jump interrupts it.
/// Requires the Rigidbody2D's Gravity Scale to be > 0.
/// </summary>
// FUNCTION SUMMARY
// Update()             — reads move/aim/jump input from PlayerInputReader, updates FacingDirection, the sprite flip, and ground state; handles jump presses.
// FixedUpdate()        — applies horizontal velocity each physics step, preserving vertical (gravity/jump) velocity, unless a dodge is currently controlling movement.
// TryJump()             — performs a ground jump, or consumes one available air jump.
// ApplyDeployment/CleanupAttempt — initialize/clear attempt-local leg resources.
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(-50)]
public class PlayerMovement : MonoBehaviour, IAttemptAugmentation
{
    // Replaceable prototype values. Hover limits descent, never changes gravity or adds height.
    public const float HoverDuration = 1.2f;
    public const float HoverFallSpeed = .75f;
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    // LEGACY / TEST ONLY: used only without a formal deployment. Formal Double Jump comes from DeploymentSnapshot.
    [Header("Legacy (test only)")]
    public bool doubleJumpUnlocked = false;

    /// <summary>Visible body facing: 1 = right, -1 = left. Independent of weapon aim.</summary>
    public int FacingDirection { get; private set; } = 1;

    // Movement facing is independent of the mouse; it owns the idle dodge direction.
    public int MovementFacingDirection { get; private set; } = 1;

    /// <summary>Current Move input (length 0..1), for other systems that want the movement direction.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>True on an upward-facing Ground surface or a solid enemy supporting the player. Gates jump and ground dodge.</summary>
    public bool IsGrounded { get; private set; }
    public bool IsOrdinaryMoving { get; private set; }
    public event System.Action JumpStarted;
    public event System.Action DropStarted;
    public event System.Action<string, float> OrdinaryMovementObserved;
    public event System.Action JumpApexReached;
    private bool jumpInFlight, jumpWasAscending, physicsObserved;
    private float previousPhysicsX, ordinaryDirection;
    private string ordinaryActionId;

    private Rigidbody2D rb;
    private readonly ContactPoint2D[] supportContacts = new ContactPoint2D[8];
    private PlayerDodge dodge;
    private PlayerInputReader input;
    private OfficerIdentity officer;
    private bool legacyCombat;
    public const float LegacyMoveSpeed = 7f;
    private float runtimeMoveSpeed = -1, runtimeJumpSpeed = -1;
    public float MoveSpeed => legacyCombat ? (runtimeMoveSpeed >= 0 ? runtimeMoveSpeed : LegacyMoveSpeed) : officer != null && officer.Deployment != null ? officer.Deployment.MoveSpeed : stats.moveSpeed;
    public float JumpSpeed => legacyCombat && runtimeJumpSpeed > 0 ? runtimeJumpSpeed : stats.jumpForce;
    public void SetRuntimeMovement(float speed, float jump)
    { runtimeMoveSpeed = speed; runtimeJumpSpeed = jump; }
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    private Collider2D movementShape;
    private readonly RaycastHit2D[] pushHits = new RaycastHit2D[16];
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private Collider2D groundSupport, droppingThrough;
    private float dropStartedAt;
    public Collider2D DropThroughSupport => droppingThrough;
    public int DropThroughCount { get; private set; }
    private float hitPushSpeed, hitPushRemaining, hitPushUntil, hitPushPreviousX;
    public bool HitPushActive => hitPushRemaining > 0 && Time.time < hitPushUntil;
    public int HitPushCount { get; private set; }
    private int airJumpsRemaining;
    private bool attemptActive;
    private bool HasDeployment => officer != null && officer.Deployment != null;
    public bool HasDoubleJump => !legacyCombat && (HasDeployment ? attemptActive && officer.Deployment.HasDoubleJump : doubleJumpUnlocked);
    public bool HasHover => !legacyCombat && HasDeployment && attemptActive && officer.Deployment.HasHover;
    private int ExtraJumpCapacity => HasDoubleJump ? (HasDeployment ? 1 : stats.maxAirJumps) : 0;
    public int AirJumpsRemaining => airJumpsRemaining;
    public float HoverRemaining { get; private set; }
    public bool IsHovering { get; private set; }

    public void ConfigureLegacyCombat()
    {
        CleanupAttempt();
        legacyCombat = true;
        doubleJumpUnlocked = false;
        attemptActive = true;
        RefillAirResources();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        attemptActive = true;
        RefillAirResources();
    }

    public void CleanupAttempt()
    {
        RestoreDropThrough();
        attemptActive = false;
        airJumpsRemaining = 0;
        HoverRemaining = 0;
        IsHovering = false;
        moveInput = Vector2.zero;
        IsGrounded = false;
        IsOrdinaryMoving = false;
        jumpInFlight = jumpWasAscending = physicsObserved = false;
        ordinaryDirection = 0;
        ordinaryActionId = null;
        hitPushRemaining = 0; HitPushCount = 0;
        groundSupport = null;
        DropThroughCount = 0;
    }

    private void OnDisable() => CleanupAttempt();

    private void RefillAirResources()
    {
        airJumpsRemaining = ExtraJumpCapacity;
        HoverRemaining = HasHover ? HoverDuration : 0;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movementShape = GetComponent<Collider2D>();
        dodge = GetComponent<PlayerDodge>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        input = GetComponent<PlayerInputReader>();
        officer = GetComponent<OfficerIdentity>();
        if (input == null)
        {
            Debug.LogError("PlayerMovement requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (input.NarrativePaused) return;
        UpdateDropThrough();
        // Preserve historical aim-facing outside conditional combat. In Duel, the body faces
        // its movement and the gun keeps its independent aim; short Move reads the prior facing.
        Vector2 aim = input.AimDirection;
        if (!input.ConditionalControlsActive && Mathf.Abs(aim.x) > 0.01f)
            FacingDirection = aim.x > 0 ? 1 : -1;

        // flipX only mirrors the sprite; flipping localScale would also mirror FirePoint and CameraTarget.
        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingDirection < 0;

        IsGrounded = false;
        groundSupport = null;
        if (groundCheck != null && rb.linearVelocity.y <= 0.1f)
        {
            // A downward foot probe avoids treating a wall as a landing surface.
            var groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
            int groundCount = Physics2D.Raycast(groundCheck.position + Vector3.up * groundCheckRadius,
                Vector2.down, groundFilter, groundHits, groundCheckRadius + 0.08f);
            for (int i = 0; i < groundCount; i++)
            {
                var hit = groundHits[i];
                // Queries do not inherit the actor/platform IgnoreCollision pair.
                if (hit.collider == null || hit.collider == droppingThrough || hit.normal.y <= .5f
                    || Physics2D.GetIgnoreCollision(movementShape, hit.collider)) continue;
                IsGrounded = true;
                groundSupport = hit.collider;
                break;
            }
            if (!IsGrounded)
            {
                // Enemy bodies can physically support the officer without belonging to Ground.
                // Require a real upward contact: nearby bodies, side/ceiling contacts, triggers and
                // temporarily ignored Boss collision pairs must not grant a jump or refill air resources.
                int count = rb.GetContacts(supportContacts);
                for (int i = 0; i < count; i++)
                {
                    var contact = supportContacts[i];
                    if (contact.normal.y <= 0.5f || contact.collider == null
                        || contact.collider.GetComponentInParent<Enemy>() == null) continue;
                    IsGrounded = true;
                    break;
                }
            }
        }

        if (IsGrounded)
        {
            RefillAirResources();
            jumpInFlight = jumpWasAscending = false;
        }
        else if (jumpInFlight)
        {
            if (jumpWasAscending && rb.linearVelocity.y <= 0)
            {
                jumpInFlight = false;
                JumpApexReached?.Invoke();
            }
            else if (rb.linearVelocity.y > 0) jumpWasAscending = true;
        }

        input.ResolveConditionalInputs();
        moveInput = input.MoveInput;
        if (Mathf.Abs(moveInput.x) > .01f) MovementFacingDirection = moveInput.x > 0 ? 1 : -1;
        // Do not turn a backward short Move into forward locomotion. Idle keeps the last
        // body orientation, so moving the cursor alone never spins the body back around.
        if(input.ConditionalControlsActive)
        {
            if(Mathf.Abs(moveInput.x)>.01f&&!input.ConditionalFrame.MovementKeepsFacing
                && (dodge==null||!dodge.IsDodging)&&!HitPushActive)
                FacingDirection=MovementFacingDirection;
        }
        else if(Mathf.Abs(input.AimDirection.x)>.01f) FacingDirection=input.AimDirection.x>0?1:-1;
        if(spriteRenderer!=null) spriteRenderer.flipX=FacingDirection<0;
        bool executableJump = input.JumpPressedThisFrame && (IsGrounded || HasDoubleJump && airJumpsRemaining > 0)
            && (input.LegacyCombatActive || dodge == null || !dodge.IsDodging) && input.GameplayInputEnabled;
        if (executableJump && input.LegacyCombatActive && dodge != null && dodge.IsDodging)
        {
            dodge.InterruptForJump();
            if (input.ConditionalFrame.DodgeOwned)
                input.NotifyConditionalAction(ControlOperation.Dodge, false);
        }
        bool acceptedDodge = false;
        if (input.LegacyCombatActive)
        {
            input.ResolveLegacyDodge(executableJump);
            acceptedDodge = !executableJump && input.DodgePressedThisFrame && dodge != null && dodge.TryDodge(input.DodgeDirection);
            if(input.ConditionalFrame.DodgeOwned && input.DodgePressedThisFrame)
                input.NotifyConditionalAction(ControlOperation.Dodge,acceptedDodge);
        }
        if (executableJump && !acceptedDodge)
        {
            TryJump();
        }
        if(input.ConditionalFrame.JumpOwned && input.JumpPressedThisFrame)
            input.NotifyConditionalAction(ControlOperation.Jump,executableJump&&!acceptedDodge);
        // A legal Jump wins over Dodge and a simultaneous S press.
        // S is a press edge, so a held key cannot drop through a second support.
        if (input.DropPressedThisFrame)
        {
            bool acceptedDrop = !acceptedDodge && !executableJump && TryDropThrough();
            if (input.ConditionalFrame.JumpOwned && input.ConditionalFrame.Drop)
                input.NotifyConditionalAction(ControlOperation.Jump, acceptedDrop);
        }
    }

    private void FixedUpdate()
    {
        UpdateDropThrough();
        // Observe the last physics result, not an input or a velocity requested against a wall.
        IsOrdinaryMoving = physicsObserved && ordinaryDirection != 0 && rb.simulated
            && input.GameplayInputEnabled && !input.NarrativePaused
            && (dodge == null || !dodge.IsDodging) && !HitPushActive
            && (rb.position.x - previousPhysicsX) * ordinaryDirection > .0005f;
        if (IsOrdinaryMoving) OrdinaryMovementObserved?.Invoke(ordinaryActionId, Time.fixedDeltaTime);
        previousPhysicsX = rb.position.x;
        physicsObserved = true;
        ordinaryDirection = 0;
        IsHovering = false;
        if (dodge != null && dodge.IsDodging)
        {hitPushRemaining=0;if(input.ConditionalFrame.MovementOwned)input.NotifyConditionalAction(ControlOperation.Movement,false);return;}

        if (HitPushActive)
        {
            if(input.ConditionalFrame.MovementOwned)input.NotifyConditionalAction(ControlOperation.Movement,false);
            float direction = Mathf.Sign(hitPushSpeed);
            hitPushRemaining = Mathf.Max(0, hitPushRemaining - Mathf.Max(0, (rb.position.x-hitPushPreviousX)*direction));
            hitPushPreviousX = rb.position.x;
            float travel = Mathf.Min(hitPushRemaining, Mathf.Abs(hitPushSpeed)*Time.fixedDeltaTime);
            var filter = new ContactFilter2D { useTriggers = false };
            int count = movementShape.Cast(Vector2.right*direction,filter,pushHits,travel+.02f);
            for (int i=0;i<count;i++)
            {
                var hit = pushHits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.attachedRigidbody == rb
                    || Vector2.Dot(hit.normal,Vector2.right*direction) >= -.2f) continue;
                // One-way character platforms do not acquire solid side walls during a hit reaction.
                if (hit.collider.usedByEffector && hit.collider.GetComponent<PlatformEffector2D>() != null) continue;
                travel = Mathf.Min(travel, Mathf.Max(0,hit.distance-.02f));
            }
            rb.linearVelocity = new Vector2(direction*travel/Time.fixedDeltaTime,rb.linearVelocity.y);
            if (travel < .001f) hitPushRemaining = 0;
            return;
        }
        hitPushRemaining = 0;

        float vertical = rb.linearVelocity.y;
        if (HasHover && input.GameplayInputEnabled && input.JumpHeld && !IsGrounded && vertical <= 0 && HoverRemaining > 0)
        {
            IsHovering = true;
            HoverRemaining = Mathf.Max(0, HoverRemaining - Time.fixedDeltaTime);
            // Gravity still integrates normally after this cap. Releasing Space resumes free fall.
            vertical = Mathf.Max(vertical, -HoverFallSpeed);
        }
        rb.linearVelocity = new Vector2(moveInput.x * MoveSpeed, vertical);
        ordinaryDirection = Mathf.Abs(moveInput.x) > .01f ? Mathf.Sign(moveInput.x) : 0;
        ordinaryActionId = input.ConditionalFrame.MovementOwned ? input.ConditionalFrame.MovementActionId : null;
        if(input.ConditionalFrame.MovementOwned)
            input.NotifyConditionalAction(ControlOperation.Movement,input.GameplayInputEnabled&&rb.simulated
                && (Mathf.Abs(input.ConditionalFrame.Move)<.01f || IsOrdinaryMoving));
    }

    public static bool IsDropThroughSupport(Collider2D support)
    {
        if (support == null || !support.enabled || !support.gameObject.activeInHierarchy || !support.usedByEffector) return false;
        var effector = support.GetComponent<PlatformEffector2D>();
        return effector != null && effector.enabled && effector.useOneWay
            && Vector2.Dot(effector.transform.up, Vector2.up) > .999f && Mathf.Abs(effector.rotationalOffset) < .01f;
    }

    public bool CanDropThrough(Collider2D expectedSupport = null)
    {
        return isActiveAndEnabled && IsGrounded && rb != null && rb.simulated && movementShape != null
            && input != null && input.GameplayInputEnabled && !input.NarrativePaused
            && (dodge == null || !dodge.IsDodging) && !HitPushActive && droppingThrough == null
            && IsDropThroughSupport(groundSupport) && (expectedSupport == null || groundSupport == expectedSupport)
            && !Physics2D.GetIgnoreCollision(movementShape, groundSupport);
    }

    // Manual S and an automatic lower-platform transition enter the same real ability.
    // Gravity moves the body; only its current one-way support pair is temporarily ignored.
    public bool TryDropThrough(Collider2D expectedSupport = null)
    {
        if (!CanDropThrough(expectedSupport)) return false;
        droppingThrough = groundSupport;
        dropStartedAt = Time.time;
        Physics2D.IgnoreCollision(movementShape, droppingThrough, true);
        DropThroughCount++;
        groundSupport = null;
        IsGrounded = false;
        jumpInFlight = jumpWasAscending = false;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(0, rb.linearVelocity.y));
        DropStarted?.Invoke();
        return true;
    }

    private void UpdateDropThrough()
    {
        if (droppingThrough == null) return;
        if (movementShape == null || !droppingThrough.enabled || !droppingThrough.gameObject.activeInHierarchy)
        { RestoreDropThrough(); return; }
        var playerBounds = movementShape.bounds;
        var supportBounds = droppingThrough.bounds;
        bool below = playerBounds.max.y < supportBounds.min.y - .01f;
        bool beside = playerBounds.max.x < supportBounds.min.x - .01f || playerBounds.min.x > supportBounds.max.x + .01f;
        // A physical obstruction may prevent descent; this ability must not leave a permanent ignored pair.
        if (below || beside || Time.time - dropStartedAt > 1.5f) RestoreDropThrough();
    }

    private void RestoreDropThrough()
    {
        if (droppingThrough != null && movementShape != null)
            Physics2D.IgnoreCollision(movementShape, droppingThrough, false);
        droppingThrough = null;
        dropStartedAt = 0;
    }

    // A real damaging hit may request one bounded horizontal reaction. Physics and gravity still own position.
    public bool ApplyHitPush(float direction, float distance, float duration)
    {
        var health = GetComponent<PlayerHealth>();
        if (!isActiveAndEnabled || rb == null || movementShape == null || Mathf.Abs(direction)<.01f
            || health == null || health.IsDead || health.IsInvulnerable || dodge != null && dodge.IsDodging) return false;
        distance = Mathf.Clamp(distance,0,2); duration = Mathf.Clamp(duration,.02f,.25f);
        if (distance<=0) return false;
        hitPushSpeed = Mathf.Sign(direction)*distance/duration; hitPushRemaining = distance;
        hitPushUntil = Time.time+duration; hitPushPreviousX = rb.position.x; HitPushCount++;
        return true;
    }

    private void TryJump()
    {
        bool accepted = false;
        if (IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpSpeed);
            IsGrounded = false;
            accepted = true;
        }
        else if (HasDoubleJump && airJumpsRemaining > 0)
        {
            airJumpsRemaining--;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpSpeed);
            accepted = true;
        }
        if (accepted)
        {
            jumpInFlight = true;
            jumpWasAscending = rb.linearVelocity.y > 0;
            JumpStarted?.Invoke();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
