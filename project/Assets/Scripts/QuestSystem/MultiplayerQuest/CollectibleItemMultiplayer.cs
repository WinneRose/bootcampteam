using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class CollectibleItemMultiplayer : NetworkBehaviour
{
    [SerializeField] private string itemTag = "Coin";
    [SerializeField] private GameObject collectEffect;
    
    private bool hasBeenCollected = false; // ✅ Prevent double collection

    private void OnTriggerEnter(Collider other)
    {
        // ✅ Only allow collection once
        if (hasBeenCollected) return;
        
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[CollectibleItem] Player touched item with tag: '{itemTag}'");
            
            // ✅ Only server processes the collection logic
            if (IsServer)
            {
                // Check if any quest needs this item
                if (CanBeCollected())
                {
                    CollectItem(other.gameObject);
                }
                else
                {
                    Debug.Log($"[CollectibleItem] No active quests need '{itemTag}' right now.");
                }
            }
            else
            {
                // ✅ Client requests collection from server
                RequestCollectionServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectionServerRpc()
    {
        // ✅ Double-check on server side
        if (hasBeenCollected) return;
        
        if (CanBeCollected())
        {
            CollectItem(null); // No need for player reference on server
        }
    }

    private bool CanBeCollected()
    {
        if (NetworkedQuestManager.Instance == null)
        {
            Debug.LogError("[CollectibleItem] NetworkedQuestManager.Instance is null!");
            return false;
        }

        var activeQuests = NetworkedQuestManager.Instance.GetActiveQuests();
        
        bool canCollect = activeQuests.Any(quest => 
            quest.IsCollectionBased() && 
            quest.template.collectionNameTag == itemTag && 
            !quest.IsCompleted() && 
            !quest.IsFailed());
            
        Debug.Log($"[CollectibleItem] Can collect '{itemTag}': {canCollect}");
        return canCollect;
    }

    private void CollectItem(GameObject player)
    {
        // ✅ Mark as collected immediately to prevent double collection
        if (hasBeenCollected) return;
        hasBeenCollected = true;
        
        Debug.Log($"[CollectibleItem] ✅ COLLECTING ITEM: '{itemTag}'");
        
        // Show collection effect on all clients
        ShowCollectionEffectClientRpc(transform.position);

        // Report to quest system (server only)
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.ReportItemCollected(itemTag);
            Debug.Log($"[CollectibleItem] Reported collection to QuestManager");
        }
        else
        {
            Debug.LogError("[CollectibleItem] QuestManager is null! Cannot report collection!");
        }

        // Remove item (server handles this)
        if (IsServer)
        {
            Debug.Log($"[CollectibleItem] Server despawning item");
            // Small delay to ensure effect plays
            Invoke(nameof(DespawnItem), 0.1f);
        }
    }

    [ClientRpc]
    private void ShowCollectionEffectClientRpc(Vector3 position)
    {
        // Show collection effect on all clients
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

}