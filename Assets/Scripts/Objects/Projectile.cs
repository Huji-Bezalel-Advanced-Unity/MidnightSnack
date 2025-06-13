// Projectile.cs

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 3f;

    [Header("Impact")]
    [SerializeField] private float knockbackForce = 15f; // How strongly player is pushed
    [SerializeField] private LayerMask playerLayerMask; // *** NEW: Set this to the Player layer(s) ***

    private Vector2 direction = Vector2.right;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Ensure projectile doesn't collide with itself or other projectiles if needed
        // Physics2D.IgnoreLayerCollision(gameObject.layer, gameObject.layer); // Uncomment if needed

        if (playerLayerMask == 0) {
             Debug.LogWarning($"Projectile '{gameObject.name}' needs Player Layer Mask assigned in Inspector to apply knockback!", this);
        }

        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Sets the initial direction and velocity of the projectile.
    /// </summary>
    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        // Rotate sprite to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        // Set initial velocity
        if (rb != null) { rb.linearVelocity = direction * speed; }
    }

    // Use OnTriggerEnter2D if projectile collider is Trigger
    // Use OnCollisionEnter2D if projectile collider is Solid
    void OnTriggerEnter2D(Collider2D other) // Or OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(other.gameObject); // Pass the collided GameObject
    }

    /// <summary>
    /// Handles what happens when the projectile hits something.
    /// </summary>
    private void HandleCollision(GameObject hitObject)
    {
        // Check if the hit object is on one of the player layers using the layermask
        if (((1 << hitObject.layer) & playerLayerMask) != 0)
        {
            // --- Player Hit ---
            Rigidbody2D playerRb = hitObject.GetComponent<Rigidbody2D>();
            IInteractionController interactionController = hitObject.GetComponent<IInteractionController>();

            // --- Actions on Player ---
            bool stoppedInteraction = false;
            Vector2 knockbackDirection = direction; // Default knockback in projectile's forward direction

            // 1. Attempt to Stop Interaction FIRST
            if (interactionController != null)
            {
                // Check the player's state *before* forcing stop, maybe only knockback if NOT interacting?
                // Or always force stop? Let's stick to always forcing stop for interruption.
                interactionController.ForceStopInteraction(); // This triggers state change logic on player
                stoppedInteraction = true; // Assume it worked for logging/logic below
            }
            else { Debug.LogWarning($"Projectile Hit: Player '{hitObject.name}' does NOT have IInteractionController component!", this); }


            // 2. Apply Knockback AFTER attempting to stop interaction
            if (playerRb != null)
            {
                // Calculate knockback direction more simply: Use projectile's travel direction
                // Or calculate away from impact point if preferred:
                // knockbackDirection = ((Vector2)playerRb.position - rb.position).normalized;
                // if (knockbackDirection == Vector2.zero) knockbackDirection = direction;

                // --- CRITICAL: Apply knockback AFTER a very short delay ---
                // This gives the physics engine a tick to process the state change triggered by ForceStopInteraction
                // and apply the restored gravity BEFORE the knockback impulse fights it.
                // We need to start a small coroutine on the projectile to do this delay.
                // Therefore, we should NOT destroy the projectile immediately here.

                // Start a coroutine on this projectile to handle delayed knockback and destruction
                StartCoroutine(ApplyKnockbackAndDestroy(playerRb, knockbackDirection));

                // --- Return here, let the coroutine handle destruction ---
                return;

            }
            // If no Rigidbody found, just destroy projectile immediately
            Destroy(gameObject);


        }
        else if (((1 << hitObject.layer) & LayerMask.GetMask("Ground", "Obstacles", "Default")) != 0) // Added Default layer check
        {
            // --- Environment Hit ---
            Destroy(gameObject);
        }
        // Optional: Destroy if hitting anything else not specifically ignored
        // else
        // {
        //      Destroy(gameObject);
        // }
    }
    
    private IEnumerator ApplyKnockbackAndDestroy(Rigidbody2D playerRb, Vector2 knockbackDir)
    {
        // Wait for the end of the current frame to allow physics/state updates to settle
        yield return new WaitForEndOfFrame();
        // Alternatively, wait for FixedUpdate if issues persist:
        // yield return new WaitForFixedUpdate();

        // Ensure player RB still exists (it shouldn't be destroyed, but good practice)
        if (playerRb != null)
        {
            // Now apply the knockback
            playerRb.linearVelocity = Vector2.zero; // Reset velocity *just before* knockback
            playerRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
        }

        // Now destroy the projectile after applying knockback
        if(gameObject != null) // Check if projectile wasn't already destroyed
        {
            Destroy(gameObject);
        }
    }

    // FixedUpdate isn't strictly necessary if velocity is set once in SetDirection
    // void FixedUpdate() {
    //     // If you need constant force or more complex movement, use FixedUpdate
    //     // rb.velocity = direction * speed; // Already set in SetDirection
    // }
}