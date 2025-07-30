using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class HittableTargetMultiplayer : NetworkBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private string targetTag = "Target";
    [Tooltip("Tags of projectiles that can hit this target")]
    [SerializeField] private string[] validProjectileTags = { "WaterProjectile", "Bullet" };
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private bool destroyOnHit = false;
    [SerializeField] private float destroyDelay = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;
    
    private bool hasBeenHit = false;
    private int hitCount = 0;

    private void Start()
    {
        // Ensure this object has the correct tag
        if (!gameObject.CompareTag(targetTag))
        {
            gameObject.tag = targetTag;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (destroyOnHit && hasBeenHit) return;
        
        // Check if the colliding object is a valid projectile
        bool isValidProjectile = validProjectileTags.Any(tag => other.CompareTag(tag));
        if (!isValidProjectile) return;
        
        if (IsServer)
        {
            if (CanBeHit())
            {
                ProcessHit(other);
            }
        }
        else
        {
            RequestHitServerRpc(other.tag);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHitServerRpc(string projectileTag)
    {
        if (destroyOnHit && hasBeenHit) return;
        
        bool isValidProjectile = validProjectileTags.Contains(projectileTag);
        if (!isValidProjectile) return;
        
        if (CanBeHit())
        {
            ProcessHit(null, projectileTag);
        }
    }

    private bool CanBeHit()
    {
        if (NetworkedQuestManager.Instance == null)
            return false;

        var activeQuests = NetworkedQuestManager.Instance.GetActiveQuests();
        
        return activeQuests.Any(quest => 
            quest.IsHitBased() && 
            quest.template.hitTargetTag == targetTag && 
            !quest.IsCompleted() && 
            !quest.IsFailed());
    }

    private void ProcessHit(Collider projectile, string projectileTag = null)
    {
        string actualProjectileTag = projectileTag ?? projectile?.tag;
        
        // Increment hit count
        hitCount++;
        
        // Show hit effect on all clients
        ShowHitEffectClientRpc(transform.position, actualProjectileTag);

        // Report to quest system
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.ReportTargetHit(targetTag, actualProjectileTag);
        }

        // Handle destruction if needed
        if (destroyOnHit)
        {
            hasBeenHit = true;
            if (IsServer)
            {
                Invoke(nameof(DespawnTarget), destroyDelay);
            }
        }

        Debug.Log($"Target hit! Tag: {targetTag}, Projectile: {actualProjectileTag}, Hit Count: {hitCount}");
    }

    [ClientRpc]
    private void ShowHitEffectClientRpc(Vector3 position, string projectileTag)
    {
        // Show visual effect
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Play sound effect
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
        else if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, position);
        }

        Debug.Log($"Hit effect shown for projectile: {projectileTag}");
    }

    private void DespawnTarget()
    {
        if (IsServer && GetComponent<NetworkObject>() != null)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    // Public methods for external access
    public int GetHitCount() => hitCount;
    public bool HasBeenHit() => hasBeenHit;
    public string GetTargetTag() => targetTag;

    // Context Menu Debug Options
    [ContextMenu("Debug: Force Hit")]
    private void DebugForceHit()
    {
        if (IsServer)
        {
            ProcessHit(null, validProjectileTags[0]);
        }
        else
        {
            Debug.Log("Force hit can only be used on server!");
        }
    }

    [ContextMenu("Debug: Check Hit Status")]
    private void DebugCheckHitStatus()
    {
        Debug.Log($"=== HITTABLE TARGET DEBUG INFO ===");
        Debug.Log($"Target Tag: {targetTag}");
        Debug.Log($"Valid Projectile Tags: {string.Join(", ", validProjectileTags)}");
        Debug.Log($"Has Been Hit: {hasBeenHit}");
        Debug.Log($"Hit Count: {hitCount}");
        Debug.Log($"Can Be Hit: {CanBeHit()}");
        Debug.Log($"Is Server: {IsServer}");
        Debug.Log($"Destroy On Hit: {destroyOnHit}");
        
        if (NetworkedQuestManager.Instance != null)
        {
            var activeQuests = NetworkedQuestManager.Instance.GetActiveQuests();
            Debug.Log($"Active Quests Count: {activeQuests.Count}");
            
            foreach (var quest in activeQuests)
            {
                if (quest.IsHitBased())
                {
                    bool matches = quest.template.hitTargetTag == targetTag;
                    Debug.Log($"Quest: {quest.GetQuestTitle()}, Target Tag: {quest.template.hitTargetTag}, Matches: {matches}");
                    Debug.Log($"  Required Hits: {quest.template.requiredHits}, Current Hits: {quest.GetHitCount()}");
                }
            }
        }
        else
        {
            Debug.Log("NetworkedQuestManager.Instance is null!");
        }
    }

    [ContextMenu("Debug: Reset Hit Count")]
    private void DebugResetHitCount()
    {
        hitCount = 0;
        hasBeenHit = false;
        Debug.Log("Hit count reset to 0");
    }
}