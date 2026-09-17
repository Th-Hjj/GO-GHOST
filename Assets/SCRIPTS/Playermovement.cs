using UnityEngine;

/// <summary>
/// Left/right movement, jump, and an unlockable double jump. Yields control
/// of the Rigidbody2D to PlayerDodge while a dodge is in progress.
/// </summary>
// FUNCTION SUMMARY
// Update()             — reads input, flips facing direction, checks if grounded, and listens for the jump button.
// FixedUpdate()        — applies horizontal velocity each physics step (unless a dodge is currently controlling movement).
// TryJump()             — performs a ground jump, or an air jump if Double Jump is unlocked and one is still available.
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

    private Rigidbody2D rb;
    private PlayerDodge dodge;
    private float horizontalInput;
    private bool isGrounded;
    private int airJumpsRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        dodge = GetComponent<PlayerDodge>();
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (horizontalInput != 0)
        {
            FacingDirection = horizontalInput > 0 ? 1 : -1;
            transform.localScale = new Vector3(FacingDirection, 1f, 1f);
        }

        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded)
        {
            airJumpsRemaining = doubleJumpUnlocked ? stats.maxAirJumps : 0;
        }

        if (Input.GetButtonDown("Jump"))
        {
            TryJump();
        }
    }

    private void FixedUpdate()
    {
        if (dodge != null && dodge.IsDodging) return; // dodge drives movement during a dash

        // Unity 6+ renamed Rigidbody2D.velocity to linearVelocity.
        // If you're on an older Unity version, replace linearVelocity with velocity below.
        rb.linearVelocity = new Vector2(horizontalInput * stats.moveSpeed, rb.linearVelocity.y);
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