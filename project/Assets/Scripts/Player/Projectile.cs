using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float damage = 10f;
    public bool destroyOnHit = true;
    public GameObject hitEffect;
    public LayerMask hitLayers = -1;
    
    [Header("Visual Effects")]
    public TrailRenderer trail;
    public Light projectileLight;
    public ParticleSystem particles;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip launchSound;
    public AudioClip hitSound;
    
    private Rigidbody rb;
    private bool hasHit = false;
    private float lifetime;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Play launch sound
        if (audioSource != null && launchSound != null)
        {
            audioSource.PlayOneShot(launchSound);
        }
    }
    
    public void Initialize(Vector3 direction, float speed, float projectileLifetime)
    {
        lifetime = projectileLifetime;
        
        // Set velocity
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        // Check if we hit a valid target
        if (((1 << collision.gameObject.layer) & hitLayers) != 0)
        {
            hasHit = true;

            // Handle hit
            HandleHit(collision);

            if (destroyOnHit)
            {
                DestroyProjectile();
            }
        }
        if (collision.gameObject.CompareTag("Bridge"))
        {
            collision.gameObject.transform.GetChild(0).gameObject.SetActive(true);
            Destroy(gameObject);
        }
    }
    
    private void HandleHit(Collision collision)
    {
        // Apply damage if target has health component
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
        
        // Create hit effect
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, collision.contacts[0].point, Quaternion.LookRotation(collision.contacts[0].normal));
            Destroy(effect, 5f);
        }
        
        // Play hit sound
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
        
        // Add force to rigidbody if it exists
        Rigidbody hitRb = collision.gameObject.GetComponent<Rigidbody>();
        if (hitRb != null)
        {
            Vector3 forceDirection = collision.contacts[0].point - transform.position;
            hitRb.AddForce(forceDirection.normalized * 500f);
        }
        
        Debug.Log($"Projectile hit: {collision.gameObject.name}");
    }
    
    private void DestroyProjectile()
    {
        // Disable collider to prevent multiple hits
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
        
        // Stop movement
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Disable trail
        if (trail != null)
        {
            trail.enabled = false;
        }
        
        // Disable light
        if (projectileLight != null)
        {
            projectileLight.enabled = false;
        }
        
        // Stop particles
        if (particles != null)
        {
            particles.Stop();
        }
        
        // Destroy after a short delay to allow sound to finish
        Destroy(gameObject, 2f);
    }
}

// Interface for objects that can take damage
public interface IDamageable
{
    void TakeDamage(float damage);
}