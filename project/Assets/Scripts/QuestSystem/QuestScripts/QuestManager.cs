using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

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

    [SerializeField] private List<QuestInstance> activeQuests = new List<QuestInstance>();
    private HashSet<QuestInstance> questsBeingRemoved = new HashSet<QuestInstance>();

    public event Action<QuestInstance> OnQuestStarted;
    public event Action<QuestInstance> OnQuestCompleted;
    public event Action<QuestInstance> OnQuestFailed;

    private void Start()
    {
        OnQuestStarted += HandleQuestStarted;
        OnQuestCompleted += HandleQuestCompleted;
        OnQuestFailed += HandleQuestFailed;
    }

    private void Update()
    {
        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = activeQuests[i];

            // Skip if already completed or failed
            if (quest.IsCompleted() || quest.IsFailed())
                continue;

            quest.UpdateQuest(Time.deltaTime);

            if (quest.IsFailed())
            {
                OnQuestFailed?.Invoke(quest);
                StartCoroutine(RemoveQuestAfterDelay(quest, 2.5f));
            }
            else if (quest.IsCompleted())
            {
                OnQuestCompleted?.Invoke(quest);
                StartCoroutine(RemoveQuestAfterDelay(quest, 2.5f));
            }
        }
    }

    private IEnumerator RemoveQuestAfterDelay(QuestInstance quest, float delay)
    {
        if (questsBeingRemoved.Contains(quest))
            yield break;

        questsBeingRemoved.Add(quest);

        Debug.Log($"[QuestManager] Scheduled to remove: {quest.GetQuestTitle()} in {delay}s");

        yield return new WaitForSeconds(delay);

        if (activeQuests.Contains(quest))
        {
            activeQuests.Remove(quest);
            Debug.Log($"[QuestManager] Removed quest: {quest.GetQuestTitle()}");
        }

        questsBeingRemoved.Remove(quest);
    }

    /// <summary>
    /// Start a new quest from a ScriptableObject template
    /// </summary>
    public void StartQuest(QuestStructure questTemplate)
    {
        if (activeQuests.Exists(q => q.template == questTemplate))
        {
            Debug.LogWarning($"[QuestManager] Quest already started: {questTemplate.name}");
            return;
        }

        QuestInstance instance = new QuestInstance(questTemplate);
        activeQuests.Add(instance);
        OnQuestStarted?.Invoke(instance);
    }

    /// <summary>
    /// Add a pre-created QuestInstance
    /// </summary>
    public void StartQuestInstance(QuestInstance instance)
    {
        if (!activeQuests.Contains(instance))
        {
            activeQuests.Add(instance);
            OnQuestStarted?.Invoke(instance);
        }
    }

    public QuestInstance GetQuestInstance(QuestStructure template)
    {
        return activeQuests.Find(q => q.template == template);
    }

    public List<QuestInstance> GetActiveQuests()
    {
        return new List<QuestInstance>(activeQuests); // return copy for safety
    }

    public void ReportItemCollected(string tag)
    {
        foreach (var quest in activeQuests)
        {
            if (quest.IsCollectionBased() && quest.template.collectionNameTag == tag && !quest.IsCompleted())
            {
                quest.CollectItem();
                Debug.Log($"[QuestManager] Item collected: {tag} ({quest.GetProgressText()})");
            }
        }
    }

    public void ClearAllQuests()
    {
        activeQuests.Clear();
        questsBeingRemoved.Clear();
        Debug.Log("[QuestManager] All quests cleared.");
    }

    // Optional logging
    private void HandleQuestStarted(QuestInstance quest)
    {
        Debug.Log($"[QuestManager] Started quest: {quest.GetQuestTitle()}");
    }

    private void HandleQuestCompleted(QuestInstance quest)
    {
        Debug.Log($"[QuestManager] Completed quest: {quest.GetQuestTitle()}");
    }

    private void HandleQuestFailed(QuestInstance quest)
    {
        Debug.Log($"[QuestManager] Failed quest: {quest.GetQuestTitle()}");
    }
}
