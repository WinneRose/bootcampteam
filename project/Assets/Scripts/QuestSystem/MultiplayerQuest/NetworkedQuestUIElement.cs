using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkedQuestUIElement : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI questTitle;
    [SerializeField] private TextMeshProUGUI questDescription;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image progressBarFill;

    [Header("Status Colors")]
    [SerializeField] private Color inProgressColor = Color.yellow;
    [SerializeField] private Color completedColor = Color.green;
    [SerializeField] private Color failedColor = Color.red;
    [SerializeField] private Color urgentColor = Color.orange;

    private NetworkedQuestInstance currentQuest;
    private bool isPlayingCompletionEffect = false;
    private bool isPlayingFailureEffect = false;

    public void Setup(NetworkedQuestInstance quest)
    {
        currentQuest = quest;
        
        if (questTitle != null)
            questTitle.text = quest.GetQuestTitle();
        
        if (questDescription != null)
            questDescription.text = quest.GetQuestDescription();

        ConfigureUIForQuestType(quest);
        UpdateProgress(quest);
        SetStatusColor(inProgressColor);
    }

    private void ConfigureUIForQuestType(NetworkedQuestInstance quest)
    {
        bool showTimer = quest.IsTimeBased();
        
        if (timeText != null)
            timeText.gameObject.SetActive(showTimer);

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
    }

    public void UpdateProgress(NetworkedQuestInstance quest)
    {
        if (quest == null) return;

        UpdateProgressDisplay(quest);
        UpdateTimerDisplay(quest);
        UpdateStatusColors(quest);
    }

    private void UpdateProgressDisplay(NetworkedQuestInstance quest)
    {
        if (progressText != null)
        {
            if (quest.IsCollectionBased())
            {
                progressText.text = $"{quest.GetCollectedCount()} / {quest.template.collectionCount}";
            }
            else if (quest.IsTimeBased() && !quest.IsCollectionBased())
            {
                progressText.text = $"{Mathf.Ceil(quest.GetTimeRemaining())}s remaining";
            }
            else
            {
                progressText.text = quest.GetProgressText();
            }
        }

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
        
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        
        if (minutes > 0)
        {
            timeText.text = $"{minutes}:{seconds:00}";
        }
        else
        {
            timeText.text = $"{seconds}s";
        }
        
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
            statusColor = urgentColor;
        }
        else
        {
            statusColor = inProgressColor;
        }
        
        SetStatusColor(statusColor);
    }

    public void ShowCompleted()
    {
        // Prevent multiple completion effects
        if (isPlayingCompletionEffect) return;
        
        // Check if GameObject is active before starting coroutine
        if (!gameObject.activeInHierarchy)
        {
            // Fallback: Just update the UI without animation
            ShowCompletedImmediate();
            return;
        }

        isPlayingCompletionEffect = true;
        
        SetStatusColor(completedColor);
        
        if (progressText != null)
            progressText.text = "TAMAMLANDI!";
        
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "TAMAMLANDI!";
        
        if (progressBar != null)
            progressBar.value = progressBar.maxValue;

        // Safely start coroutine
        StartCoroutine(FlashEffect(completedColor, () => {
            isPlayingCompletionEffect = false;
        }));
    }

    public void ShowFailed()
    {
        // Prevent multiple failure effects
        if (isPlayingFailureEffect) return;
        
        // Check if GameObject is active before starting coroutine
        if (!gameObject.activeInHierarchy)
        {
            // Fallback: Just update the UI without animation
            ShowFailedImmediate();
            return;
        }

        isPlayingFailureEffect = true;
        
        SetStatusColor(failedColor);
        
        if (progressText != null)
            progressText.text = "BAŞARISIZ OLDU";
            
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "ZAMAN DOLDU";

        // Safely start coroutine
        StartCoroutine(FlashEffect(failedColor, () => {
            isPlayingFailureEffect = false;
        }));
    }

    private void ShowCompletedImmediate()
    {
        SetStatusColor(completedColor);
        
        if (progressText != null)
            progressText.text = "TAMAMLANDI!";
        
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "TAMAMLANDI!";
        
        if (progressBar != null)
            progressBar.value = progressBar.maxValue;
    }

    private void ShowFailedImmediate()
    {
        SetStatusColor(failedColor);
        
        if (progressText != null)
            progressText.text = "BAŞARISIZ OLDU";
            
        if (timeText != null && timeText.gameObject.activeSelf)
            timeText.text = "ZAMAN DOLDU";
    }

    private void SetStatusColor(Color color)
    {
        if (backgroundImage != null)
        {
            Color bgColor = color;
            bgColor.a = 0.3f;
            backgroundImage.color = bgColor;
        }

        if (progressBarFill != null)
        {
            progressBarFill.color = color;
        }
    }

    private System.Collections.IEnumerator FlashEffect(Color flashColor, System.Action onComplete = null)
    {
        // Double-check that we're still active when the coroutine runs
        if (!gameObject.activeInHierarchy)
        {
            onComplete?.Invoke();
            yield break;
        }

        Color originalColor = flashColor;
        
        // Flash effect
        for (int i = 0; i < 3; i++)
        {
            // Check if still active during each iteration
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                yield break;
            }

            SetStatusColor(Color.white);
            yield return new WaitForSeconds(0.15f);
            
            // Check again after wait
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                yield break;
            }
            
            SetStatusColor(originalColor);
            yield return new WaitForSeconds(0.15f);
        }

        onComplete?.Invoke();
    }

    // Context Menu Debug Options
    [ContextMenu("Debug: Show Quest Info")]
    private void DebugShowQuestInfo()
    {
        if (currentQuest != null)
        {
            Debug.Log($"=== QUEST UI DEBUG INFO ===");
            Debug.Log($"Quest Title: {currentQuest.GetQuestTitle()}");
            Debug.Log($"Quest Description: {currentQuest.GetQuestDescription()}");
            Debug.Log($"Collection Based: {currentQuest.IsCollectionBased()}");
            Debug.Log($"Time Based: {currentQuest.IsTimeBased()}");
            Debug.Log($"Progress: {currentQuest.GetCollectedCount()}/{currentQuest.template.collectionCount}");
            Debug.Log($"Time Remaining: {currentQuest.GetTimeRemaining():F1}s");
            Debug.Log($"Completed: {currentQuest.IsCompleted()}");
            Debug.Log($"Failed: {currentQuest.IsFailed()}");
            Debug.Log($"GameObject Active: {gameObject.activeInHierarchy}");
            Debug.Log($"Is Playing Completion Effect: {isPlayingCompletionEffect}");
            Debug.Log($"Is Playing Failure Effect: {isPlayingFailureEffect}");
        }
        else
        {
            Debug.Log("No current quest assigned to this UI element!");
        }
    }

    [ContextMenu("Debug: Test Completion Animation")]
    private void DebugTestCompletion()
    {
        ShowCompleted();
    }

    [ContextMenu("Debug: Test Failure Animation")]
    private void DebugTestFailure()
    {
        ShowFailed();
    }

    [ContextMenu("Debug: Test Immediate Completion")]
    private void DebugTestImmediateCompletion()
    {
        ShowCompletedImmediate();
    }

    [ContextMenu("Debug: Test Immediate Failure")]
    private void DebugTestImmediateFailure()
    {
        ShowFailedImmediate();
    }
}