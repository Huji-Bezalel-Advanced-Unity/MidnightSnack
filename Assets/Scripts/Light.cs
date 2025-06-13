using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CheckLightRange : MonoBehaviour
{
    [Header("Light Detection Settings")]
    [SerializeField] private GameObject lightColliderParent;  
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float detectionMargin = 0.1f;  

    private DarkPlayerController movementScript;

    private void Awake()
    {
        movementScript = GetComponent<DarkPlayerController>();
        if (movementScript == null)
        {
            Debug.LogWarning("DarkPlayerController script not found on the player.");
        }
    }

    private void Update()
    {
        CheckIfInLight();
    }

    private void CheckIfInLight()
    {
        if (lightColliderParent == null || respawnPoint == null) return;

        Vector2 playerPosition = transform.position;
        Vector2 lightPosition = lightColliderParent.transform.position;
        Vector2 directionToLight = (lightPosition - playerPosition).normalized;
        float distanceToLight = Vector2.Distance(playerPosition, lightPosition);

        if (distanceToLight > detectionMargin)
            return;

        RaycastHit2D hit = Physics2D.Raycast(playerPosition, directionToLight, distanceToLight);

        if (hit.collider != null)
        {
            if (hit.collider.transform.IsChildOf(lightColliderParent.transform))
            {
                RespawnPlayer();
            }
        }
    }

    private void RespawnPlayer()
    {
        transform.position = respawnPoint.position;

        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }
}