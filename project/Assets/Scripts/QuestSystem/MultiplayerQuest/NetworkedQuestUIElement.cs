using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkedQuestUIElement : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI questTitle;
    [SerializeField] private TextMeshProUGUI questDescription;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI timeText;        // NEW: Time display
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image progressBarFill;

    [Header("Status Colors")]
    [SerializeField] private Color inProgressColor = Color.yellow;
    [SerializeField] private Color completedColor = Color.green;
    [SerializeField] private Color failedColor = Color.red;
    [SerializeField] private Color urgentColor = Color.orange;  // NEW: For low time warning

    private NetworkedQuestInstance currentQuest;

    public void Setup(NetworkedQuestInstance quest)
    {
        currentQuest = quest;
        
        // Set basic information
        if (questTitle != null)
            questTitle.text = quest.GetQuestTitle();
        
        if (questDescription != null)
            questDescription.text = quest.GetQuestDescription();

        // Configure UI based on quest type
        ConfigureUIForQuestType(quest);

        // Set initial progress
        UpdateProgress(quest);
        
        // Set initial color
        SetStatusColor(inProgressColor);
    }

    private void ConfigureUIForQuestType(NetworkedQuestInstance quest)
    {
        bool showTimer = quest.IsTimeBased();
        
        // Show/hide timer text
        if (timeText != null)
            timeText.gameObject.SetActive(showTimer);

        // Configure progress bar based on quest type
        if (progressBar != null)
        {
            if (quest.IsCollectionBased())
            {
                progressBar.maxValue = quest.template.collectionCount;
                progressBar.wholeNumbers = true;
                progressBar.value = 0;
            }
            else if (quest.IsTimeBased())
            {
                progressBar.maxValue = 1f;
                progressBar.wholeNumbers = false;
                progressBar.value = 0f;
            }
        }

        Debug.Log($"[QuestUI] Configured for quest: {quest.GetQuestTitle()}, " +
                 $"Collection: {quest.IsCollectionBased()}, " +
                 $"Timed: {quest.IsTimeBased()}, " +
                 $"Timer Visible: {showTimer}");
    }

    public void UpdateProgress(NetworkedQuestInstance quest)
    {
        if (quest == null) return;

        // Update progress text and bar
        UpdateProgressDisplay(quest);
        
        // Update timer display and bar
        UpdateTimerDisplay(quest);

        // Update colors based on status
        UpdateStatusColors(quest);
    }

    private void UpdateProgressDisplay(NetworkedQuestInstance quest)
    {
        // Update progress text
        if (progressText != null)
        {
            if (quest.IsCollectionBased())
            {
                progressText.text = $"{quest.GetCollectedCount()} / {quest.template.collectionCount}";
            }
            else if (quest.IsTimeBased() && !quest.IsCollectionBased())
            {
                // Pure time quest
                progressText.text = $"{Mathf.Ceil(quest.GetTimeRemaining())}s remaining";
            }
            else
            {
                progressText.text = quest.GetProgressText();
            }
        }

        // Update progress bar
        if (progressBar != null)
        {
            if (quest.IsCollectionBased())
            {
                progressBar.value = quest.GetCollectedCount();
            }
            else
            {
                progressBar.value = quest.GetProgressPercentage();
            }
        }
    }

    private void UpdateTimerDisplay(NetworkedQuestInstance quest)
    {
        if (!quest.IsTimeBased() || timeText == null) return;

        float timeRemaining = quest.GetTimeRemaining();
        
        // Update time text with nice formatting
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        
        if (minutes > 0)
        {
            timeText.text = $"⏰ {minutes}:{seconds:00}";
        }
        else
        {
            timeText.text = $"⏰ {seconds}s";
        }
        
        // Change color when time is low
        if (timeRemaining <= 30f && !quest.IsCompleted())
        {
            timeText.color = urgentColor;
        }
        else
        {
            timeText.color = Color.white;
        }
    }

    private void UpdateStatusColors(NetworkedQuestInstance quest)
    {
        Color statusColor;
        
        if (quest.IsCompleted())
        {
            statusColor = completedColor;
        }
        else if (quest.IsFailed())
        {
            statusColor = failedColor;
        }
        else if (quest.IsTimeBased() && quest.GetTimeRemaining() <= 30f)
        {
            statusColor = urgentColor;  // Urgent when time is low
        }
        else
        {
            statusColor = inProgressColor;
        }
        
        SetStatusColor(statusColor);
    }

    public void ShowCompleted()
    {
        SetStatusColor(completedColor);
        
        if (progressText != null)
            progressText.text = "COMPLETED! ✅";
        
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "⏰ DONE!";
        
        if (progressBar != null)
            progressBar.value = progressBar.maxValue;

        StartCoroutine(FlashEffect(completedColor));
    }

    public void ShowFailed()
    {
        SetStatusColor(failedColor);
        
        if (progressText != null)
            progressText.text = "FAILED! ❌";
            
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "⏰ TIME UP!";

        StartCoroutine(FlashEffect(failedColor));
    }

    private void SetStatusColor(Color color)
    {
        if (backgroundImage != null)
        {
            Color bgColor = color;
            bgColor.a = 0.3f; // Make background semi-transparent
            backgroundImage.color = bgColor;
        }

        if (progressBarFill != null)
        {
            progressBarFill.color = color;
        }
    }

    private System.Collections.IEnumerator FlashEffect(Color flashColor)
    {
        Color originalColor = flashColor;
        
        // Flash effect
        for (int i = 0; i < 3; i++)
        {
            SetStatusColor(Color.white);
            yield return new WaitForSeconds(0.15f);
            
            SetStatusColor(originalColor);
            yield return new WaitForSeconds(0.15f);
        }
    }
}