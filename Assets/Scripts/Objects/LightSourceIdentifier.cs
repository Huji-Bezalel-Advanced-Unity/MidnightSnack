// LightSourceIdentifier.cs
using UnityEngine;

// Attach this to the same GameObject as the HardLight2D component
// and the 'LightArea' trigger collider.
public class LightSourceIdentifier : MonoBehaviour
{
    // You might not even need anything in here,
    // its presence and transform.position are enough.
    // Optionally cache transform if accessed frequently.
    public Transform SourceTransform { get; private set; }

    void Awake() {
        SourceTransform = transform;
    }
}