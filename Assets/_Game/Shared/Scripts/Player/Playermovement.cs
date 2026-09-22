using UnityEngine;

/// <summary>
/// Top-down planar movement (Move vector from PlayerInputReader). Facing
/// follows the aim direction, not the movement, so the player can strafe
/// one way while shooting the other. Yields control of the Rigidbody2D to
/// PlayerDodge while a dodge is in progress.
/// The jump / ground-check members below are left over from the earlier
/// side-scroller version and are currently unused (kept until the team
/// decides to remove them).
/// </summary>
// FUNCTION SUMMARY
// Update()             — reads move/aim input from PlayerInputReader, updates FacingDirection and the sprite flip, checks if grounded.
// FixedUpdate()        — applies the planar velocity each physics step (unless a dodge is currently controlling movement).
// TryJump()             — (unused in top-down) performs a ground jump, or an air jump if Double Jump is unlocked and one is still available.
// UnlockDoubleJump()   — permanently turns on the double jump via the upgrade tree.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Upgrades")]
    public bool doubleJumpUnlocked = false;

    /// <summary>1 = facing right, -1 = facing left. Used by dodge/shooting to know which way to fire/dash.</summary>
    public int FacingDirection { get; private set; } = 1;

    /// <summary>Current Move input (length 0..1), for other systems that want the movement direction.</summary>
    public Vector2 MoveInput => moveInput;

    private Rigidbody2D rb;
    private PlayerDodge dodge;
    private PlayerInputReader input;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    private bool isGrounded;
    private int airJumpsRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        dodge = GetComponent<PlayerDodge>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        input = GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("PlayerMovement requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        moveInput = input.MoveInput;

        // Facing follows the aim so the player can move left while shooting right.
        Vector2 aim = input.AimDirection;
        if (Mathf.Abs(aim.x) > 0.01f)
            FacingDirection = aim.x > 0 ? 1 : -1;

        // flipX only mirrors the sprite; flipping localScale would also mirror FirePoint and CameraTarget.
        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingDirection < 0;

        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded)
        {
            airJumpsRemaining = doubleJumpUnlocked ? stats.maxAirJumps : 0;
        }

        // Jump input is no longer read: the game is top-down (see Docs/ProjectMap.md). TryJump is kept for now.
    }

    private void FixedUpdate()
    {
        if (dodge != null && dodge.IsDodging) return; // dodge drives movement during a dash

        // Top-down: planar velocity from the Move vector (PlayerInputReader already clamps it to length 1,
        // so diagonals are not faster). The Player Rigidbody2D must have gravityScale = 0.
        // Unity 6+ renamed Rigidbody2D.velocity to linearVelocity.
        rb.linearVelocity = moveInput * stats.moveSpeed;
    }

    private void TryJump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpForce);
        }
        else if (doubleJumpUnlocked && airJumpsRemaining > 0)
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