using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct QuestNetworkData : INetworkSerializable
{
    public float timeRemaining;
    public int collectedCount;
    public int hitCount;           // NEW: Track hits
    public bool isCompleted;
    public bool isFailed;
    public int templateId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref timeRemaining);
        serializer.SerializeValue(ref collectedCount);
        serializer.SerializeValue(ref hitCount);        // NEW
        serializer.SerializeValue(ref isCompleted);
        serializer.SerializeValue(ref isFailed);
        serializer.SerializeValue(ref templateId);
    }
}

public class NetworkedQuestInstance
{
    public QuestStructure template;
    public QuestNetworkData currentData;
    
    public System.Action<QuestNetworkData> OnDataChanged;
    
    private NetworkedQuestManager questManager;
    private int questId;

    public NetworkedQuestInstance(QuestStructure template, int questId, NetworkedQuestManager manager)
    {
        this.template = template;
        this.questId = questId;
        this.questManager = manager;
        
        currentData = new QuestNetworkData
        {
            timeRemaining = template.isTimeBased ? template.timeInMinute * 60f : 0f,
            collectedCount = 0,
            hitCount = 0,               // NEW
            isCompleted = false,
            isFailed = false,
            templateId = questId
        };
    }

    public void UpdateDataFromNetwork(QuestNetworkData newData)
    {
        currentData = newData;
        OnDataChanged?.Invoke(newData);
    }

    public void UpdateQuest(float deltaTime)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        bool wasModified = false;
        var data = currentData;

        if (data.isCompleted || data.isFailed)
            return;

        // Handle time-based logic
        if (template.isTimeBased)
        {
            data.timeRemaining -= deltaTime;
            wasModified = true;

            if (data.timeRemaining <= 0f)
            {
                data.timeRemaining = 0f;
                
                // Check if quest objectives are completed when time runs out
                bool objectivesComplete = CheckObjectivesComplete(data);
                
                if (objectivesComplete)
                {
                    data.isCompleted = true;
                }
                else
                {
                    data.isFailed = true;
                }
                
                wasModified = true;
            }
        }

        // Check if all objectives are completed (without time pressure)
        if (!data.isCompleted && !data.isFailed)
        {
            if (CheckObjectivesComplete(data))
            {
                data.isCompleted = true;
                wasModified = true;
            }
        }

        // Sync data if modified
        if (wasModified)
        {
            currentData = data;
            questManager.SyncQuestDataClientRpc(questId, data);
        }
    }

    private bool CheckObjectivesComplete(QuestNetworkData data)
    {
        bool collectionComplete = !template.isCollection || data.collectedCount >= template.collectionCount;
        bool hitsComplete = !template.isHitBased || data.hitCount >= template.requiredHits;
        
        return collectionComplete && hitsComplete;
    }

    public void CollectItem()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!template.isCollection || currentData.isCompleted || currentData.isFailed) return;

        var data = currentData;
        data.collectedCount++;
        
        // Check if quest is now complete
        if (CheckObjectivesComplete(data))
        {
            data.isCompleted = true;
        }

        currentData = data;
        questManager.SyncQuestDataClientRpc(questId, data);
    }

    public void RegisterHit()  // NEW METHOD
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!template.isHitBased || currentData.isCompleted || currentData.isFailed) return;

        var data = currentData;
        data.hitCount++;
        
        // Check if quest is now complete
        if (CheckObjectivesComplete(data))
        {
            data.isCompleted = true;
        }

        currentData = data;
        questManager.SyncQuestDataClientRpc(questId, data);
    }

    // Getters
    public bool IsTimeBased() => template.isTimeBased;
    public bool IsCollectionBased() => template.isCollection;
    public bool IsHitBased() => template.isHitBased;  // NEW

    public float GetProgressPercentage()
    {
        if (template.isCollection && template.isHitBased)
        {
            // Combined quest: average both progresses
            float collectionProgress = (float)currentData.collectedCount / template.collectionCount;
            float hitProgress = (float)currentData.hitCount / template.requiredHits;
            return Mathf.Clamp01((collectionProgress + hitProgress) / 2f);
        }
        else if (IsCollectionBased())
        {
            return Mathf.Clamp01((float)currentData.collectedCount / template.collectionCount);
        }
        else if (IsHitBased())
        {
            return Mathf.Clamp01((float)currentData.hitCount / template.requiredHits);
        }
        else if (IsTimeBased())
        {
            return Mathf.Clamp01(1f - (currentData.timeRemaining / (template.timeInMinute * 60f)));
        }

        return 0f;
    }

    public string GetQuestTitle() => template.questName;
    public string GetQuestDescription() => template.questDescription;

    public string GetProgressText()
    {
        string progressText = "";
        
        if (IsCollectionBased())
        {
            progressText += $"TOPLANAN: {currentData.collectedCount}/{template.collectionCount}";
        }
        
        if (IsHitBased())
        {
            if (!string.IsNullOrEmpty(progressText)) progressText += " | ";
            progressText += $"TEMIZLENEN: {currentData.hitCount}/{template.requiredHits}";
        }
        
        if (IsTimeBased())
        {
            if (!string.IsNullOrEmpty(progressText)) progressText += " | ";
            progressText += $"KALAN ZAMAN: {Mathf.Ceil(currentData.timeRemaining)}s";
        }
        
        return string.IsNullOrEmpty(progressText) ? "DEVAM EDIYOR" : progressText;
    }

    public bool IsCompleted() => currentData.isCompleted;
    public bool IsFailed() => currentData.isFailed;
    public float GetTimeRemaining() => currentData.timeRemaining;
    public int GetCollectedCount() => currentData.collectedCount;
    public int GetHitCount() => currentData.hitCount;  // NEW
    public int GetQuestId() => questId;

    public void Dispose()
    {
        OnDataChanged = null;
    }
}