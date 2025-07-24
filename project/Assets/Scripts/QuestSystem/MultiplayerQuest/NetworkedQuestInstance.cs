using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct QuestNetworkData : INetworkSerializable
{
    public float timeRemaining;
    public int collectedCount;
    public bool isCompleted;
    public bool isFailed;
    public int templateId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref timeRemaining);
        serializer.SerializeValue(ref collectedCount);
        serializer.SerializeValue(ref isCompleted);
        serializer.SerializeValue(ref isFailed);
        serializer.SerializeValue(ref templateId);
    }
}

public class NetworkedQuestInstance
{
    public QuestStructure template;
    public QuestNetworkData currentData;
    
    // Events for UI updates
    public System.Action<QuestNetworkData> OnDataChanged;
    
    private NetworkedQuestManager questManager;
    private int questId;

    public NetworkedQuestInstance(QuestStructure template, int questId, NetworkedQuestManager manager)
    {
        this.template = template;
        this.questId = questId;
        this.questManager = manager;
        
        // Initialize data
        currentData = new QuestNetworkData
        {
            timeRemaining = template.isTimeBased ? template.timeInMinute * 60f : 0f,
            collectedCount = 0,
            isCompleted = false,
            isFailed = false,
            templateId = questId
        };

        Debug.Log($"[NetworkedQuest] Created quest: {template.questName}, Collection: {template.isCollection}, Target: {template.collectionCount}");
    }

    public void UpdateDataFromNetwork(QuestNetworkData newData)
    {
        var oldData = currentData;
        currentData = newData;
        
        // Trigger event for UI updates
        OnDataChanged?.Invoke(newData);
        
        Debug.Log($"[NetworkedQuest] {template.questName} updated - Time: {newData.timeRemaining:F1}s, Collected: {newData.collectedCount}/{template.collectionCount}, Completed: {newData.isCompleted}, Failed: {newData.isFailed}");
    }

    public void UpdateQuest(float deltaTime)
    {
        // Only server updates the quest logic
        if (!NetworkManager.Singleton.IsServer) return;

        bool wasModified = false;
        var data = currentData;

        // Handle time-based logic
        if (template.isTimeBased && !data.isCompleted && !data.isFailed)
        {
            data.timeRemaining -= deltaTime;
            wasModified = true;

            if (data.timeRemaining <= 0f)
            {
                if (template.isCollection)
                {
                    // Time ran out on a collection quest = FAILED (unless already completed)
                    if (data.collectedCount >= template.collectionCount)
                    {
                        data.isCompleted = true;
                        Debug.Log($"[NetworkedQuest] {template.questName} COMPLETED - Collection finished just as time ran out!");
                    }
                    else
                    {
                        data.isFailed = true;
                        Debug.Log($"[NetworkedQuest] {template.questName} FAILED - Time ran out before collecting all items!");
                    }
                }
                else
                {
                    // Time ran out on a pure time quest = COMPLETED
                    data.isCompleted = true;
                    Debug.Log($"[NetworkedQuest] {template.questName} COMPLETED - Time finished!");
                }
                wasModified = true;
            }
        }

        // Sync data if modified
        if (wasModified)
        {
            currentData = data;
            // Notify clients of the update
            questManager.SyncQuestDataClientRpc(questId, data);
        }
    }

    public void CollectItem()
    {
        // Only server can modify quest state
        if (!NetworkManager.Singleton.IsServer) return;
        if (!template.isCollection || currentData.isCompleted || currentData.isFailed) return;

        var data = currentData;
        data.collectedCount++;
        
        Debug.Log($"[NetworkedQuest] {template.questName} - Collected item! Count: {data.collectedCount}/{template.collectionCount}");
        
        // Check for completion immediately after collecting
        if (data.collectedCount >= template.collectionCount)
        {
            data.isCompleted = true;
            Debug.Log($"[NetworkedQuest] {template.questName} COMPLETED - All items collected!");
        }

        currentData = data;
        // Notify clients of the update
        questManager.SyncQuestDataClientRpc(questId, data);
    }

    // Method to force check completion (useful for debugging)
 

    // Getters
    public bool IsTimeBased() => template.isTimeBased;
    public bool IsCollectionBased() => template.isCollection;

    public float GetProgressPercentage()
    {
        if (IsCollectionBased())
            return Mathf.Clamp01((float)currentData.collectedCount / template.collectionCount);
        else if (IsTimeBased())
            return Mathf.Clamp01(1f - (currentData.timeRemaining / (template.timeInMinute * 60f)));

        return 0f;
    }

    public string GetQuestTitle() => template.questName;
    public string GetQuestDescription() => template.questDescription;

    public string GetProgressText()
    {
        if (IsCollectionBased())
            return $"{currentData.collectedCount} / {template.collectionCount}";
        else if (IsTimeBased())
            return $"{Mathf.Ceil(currentData.timeRemaining)}s left";

        return "Progress Unknown";
    }

    public bool IsCompleted() => currentData.isCompleted;
    public bool IsFailed() => currentData.isFailed;
    public float GetTimeRemaining() => currentData.timeRemaining;
    public int GetCollectedCount() => currentData.collectedCount;
    public int GetQuestId() => questId;

    public void Dispose()
    {
        OnDataChanged = null;
    }
}