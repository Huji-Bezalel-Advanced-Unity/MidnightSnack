// PlayerMovement.cs (New File)
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    // Using simple velocity check for testing jump
    [SerializeField] private float verticalVelocityThreshold = 0.05f;

    // --- Private Variables ---
    private Rigidbody2D rb;
    private bool movementEnabled = true; // Controls if input is processed

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Only process movement if enabled
        if (movementEnabled)
        {
            HandleHorizontalMovement();
            HandleJumpInput();
        }
    }

    /// <summary>
    /// Reads horizontal input and applies velocity.
    /// </summary>
    private void HandleHorizontalMovement()
    {
        float moveInput = Input.GetAxis("Horizontal"); // Consider GetAxisRaw for less smoothing
        // Keep existing vertical velocity when applying horizontal
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    /// <summary>
    /// Handles jump input based on vertical velocity (Simplified for testing).
    /// </summary>
    private void HandleJumpInput()
    {
        // Check condition: Vertical velocity is near zero
        bool canJumpForTesting = Mathf.Abs(rb.linearVelocity.y) < verticalVelocityThreshold;

        if (Input.GetButtonDown("Jump") && canJumpForTesting)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    /// <summary>
    /// Allows external scripts (like PlayerInteraction) to enable/disable player input processing.
    /// </summary>
    /// <param name="enable">True to enable movement, false to disable.</param>
    public void SetMovementEnabled(bool enable)
    {
        movementEnabled = enable;

        // Optional: If disabling movement, might want to zero out velocity immediately
        // to prevent sliding if disabled mid-air or mid-run.
        if (!enable)
        {
            // Be careful: Setting velocity to zero might interfere with rope/ladder control.
            // It might be better handled by the interaction script itself setting gravity=0/velocity=0.
            // Consider if this line is needed based on interaction behavior.
            // rb.velocity = new Vector2(0, rb.velocity.y); // Zero horizontal only? Or Vector2.zero?
        }
        // Debug.Log($"PlayerMovement SetMovementEnabled: {enable}");
    }

    // Public access to Rigidbody if needed by other components (e.g., PlayerInteraction for EventManager)
    public Rigidbody2D Rigidbody => rb;
}