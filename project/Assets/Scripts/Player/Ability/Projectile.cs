using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
    [Header("Settings")]
    public float damage = 10f;
    public float lifetime = 5f;
    public GameObject hitEffect;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip hitSound;

    private Rigidbody rb;
    private bool hasHit = false;
    private bool isBeingDestroyed = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Ensure projectile uses solid collider (not trigger)
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false;
        }
        
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
        
        // Play launch sound
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    public void Initialize(Vector3 direction, float speed)
    {
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
    }

    // Only use OnCollisionEnter for solid collider detection
    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        HandleHit(collision.gameObject, hitPoint);
    }

    private void HandleHit(GameObject hitObject, Vector3 hitPoint)
    {
        // Prevent multiple hits and destruction race conditions
        if (hasHit || isBeingDestroyed) return;
        hasHit = true;

        // Debug log for projectile hits
        Debug.Log($"[PROJECTILE] {gameObject.name} (Tag: '{gameObject.tag}') hit {hitObject.name} at {hitPoint}");

        // Play hit effect immediately
        if (hitEffect != null)
        {
            Instantiate(hitEffect, hitPoint, Quaternion.identity);
        }

        // Play hit sound
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        // Handle special cases
        if (hitObject.CompareTag("Bridge"))
        {
            if (hitObject.transform.childCount > 0)
            {
                hitObject.transform.GetChild(0).gameObject.SetActive(true);
            }
        }

        // Apply damage if target can take damage
        IDamageable damageable = hitObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // Check if this is a plant hit - let PlantGrowthManager handle the counting and destruction
        PlantGrowthManager plantManager = hitObject.GetComponent<PlantGrowthManager>();
        if (plantManager != null)
        {
            Debug.Log($"[PROJECTILE] Hit plant! Projectile tag: '{gameObject.tag}'");
            // Plant will handle the destruction through ForceDestroy()
            // Don't destroy here to avoid race conditions
            return;
        }

        // For non-plant collisions, destroy immediately
        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (isBeingDestroyed) return;
        isBeingDestroyed = true;

        // Stop movement immediately
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; // Prevent further physics interactions
        }

        // Disable collider to prevent additional hits
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Handle networked destruction
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            if (IsServer)
            {
                netObj.Despawn();
            }
            // If client, the server will handle despawning
        }
        else
        {
            // Non-networked object, destroy immediately
            Destroy(gameObject);
        }
    }

    // Public method for external destruction (called by PlantGrowthManager)
    public void ForceDestroy()
    {
        if (isBeingDestroyed) return;
        hasHit = true; // Mark as hit to prevent further processing
        DestroyProjectile();
    }
}

// Simple interface for damage
public interface IDamageable
{
    void TakeDamage(float damage);
}