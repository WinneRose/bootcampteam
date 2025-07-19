using UnityEngine;

public class QuestInstance
{
    public QuestStructure template;
    public float timeRemaining;
    public int collectedCount;
    public bool isCompleted;
    public bool isFailed;

    public QuestInstance(QuestStructure template)
    {
        this.template = template;
        if (template.isTimeBased)
            timeRemaining = template.timeInMinute * 60f;
        collectedCount = 0;
    }

    public void UpdateQuest(float deltaTime)
    {
        if (template.isTimeBased)
        {
            timeRemaining -= deltaTime;
            if (timeRemaining <= 0f && template.isCollection)
                isFailed = true;
            if (timeRemaining <= 0f)
                isCompleted = true;
        }
    }

    public void CollectItem()
    {
        if (!template.isCollection || isCompleted) return;

        collectedCount++;
        if (collectedCount >= template.collectionCount)
            isCompleted = true;
    }

    // ✅ Getters for external usage (clean access)

    public bool IsTimeBased() => template.isTimeBased;
    public bool IsCollectionBased() => template.isCollection;

    public float GetProgressPercentage()
    {
        if (IsCollectionBased())
            return Mathf.Clamp01((float)collectedCount / template.collectionCount);
        else if (IsTimeBased())
            return Mathf.Clamp01(1f - (timeRemaining / (template.timeInMinute * 60f)));

        return 0f;
    }

    public string GetQuestTitle() => template.questName;
    public string GetQuestDescription() => template.questDescription;

    public string GetProgressText()
    {
        if (IsCollectionBased())
            return $"{collectedCount} / {template.collectionCount}";
        else if (IsTimeBased())
            return $"{Mathf.Ceil(timeRemaining)}s left";

        return "Progress Unknown";
    }

    public bool IsCompleted() => isCompleted;
    public bool IsFailed() => isFailed;
}