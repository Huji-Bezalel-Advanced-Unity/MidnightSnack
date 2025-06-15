using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Key Bindings")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.W;

    [Header("Movement Settings")]
    public float acceleration = 50f;
    public float maxSpeed = 7f;
    public float jumpForce = 12f;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.5f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator anim;

    private float horizontalInput;
    private bool isGrounded;
    private bool wasInAir = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetKey(moveRightKey))
            horizontalInput = 1f;
        else if (Input.GetKey(moveLeftKey))
            horizontalInput = -1f;
        else
            horizontalInput = 0f;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded && Input.GetKeyDown(jumpKey))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        UpdateAnimations();
        FlipSprite();
    }

    void FixedUpdate()
    {
        if (horizontalInput != 0)
        {
            rb.AddForce(new Vector2(horizontalInput * acceleration, 0f));
        }

        float clampedX = Mathf.Clamp(rb.linearVelocity.x, -maxSpeed, maxSpeed);
        rb.linearVelocity = new Vector2(clampedX, rb.linearVelocity.y);
    }

    private void UpdateAnimations()
    {
        float speedX = Mathf.Abs(rb.linearVelocity.x);
        anim.SetFloat("Speed", speedX);

        if (!isGrounded && !wasInAir)
        {
            anim.SetTrigger("JumpTrigger");
            wasInAir = true;
        }

        if (isGrounded && wasInAir)
        {
            wasInAir = false;
        }
    }

    private void FlipSprite()
    {
        if (horizontalInput > 0)
            transform.localScale = new Vector3(1, 1, 1);
        else if (horizontalInput < 0)
            transform.localScale = new Vector3(-1, 1, 1);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
