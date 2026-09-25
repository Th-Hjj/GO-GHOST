using UnityEngine;

/// <summary>
/// Side-scroller movement: horizontal velocity from Move input, real
/// gravity-driven jump (Space), snapshot-owned extra jump and bounded descent hover. Facing
/// follows the resolved manual/AI aim direction, independently of movement
/// and horizontal dodge facing. Yields control of the
/// Rigidbody2D to PlayerDodge while a dodge is in progress.
/// Requires the Rigidbody2D's Gravity Scale to be > 0.
/// </summary>
// FUNCTION SUMMARY
// Update()             — reads move/aim/jump input from PlayerInputReader, updates FacingDirection, the sprite flip, and ground state; handles jump presses.
// FixedUpdate()        — applies horizontal velocity each physics step, preserving vertical (gravity/jump) velocity, unless a dodge is currently controlling movement.
// TryJump()             — performs a ground jump, or consumes one available air jump.
// ApplyDeployment/CleanupAttempt — initialize/clear attempt-local leg resources.
// UnlockDoubleJump()   — legacy test-scene hook, never overrides a formal deployment.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour, IAttemptAugmentation
{
    // Replaceable prototype values. Hover limits descent, never changes gravity or adds height.
    public const float HoverDuration = 1.2f;
    public const float HoverFallSpeed = .75f;
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Upgrades")]
    public bool doubleJumpUnlocked = false;

    /// <summary>Visual aim facing: 1 = right, -1 = left. Independent of movement/dodge.</summary>
    public int FacingDirection { get; private set; } = 1;

    // Movement facing is independent of the mouse; it owns the idle dodge direction.
    public int MovementFacingDirection { get; private set; } = 1;

    /// <summary>Current Move input (length 0..1), for other systems that want the movement direction.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>True while the feet find an upward-facing Ground surface. Gates jump and ground dodge.</summary>
    public bool IsGrounded { get; private set; }

    private Rigidbody2D rb;
    private PlayerDodge dodge;
    private PlayerInputReader input;
    private OfficerIdentity officer;
    public float MoveSpeed => officer != null && officer.Deployment != null ? officer.Deployment.MoveSpeed : stats.moveSpeed;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    private int airJumpsRemaining;
    private bool attemptActive;
    private bool HasDeployment => officer != null && officer.Deployment != null;
    public bool HasDoubleJump => HasDeployment ? attemptActive && officer.Deployment.HasDoubleJump : doubleJumpUnlocked;
    public bool HasHover => HasDeployment && attemptActive && officer.Deployment.HasHover;
    private int ExtraJumpCapacity => HasDoubleJump ? (HasDeployment ? 1 : stats.maxAirJumps) : 0;
    public int AirJumpsRemaining => airJumpsRemaining;
    public float HoverRemaining { get; private set; }
    public bool IsHovering { get; private set; }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        attemptActive = true;
        RefillAirResources();
    }

    public void CleanupAttempt()
    {
        attemptActive = false;
        airJumpsRemaining = 0;
        HoverRemaining = 0;
        IsHovering = false;
        moveInput = Vector2.zero;
        IsGrounded = false;
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
        moveInput = input.MoveInput;
        if (Mathf.Abs(moveInput.x) > 0.01f)
            MovementFacingDirection = moveInput.x > 0f ? 1 : -1;

        // Facing follows the aim so the player can move left while shooting right.
        Vector2 aim = input.AimDirection;
        if (Mathf.Abs(aim.x) > 0.01f)
            FacingDirection = aim.x > 0 ? 1 : -1;

        // flipX only mirrors the sprite; flipping localScale would also mirror FirePoint and CameraTarget.
        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingDirection < 0;

        IsGrounded = false;
        if (groundCheck != null && rb.linearVelocity.y <= 0.1f)
        {
            // A downward foot probe avoids treating a wall as a landing surface.
            var hit = Physics2D.Raycast(groundCheck.position + Vector3.up * groundCheckRadius,
                Vector2.down, groundCheckRadius + 0.08f, groundLayer);
            IsGrounded = hit.collider != null && hit.normal.y > 0.5f;
        }

        if (IsGrounded)
        {
            RefillAirResources();
        }

        if (input.JumpPressedThisFrame && (dodge == null || !dodge.IsDodging))
        {
            TryJump();
        }
    }

    private void FixedUpdate()
    {
        IsHovering = false;
        if (dodge != null && dodge.IsDodging) return; // dodge drives movement during a dash

        float vertical = rb.linearVelocity.y;
        if (HasHover && input.GameplayInputEnabled && input.JumpHeld && !IsGrounded && vertical <= 0 && HoverRemaining > 0)
        {
            IsHovering = true;
            HoverRemaining = Mathf.Max(0, HoverRemaining - Time.fixedDeltaTime);
            // Gravity still integrates normally after this cap. Releasing Space resumes free fall.
            vertical = Mathf.Max(vertical, -HoverFallSpeed);
        }
        rb.linearVelocity = new Vector2(moveInput.x * MoveSpeed, vertical);
    }

    private void TryJump()
    {
        if (IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
            IsGrounded = false;
        }
        else if (HasDoubleJump && airJumpsRemaining > 0)
        {
            airJumpsRemaining--;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
        }
    }

    public void UnlockDoubleJump()
    {
        doubleJumpUnlocked = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
