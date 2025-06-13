using UnityEngine;
using System.Collections;
using System.Collections.Generic; // For potential list of targets
using System.Linq; // For sorting/finding closest

public class TurretController : MonoBehaviour
{
    public enum FiringMode
    {
        ForwardOnly,    // Fires only if target is within a forward cone
        ClosestVisible // Fires at the closest player with clear line of sight in any direction
    }

    [Header("Setup")]
    [SerializeField] private GameObject projectilePrefab; // Assign your projectile prefab
    [SerializeField] private Transform firePoint;       // Empty GameObject where projectiles spawn
    [SerializeField] private LayerMask targetLayers;    // Layer(s) the players are on
    [SerializeField] private LayerMask obstacleLayers;  // Layer(s) that block line of sight (Walls, Ground, etc.)

    [Header("Firing Settings")]
    [SerializeField] private FiringMode firingMode = FiringMode.ForwardOnly;
    [SerializeField] private float fireRate = 1f; // Shots per second
    [SerializeField] private float detectionRange = 10f;

    [Header("ForwardOnly Settings")]
    [Tooltip("The direction the turret considers 'forward'. Usually Vector2.right (red axis) or Vector2.up (green axis).")]
    [SerializeField] private Vector2 forwardDirection = Vector2.right; // Default to right
    [SerializeField] [Range(0f, 180f)] private float forwardAngleTolerance = 30f; // Cone angle (degrees total)

    [Header("Visuals (Optional)")]
    [SerializeField] private Transform turretHead; // Part of the turret that rotates to aim

    // --- Internal State ---
    private float timeBetweenShots;
    private float timeSinceLastShot = 0f;
    private Transform currentTarget = null;
    private Vector2 aimDirection = Vector2.right; // Store direction to aim/fire

    void Awake()
    {
        if (projectilePrefab == null) Debug.LogError($"Turret '{gameObject.name}' missing projectile prefab!", this);
        if (firePoint == null) Debug.LogError($"Turret '{gameObject.name}' missing fire point!", this);
        if (targetLayers == 0) Debug.LogWarning($"Turret '{gameObject.name}' has no Target Layers assigned.", this);
        if (obstacleLayers == 0) Debug.LogWarning($"Turret '{gameObject.name}' has no Obstacle Layers assigned. Line of sight checks might fail.", this);

        if (fireRate > 0)
        {
            timeBetweenShots = 1f / fireRate;
        }
        else
        {
            timeBetweenShots = float.MaxValue; // Effectively disable automatic firing if rate is zero or less
            Debug.LogWarning($"Turret '{gameObject.name}' has a fire rate of 0 or less. Automatic firing disabled.", this);
        }

        // Initialize aim direction based on serialized forward direction
        aimDirection = GetWorldForwardDirection();
    }

    void Update()
    {
        timeSinceLastShot += Time.deltaTime;

        FindTarget(); // Find a target based on the current mode

        if (currentTarget != null)
        {
            AimTurret(); // Aim visually if possible

            // Check if ready to fire
            if (timeSinceLastShot >= timeBetweenShots)
            {
                Fire();
                timeSinceLastShot = 0f; // Reset timer
            }
        }
    }

    void FindTarget()
    {
        currentTarget = null; // Assume no target initially
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRange, targetLayers);

        if (potentialTargets.Length == 0) return; // No targets in range

        Transform closestVisibleTarget = null;
        float minDistanceSqr = Mathf.Infinity;

        // Determine the turret's actual forward direction in world space
        Vector2 worldForward = GetWorldForwardDirection();

        foreach (Collider2D targetCollider in potentialTargets)
        {
            Transform targetTransform = targetCollider.transform;
            Vector2 directionToTarget = (targetTransform.position - firePoint.position).normalized;
            float distanceToTarget = Vector2.Distance(firePoint.position, targetTransform.position);

            // Check Line of Sight first (common to both modes)
            if (!HasClearLineOfSight(targetTransform, directionToTarget, distanceToTarget))
            {
                continue; // Obstacle in the way
            }

            // Mode-specific checks
            if (firingMode == FiringMode.ForwardOnly)
            {
                // Check angle relative to turret's forward direction
                float angle = Vector2.Angle(worldForward, directionToTarget);
                if (angle <= forwardAngleTolerance / 2f) // Check within half the total tolerance angle
                {
                    // Valid target found in forward cone, select the first one for simplicity
                    // (Could be modified to select closest *within* the cone)
                     currentTarget = targetTransform;
                     aimDirection = directionToTarget; // Aim directly at the target
                    // Debug.DrawLine(firePoint.position, targetTransform.position, Color.green); // Visualize LOS
                    return; // Found a valid target in front, stop searching
                }
                 // else { Debug.DrawLine(firePoint.position, targetTransform.position, Color.yellow); } // Visualize LOS but wrong angle
            }
            else // FiringMode.ClosestVisible
            {
                // We already know LOS is clear. Find the closest one.
                float distSqr = (targetTransform.position - firePoint.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestVisibleTarget = targetTransform;
                    // Don't set aimDirection yet, wait until we've checked all targets
                }
                 // Debug.DrawLine(firePoint.position, targetTransform.position, Color.green); // Visualize LOS
            }
        }

        // After checking all targets in ClosestVisible mode
        if (firingMode == FiringMode.ClosestVisible && closestVisibleTarget != null)
        {
            currentTarget = closestVisibleTarget;
            // Calculate aim direction towards the final closest target
            aimDirection = (currentTarget.position - firePoint.position).normalized;
        }
    }

    bool HasClearLineOfSight(Transform target, Vector2 direction, float distance)
    {
        // Raycast from fire point towards the target
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, direction, distance, obstacleLayers);

        // If the raycast hits nothing, LOS is clear
        // If it hits something, check if the thing it hit IS the target itself
        if (hit.collider == null || hit.transform == target)
        {
            // Debug.DrawRay(firePoint.position, direction * distance, Color.green); // Clear LOS
            return true;
        }
        else
        {
            // Debug.DrawRay(firePoint.position, direction * hit.distance, Color.red); // Blocked LOS
            // Debug.Log($"LOS blocked by {hit.collider.name}");
            return false;
        }
    }

    void AimTurret()
    {
        if (turretHead != null && currentTarget != null)
        {
            // Calculate the angle needed to point at the aimDirection
            // Note: For 2D top-down or side-scroller, adjust axis accordingly
            // Assumes turret's "forward" is its local right (positive X) axis
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            // Smooth rotation (optional)
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            turretHead.rotation = Quaternion.Slerp(turretHead.rotation, targetRotation, Time.deltaTime * 10f); // Adjust rotation speed as needed
            // Or instantaneous:
            // turretHead.rotation = targetRotation;
        }
        // If no turret head, turret just fires in aimDirection without rotating visually
    }


    void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

         // Debug.Log($"Turret '{gameObject.name}' firing at {currentTarget.name} in direction {aimDirection}");

        // Instantiate the projectile
        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation); // Start with firepoint rotation

        // --- Set projectile direction/velocity ---
        // Option A: If projectile has a script to set its own direction/speed
        Projectile proj = projectileGO.GetComponent<Projectile>(); // Assuming a Projectile script exists
        if (proj != null)
        {
            proj.SetDirection(aimDirection); // Projectile handles its own speed/movement
        }
        // Option B: Directly set velocity if projectile only has Rigidbody2D
        else
        {
            Rigidbody2D projRb = projectileGO.GetComponent<Rigidbody2D>();
            if (projRb != null)
            {
                 // Calculate speed - maybe projectile has its own speed variable?
                 float projectileSpeed = 10f; // Example speed
                projRb.linearVelocity = aimDirection * projectileSpeed;
                // Optional: Rotate projectile sprite to face direction
                 float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                 projectileGO.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    Vector2 GetWorldForwardDirection()
    {
        // Converts the local forwardDirection vector into world space
        if (forwardDirection == Vector2.right) return transform.right;
        if (forwardDirection == Vector2.left) return -transform.right;
        if (forwardDirection == Vector2.up) return transform.up;
        if (forwardDirection == Vector2.down) return -transform.up;
        // Default fallback to transform.right if vector is custom/zero
        return transform.TransformDirection(forwardDirection.normalized);
    }

    // Draw detection range and forward cone in editor
    void OnDrawGizmosSelected()
    {
        // Detection Range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Forward Cone (if applicable)
        if (firingMode == FiringMode.ForwardOnly)
        {
            Gizmos.color = Color.cyan;
            Vector3 worldForward = Application.isPlaying ? GetWorldForwardDirection() : transform.TransformDirection(forwardDirection.normalized); // Estimate in editor
            if(worldForward == Vector3.zero) worldForward = transform.right; // Fallback

            Vector3 coneEdge1 = Quaternion.Euler(0, 0, forwardAngleTolerance / 2f) * worldForward;
            Vector3 coneEdge2 = Quaternion.Euler(0, 0, -forwardAngleTolerance / 2f) * worldForward;

            Vector3 firePos = (firePoint != null) ? firePoint.position : transform.position;
            Gizmos.DrawLine(firePos, firePos + coneEdge1 * detectionRange * 0.5f); // Draw shorter lines for clarity
            Gizmos.DrawLine(firePos, firePos + coneEdge2 * detectionRange * 0.5f);
            Gizmos.DrawLine(firePos, firePos + (Vector3)worldForward * detectionRange * 0.6f); // Center line
        }
    }
}
