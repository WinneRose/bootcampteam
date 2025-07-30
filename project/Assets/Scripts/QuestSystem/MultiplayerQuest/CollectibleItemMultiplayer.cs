using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class CollectibleItemMultiplayer : NetworkBehaviour
{
    [SerializeField] private string itemTag = "Coin";
    [SerializeField] private GameObject collectEffect;
    
    private bool hasBeenCollected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenCollected) return;
        
        if (other.CompareTag("Player"))
        {
            if (IsServer)
            {
                if (CanBeCollected())
                {
                    CollectItem();
                }
            }
            else
            {
                RequestCollectionServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectionServerRpc()
    {
        if (hasBeenCollected) return;
        
        if (CanBeCollected())
        {
            CollectItem();
        }
    }

    private bool CanBeCollected()
    {
        if (NetworkedQuestManager.Instance == null)
            return false;

        var activeQuests = NetworkedQuestManager.Instance.GetActiveQuests();
        
        return activeQuests.Any(quest => 
            quest.IsCollectionBased() && 
            quest.template.collectionNameTag == itemTag && 
            !quest.IsCompleted() && 
            !quest.IsFailed());
    }

    private void CollectItem()
    {
        if (hasBeenCollected) return;
        hasBeenCollected = true;
        
        // Show collection effect on all clients
        ShowCollectionEffectClientRpc(transform.position);

        // Report to quest system
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.ReportItemCollected(itemTag);
        }

        // Remove item
        if (IsServer)
        {
            Invoke(nameof(DespawnItem), 0.1f);
        }
    }

    [ClientRpc]
    private void ShowCollectionEffectClientRpc(Vector3 position)
    {
        if (collectEffect != null)
        {
            Instantiate(collectEffect, position, Quaternion.identity);
        }
    }

    private void DespawnItem()
    {
        if (IsServer && GetComponent<NetworkObject>() != null)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    // Context Menu Debug Options
    [ContextMenu("Debug: Force Collect")]
    private void DebugForceCollect()
    {
        if (IsServer)
        {
            CollectItem();
        }
        else
        {
            Debug.Log("Force collect can only be used on server!");
        }
    }

    [ContextMenu("Debug: Check Collection Status")]
    private void DebugCheckCollectionStatus()
    {
        Debug.Log($"=== COLLECTIBLE DEBUG INFO ===");
        Debug.Log($"Item Tag: {itemTag}");
        Debug.Log($"Has Been Collected: {hasBeenCollected}");
        Debug.Log($"Can Be Collected: {CanBeCollected()}");
        Debug.Log($"Is Server: {IsServer}");
        
        if (NetworkedQuestManager.Instance != null)
        {
            var activeQuests = NetworkedQuestManager.Instance.GetActiveQuests();
            Debug.Log($"Active Quests Count: {activeQuests.Count}");
            
            foreach (var quest in activeQuests)
            {
                if (quest.IsCollectionBased())
                {
                    bool matches = quest.template.collectionNameTag == itemTag;
                    Debug.Log($"Quest: {quest.GetQuestTitle()}, Tag: {quest.template.collectionNameTag}, Matches: {matches}");
                }
            }
        }
        else
        {
            Debug.Log("NetworkedQuestManager.Instance is null!");
        }
    }
}