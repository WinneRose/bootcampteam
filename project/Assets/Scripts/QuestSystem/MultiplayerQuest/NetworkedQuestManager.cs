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
        if (!IsServer) return;

        var questsToCheck = activeQuests.Values.ToList();
        
        foreach (var quest in questsToCheck)
        {
            if (quest.IsCompleted() || quest.IsFailed())
                continue;

            // Store previous state
            bool wasCompletedBefore = quest.IsCompleted();
            bool wasFailedBefore = quest.IsFailed();
            
            // Update quest
            quest.UpdateQuest(Time.deltaTime);

            // Check for state changes
            bool isCompletedNow = quest.IsCompleted();
            bool isFailedNow = quest.IsFailed();
            int questId = quest.GetQuestId();

            // Handle failure
            if (!wasFailedBefore && isFailedNow)
            {
                OnQuestFailed?.Invoke(quest);
                NotifyQuestFailedClientRpc(questId);
                StartCoroutine(RemoveQuestAfterDelay(questId, 2.5f));
            }
            // Handle completion
            else if (!wasCompletedBefore && isCompletedNow && !isFailedNow)
            {
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
        yield return new WaitForSeconds(delay);

        if (activeQuests.ContainsKey(questId))
        {
            var quest = activeQuests[questId];
            quest.Dispose();
            activeQuests.Remove(questId);
            RemoveQuestClientRpc(questId);
        }

        questsBeingRemoved.Remove(questId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartQuestServerRpc(int templateIndex)
    {
        if (templateIndex < 0 || templateIndex >= availableQuestTemplates.Count)
            return;

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
            int templateIndex = availableQuestTemplates.IndexOf(questTemplate);
            if (templateIndex >= 0)
            {
                StartQuestServerRpc(templateIndex);
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
                return;
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
        if (IsServer) return;

        if (templateIndex < 0 || templateIndex >= availableQuestTemplates.Count)
            return;

        var template = availableQuestTemplates[templateIndex];
        var instance = new NetworkedQuestInstance(template, questId, this);
        instance.currentData = initialData;
        
        instance.OnDataChanged += (data) => OnQuestUpdated?.Invoke(instance);
        activeQuests[questId] = instance;
        OnQuestStarted?.Invoke(instance);
    }

    [ClientRpc]
    public void SyncQuestDataClientRpc(int questId, QuestNetworkData newData)
    {
        if (IsServer) return;
        
        if (activeQuests.ContainsKey(questId))
        {
            activeQuests[questId].UpdateDataFromNetwork(newData);
            OnQuestUpdated?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void NotifyQuestCompletedClientRpc(int questId)
    {
        if (IsServer) return;
        
        if (activeQuests.ContainsKey(questId))
        {
            OnQuestCompleted?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void NotifyQuestFailedClientRpc(int questId)
    {
        if (IsServer) return;
        
        if (activeQuests.ContainsKey(questId))
        {
            OnQuestFailed?.Invoke(activeQuests[questId]);
        }
    }

    [ClientRpc]
    private void RemoveQuestClientRpc(int questId)
    {
        if (IsServer) return;
        
        if (activeQuests.ContainsKey(questId))
        {
            var quest = activeQuests[questId];
            quest.Dispose();
            activeQuests.Remove(questId);
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
        foreach (var quest in activeQuests.Values)
        {
            if (quest.IsCollectionBased() && 
                quest.template.collectionNameTag == tag && 
                !quest.IsCompleted() && 
                !quest.IsFailed())
            {
                bool wasCompletedBefore = quest.IsCompleted();
                quest.CollectItem();
                bool isCompletedNow = quest.IsCompleted();
                
                if (!wasCompletedBefore && isCompletedNow)
                {
                    OnQuestCompleted?.Invoke(quest);
                    NotifyQuestCompletedClientRpc(quest.GetQuestId());
                    StartCoroutine(RemoveQuestAfterDelay(quest.GetQuestId(), 2.5f));
                }
            }
        }
    }
    
    // Add this method to your existing NetworkedQuestManager class:

[ServerRpc(RequireOwnership = false)]
public void ReportTargetHitServerRpc(string targetTag, string projectileTag)
{
    ReportTargetHitInternal(targetTag, projectileTag);
}

public void ReportTargetHit(string targetTag, string projectileTag)
{
    if (IsServer)
    {
        ReportTargetHitInternal(targetTag, projectileTag);
    }
    else
    {
        ReportTargetHitServerRpc(targetTag, projectileTag);
    }
}

private void ReportTargetHitInternal(string targetTag, string projectileTag)
{
    foreach (var quest in activeQuests.Values)
    {
        if (quest.IsHitBased() && 
            quest.template.hitTargetTag == targetTag && 
            quest.template.projectileTag == projectileTag &&
            !quest.IsCompleted() && 
            !quest.IsFailed())
        {
            bool wasCompletedBefore = quest.IsCompleted();
            quest.RegisterHit();
            bool isCompletedNow = quest.IsCompleted();
            
            Debug.Log($"Hit registered for quest: {quest.GetQuestTitle()} ({quest.GetHitCount()}/{quest.template.requiredHits})");
            
            if (!wasCompletedBefore && isCompletedNow)
            {
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
    }

    [ClientRpc]
    private void ClearAllQuestsClientRpc()
    {
        if (IsServer) return;
        ClearAllQuestsInternal();
    }

    // Event handlers
    private void HandleQuestStarted(NetworkedQuestInstance quest) { }
    private void HandleQuestCompleted(NetworkedQuestInstance quest) { }
    private void HandleQuestFailed(NetworkedQuestInstance quest) { }

    // Context Menu Debug Options
    [ContextMenu("Debug: Show Active Quests")]
    private void DebugShowActiveQuests()
    {
        Debug.Log($"=== ACTIVE QUESTS DEBUG (Total: {activeQuests.Count}) ===");
        Debug.Log($"Is Server: {IsServer}");
        
        foreach (var kvp in activeQuests)
        {
            var quest = kvp.Value;
            Debug.Log($"Quest {kvp.Key}: {quest.GetQuestTitle()}");
            Debug.Log($"  - Type: Collection={quest.IsCollectionBased()}, Time={quest.IsTimeBased()}");
            Debug.Log($"  - Progress: {quest.GetCollectedCount()}/{quest.template.collectionCount}");
            Debug.Log($"  - Status: Completed={quest.IsCompleted()}, Failed={quest.IsFailed()}");
            Debug.Log($"  - Time: {quest.GetTimeRemaining():F1}s remaining");
        }
    }

    [ContextMenu("Debug: Force Complete All Quests")]
    private void DebugForceCompleteAllQuests()
    {
        if (!IsServer) return;
        
        foreach (var quest in activeQuests.Values.ToList())
        {
            OnQuestCompleted?.Invoke(quest);
            NotifyQuestCompletedClientRpc(quest.GetQuestId());
        }
    }

    [ContextMenu("Debug: Force Fail All Quests")]
    private void DebugForceFailAllQuests()
    {
        if (!IsServer) return;
        
        foreach (var quest in activeQuests.Values.ToList())
        {
            OnQuestFailed?.Invoke(quest);
            NotifyQuestFailedClientRpc(quest.GetQuestId());
        }
    }

    [ContextMenu("Debug: Start All Available Quests")]
    private void DebugStartAllQuests()
    {
        foreach (var template in availableQuestTemplates)
        {
            StartQuest(template);
        }
    }
    
    
}