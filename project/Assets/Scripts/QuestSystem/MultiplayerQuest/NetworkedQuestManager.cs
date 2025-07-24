using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class NetworkedQuestManager : NetworkBehaviour
{
    public static NetworkedQuestManager Instance;

    [SerializeField] private List<QuestStructure> availableQuestTemplates = new List<QuestStructure>();
    
    private Dictionary<int, NetworkedQuestInstance> activeQuests = new Dictionary<int, NetworkedQuestInstance>();
    private HashSet<int> questsBeingRemoved = new HashSet<int>();
    private int nextQuestId = 0;

    // Events
    public event Action<NetworkedQuestInstance> OnQuestStarted;
    public event Action<NetworkedQuestInstance> OnQuestCompleted;
    public event Action<NetworkedQuestInstance> OnQuestFailed;
    public event Action<NetworkedQuestInstance> OnQuestUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        OnQuestStarted += HandleQuestStarted;
        OnQuestCompleted += HandleQuestCompleted;
        OnQuestFailed += HandleQuestFailed;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Clean up all active quests
        foreach (var quest in activeQuests.Values)
        {
            quest.Dispose();
        }
        activeQuests.Clear();
    }

    private void Update()
    {
        if (!IsServer) return; // Only server updates quest logic

        var questsToCheck = activeQuests.Values.ToList();
        
        foreach (var quest in questsToCheck)
        {
            // Skip quests that are already completed or failed
            if (quest.IsCompleted() || quest.IsFailed())
                continue;

            // Store previous state
            bool wasCompletedBefore = quest.IsCompleted();
            bool wasFailedBefore = quest.IsFailed();

            // Update quest (this handles time-based logic)
            quest.UpdateQuest(Time.deltaTime);

            // Check for state changes
            bool isCompletedNow = quest.IsCompleted();
            bool isFailedNow = quest.IsFailed();

            int questId = quest.GetQuestId();

            // Trigger events for newly completed/failed quests
            if (!wasFailedBefore && isFailedNow)
            {
                Debug.Log($"[NetworkedQuestManager] ❌ Quest Failed: {quest.GetQuestTitle()}");
                OnQuestFailed?.Invoke(quest);
                NotifyQuestFailedClientRpc(questId);
                StartCoroutine(RemoveQuestAfterDelay(questId, 2.5f));
            }
            else if (!wasCompletedBefore && isCompletedNow)
            {
                Debug.Log($"[NetworkedQuestManager] ✅ Quest Completed: {quest.GetQuestTitle()}");
                OnQuestCompleted?.Invoke(quest);
                NotifyQuestCompletedClientRpc(questId);
                StartCoroutine(RemoveQuestAfterDelay(questId, 2.5f));
            }
        }
    }

    private IEnumerator RemoveQuestAfterDelay(int questId, float delay)
    {
        if (questsBeingRemoved.Contains(questId))
            yield break;

        questsBeingRemoved.Add(questId);

        if (activeQuests.ContainsKey(questId))
        {
            Debug.Log($"[NetworkedQuestManager] Scheduled to remove: {activeQuests[questId].GetQuestTitle()} in {delay}s");
        }

        yield return new WaitForSeconds(delay);

        if (activeQuests.ContainsKey(questId))
        {
            var quest = activeQuests[questId];
            quest.Dispose();
            activeQuests.Remove(questId);
            
            Debug.Log($"[NetworkedQuestManager] Removed quest ID: {questId}");
            
            // Notify clients to remove the quest
            RemoveQuestClientRpc(questId);
        }

        questsBeingRemoved.Remove(questId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartQuestServerRpc(int templateIndex)
    {
        if (templateIndex < 0 || templateIndex >= availableQuestTemplates.Count)
        {
            Debug.LogError($"[NetworkedQuestManager] Invalid template index: {templateIndex}");
            return;
        }

        var template = availableQuestTemplates[templateIndex];
        StartQuestInternal(template);
    }

    public void StartQuest(QuestStructure questTemplate)
    {
        if (IsServer)
        {
            StartQuestInternal(questTemplate);
        }
        else
        {
            // Find template index and request from server
            int templateIndex = availableQuestTemplates.IndexOf(questTemplate);
            if (templateIndex >= 0)
            {
                StartQuestServerRpc(templateIndex);
            }
            else
            {
                Debug.LogError($"[NetworkedQuestManager] Quest template not found in available templates: {questTemplate.name}");
            }
        }
    }

    private void StartQuestInternal(QuestStructure questTemplate)
    {
        if (!IsServer) return;

        // Check if quest already exists
        foreach (var quest in activeQuests.Values)
        {
            if (quest.template == questTemplate)
            {
                Debug.LogWarning($"[NetworkedQuestManager] Quest already started: {questTemplate.name}");
                return;
            }
        }

        int questId = nextQuestId++;
        var instance = new NetworkedQuestInstance(questTemplate, questId, this);
        activeQuests[questId] = instance;

        OnQuestStarted?.Invoke(instance);
        
        // Notify all clients
        int templateIndex = availableQuestTemplates.IndexOf(questTemplate);
        StartQuestClientRpc(questId, templateIndex, instance.currentData);
    }

    [ClientRpc]
    private void StartQuestClientRpc(int questId, int templateIndex, QuestNetworkData initialData)
    {
        if (IsServer) return; // Server already handled this

        if (templateIndex < 0 || templateIndex >= availableQuestTemplates.Count)
        {
            Debug.LogError($"[NetworkedQuestManager] Invalid template index received: {templateIndex}");
            return;
        }

        var template = availableQuestTemplates[templateIndex];
        var instance = new NetworkedQuestInstance(template, questId, this);
        instance.currentData = initialData;
        
        // Subscribe to data changes for UI updates
        instance.OnDataChanged += (data) => OnQuestUpdated?.Invoke(instance);
        
        activeQuests[questId] = instance;

        OnQuestStarted?.Invoke(instance);
    }

    // Method to sync quest data in real-time
    [ClientRpc]
    public void SyncQuestDataClientRpc(int questId, QuestNetworkData newData)
    {
        if (IsServer) return; // Server already has the data
        
        if (activeQuests.ContainsKey(questId))
        {
            activeQuests[questId].UpdateDataFromNetwork(newData);
            OnQuestUpdated?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void NotifyQuestCompletedClientRpc(int questId)
    {
        if (IsServer) return; // Server already handled this
        
        if (activeQuests.ContainsKey(questId))
        {
            Debug.Log($"[NetworkedQuestManager] Client received quest completion notification for: {activeQuests[questId].GetQuestTitle()}");
            OnQuestCompleted?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void NotifyQuestFailedClientRpc(int questId)
    {
        if (IsServer) return; // Server already handled this
        
        if (activeQuests.ContainsKey(questId))
        {
            OnQuestFailed?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void RemoveQuestClientRpc(int questId)
    {
        if (IsServer) return; // Server already handled this
        
        if (activeQuests.ContainsKey(questId))
        {
            var quest = activeQuests[questId];
            quest.Dispose();
            activeQuests.Remove(questId);
            Debug.Log($"[NetworkedQuestManager] Client removed quest ID: {questId}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportItemCollectedServerRpc(string tag)
    {
        ReportItemCollectedInternal(tag);
    }

    public void ReportItemCollected(string tag)
    {
        if (IsServer)
        {
            ReportItemCollectedInternal(tag);
        }
        else
        {
            ReportItemCollectedServerRpc(tag);
        }
    }

    private void ReportItemCollectedInternal(string tag)
    {
        Debug.Log($"[NetworkedQuestManager] 📦 Item collected: '{tag}'");
        
        foreach (var quest in activeQuests.Values)
        {
            if (quest.IsCollectionBased() && 
                quest.template.collectionNameTag == tag && 
                !quest.IsCompleted() && 
                !quest.IsFailed())
            {
                Debug.Log($"[NetworkedQuestManager] ✅ Applying to quest: {quest.GetQuestTitle()}");
                
                // Store state before collection
                bool wasCompletedBefore = quest.IsCompleted();
                
                // Let the quest handle the collection
                quest.CollectItem();
                
                // Check if quest was just completed
                bool isCompletedNow = quest.IsCompleted();
                
                Debug.Log($"[NetworkedQuestManager] Progress: {quest.GetProgressText()}");
                Debug.Log($"[NetworkedQuestManager] Was completed before: {wasCompletedBefore}, Is completed now: {isCompletedNow}");
                
                // ✅ FIX: Immediately trigger completion event if quest was just completed
                if (!wasCompletedBefore && isCompletedNow)
                {
                    Debug.Log($"[NetworkedQuestManager] 🎉 QUEST JUST COMPLETED: {quest.GetQuestTitle()}!");
                    OnQuestCompleted?.Invoke(quest);
                    NotifyQuestCompletedClientRpc(quest.GetQuestId());
                    StartCoroutine(RemoveQuestAfterDelay(quest.GetQuestId(), 2.5f));
                }
            }
        }
    }

    public List<NetworkedQuestInstance> GetActiveQuests()
    {
        return activeQuests.Values.ToList();
    }

    public NetworkedQuestInstance GetQuestInstance(QuestStructure template)
    {
        return activeQuests.Values.FirstOrDefault(q => q.template == template);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearAllQuestsServerRpc()
    {
        ClearAllQuestsInternal();
        ClearAllQuestsClientRpc();
    }

    public void ClearAllQuests()
    {
        if (IsServer)
        {
            ClearAllQuestsInternal();
            ClearAllQuestsClientRpc();
        }
        else
        {
            ClearAllQuestsServerRpc();
        }
    }

    private void ClearAllQuestsInternal()
    {
        foreach (var quest in activeQuests.Values)
        {
            quest.Dispose();
        }
        activeQuests.Clear();
        questsBeingRemoved.Clear();
        Debug.Log("[NetworkedQuestManager] All quests cleared.");
    }

    [ClientRpc]
    private void ClearAllQuestsClientRpc()
    {
        if (IsServer) return;
        ClearAllQuestsInternal();
    }

    // Event handlers for logging
    private void HandleQuestStarted(NetworkedQuestInstance quest)
    {
        Debug.Log($"[NetworkedQuestManager] 🚀 Started quest: {quest.GetQuestTitle()}");
    }

    private void HandleQuestCompleted(NetworkedQuestInstance quest)
    {
        Debug.Log($"[NetworkedQuestManager] 🎉 Completed quest: {quest.GetQuestTitle()}");
    }

    private void HandleQuestFailed(NetworkedQuestInstance quest)
    {
        Debug.Log($"[NetworkedQuestManager] ❌ Failed quest: {quest.GetQuestTitle()}");
    }

   

  
}