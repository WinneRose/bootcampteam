using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class QuestTracker : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private int requiredCompletedQuests = 5;
    
    [Header("Events")]
    [SerializeField] private UnityEvent OnMilestoneReached;
    [SerializeField] private UnityEvent<int> OnQuestCountChanged;
    
    [Header("Quest Filter")]
    [SerializeField] private QuestType questTypeToTrack = QuestType.All;
    [SerializeField] private List<QuestStructure> specificQuestsToTrack = new List<QuestStructure>();
    [SerializeField] private List<string> questNamesToTrack = new List<string>();
    [SerializeField] private List<string> questTagsToTrack = new List<string>();

    public enum QuestType
    {
        All,
        CollectionOnly,
        HitBasedOnly,
        TimeBased,
        SpecificQuests,
        QuestNames,
        QuestTags
    }

    // Network synchronized variable for completed quests
    private NetworkVariable<int> completedQuests = new NetworkVariable<int>(0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);

    // Track which specific quests we've already counted to prevent duplicates
    private HashSet<string> countedQuestIds = new HashSet<string>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to quest manager events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
        }
        else
        {
            StartCoroutine(WaitForQuestManager());
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe from events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
        }
    }

    

    private System.Collections.IEnumerator WaitForQuestManager()
    {
        while (NetworkedQuestManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
    }

    private void OnQuestCompleted(NetworkedQuestInstance completedQuest)
    {
        // Only process on server
        if (!IsServer) return;
        
        // Check if this quest should be tracked
        if (!ShouldTrackQuest(completedQuest)) return;

        // Prevent duplicate counting
        string questIdentifier = $"{completedQuest.GetQuestTitle()}_{completedQuest.GetQuestId()}_{System.DateTime.Now.Ticks}";
        if (countedQuestIds.Contains(questIdentifier)) return;

        // Count this quest
        countedQuestIds.Add(questIdentifier);
        completedQuests.Value++;

        // Trigger quest count changed event
        TriggerQuestCountChangedClientRpc(completedQuests.Value);

        // Check if milestone reached
        if (completedQuests.Value >= requiredCompletedQuests)
        {
            TriggerMilestoneReachedClientRpc();
        }
    }

    private bool ShouldTrackQuest(NetworkedQuestInstance quest)
    {
        switch (questTypeToTrack)
        {
            case QuestType.All:
                return true;
                
            case QuestType.CollectionOnly:
                return quest.IsCollectionBased();
                
            case QuestType.HitBasedOnly:
                return quest.IsHitBased();
                
            case QuestType.TimeBased:
                return quest.IsTimeBased();
                
            case QuestType.SpecificQuests:
                return specificQuestsToTrack.Contains(quest.template);
                
            case QuestType.QuestNames:
                return questNamesToTrack.Any(name => 
                    quest.GetQuestTitle().Equals(name, System.StringComparison.OrdinalIgnoreCase));
                
            case QuestType.QuestTags:
                return questTagsToTrack.Any(tag => 
                    (quest.IsCollectionBased() && quest.template.collectionNameTag.Equals(tag, System.StringComparison.OrdinalIgnoreCase)) ||
                    (quest.IsHitBased() && (quest.template.hitTargetTag.Equals(tag, System.StringComparison.OrdinalIgnoreCase) || 
                                           quest.template.projectileTag.Equals(tag, System.StringComparison.OrdinalIgnoreCase))));
                
            default:
                return false;
        }
    }

 

    [ClientRpc]
    private void TriggerMilestoneReachedClientRpc()
    {
        OnMilestoneReached?.Invoke();
    }

    [ClientRpc]
    private void TriggerQuestCountChangedClientRpc(int newCount)
    {
        OnQuestCountChanged?.Invoke(newCount);
    }

    // Public getters
    public int GetCompletedQuests() => completedQuests.Value;
    public bool IsMilestoneReached() => completedQuests.Value >= requiredCompletedQuests;
    public QuestType GetTrackingType() => questTypeToTrack;
}