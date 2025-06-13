using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Pushable : MonoBehaviour
{
    [SerializeField] private float pushResistance = 1.0f;
    [SerializeField] private bool requiresSpecificPlayerPower = false;
    [SerializeField] private AudioClip pushSound; 

    public Rigidbody2D Rb { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        if (Rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning($"Pushable object '{gameObject.name}' had its Rigidbody2D set to Dynamic.", this);
            Rb.bodyType = RigidbodyType2D.Dynamic;
        }
        Rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (SoundManagerForGamePlay.Instance != null)
            {
                if (pushSound != null)
                    SoundManagerForGamePlay.Instance.PlaySFX(pushSound);
                else
                    SoundManagerForGamePlay.Instance.PlayPushSound();
            }
        }
    }
}