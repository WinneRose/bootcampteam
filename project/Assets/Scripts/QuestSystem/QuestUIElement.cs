using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestUIElement : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI questTitle;
    [SerializeField] private TextMeshProUGUI questDescription;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image progressBarFill;

    [Header("Status Colors")]
    [SerializeField] private Color inProgressColor = Color.yellow;
    [SerializeField] private Color completedColor = Color.green;
    [SerializeField] private Color failedColor = Color.red;

    private QuestInstance currentQuest;

    public void Setup(QuestInstance quest)
    {
        currentQuest = quest;
        
        // Set basic information
        if (questTitle != null)
            questTitle.text = quest.GetQuestTitle();
        
        if (questDescription != null)
            questDescription.text = quest.GetQuestDescription();

        // Set initial progress
        UpdateProgress(quest);
        
        // Set initial color
        SetStatusColor(inProgressColor);
    }

    public void UpdateProgress(QuestInstance quest)
    {
        if (quest == null) return;

        // Update progress text
        if (progressText != null)
            progressText.text = quest.GetProgressText();

        // Update progress bar
        if (progressBar != null)
        {
            float progress = quest.GetProgressPercentage();
            progressBar.value = progress;
        }

        // Update colors based on status
        if (quest.IsCompleted())
        {
            SetStatusColor(completedColor);
        }
        else if (quest.IsFailed())
        {
            SetStatusColor(failedColor);
        }
        else
        {
            SetStatusColor(inProgressColor);
        }
    }

    public void ShowCompleted()
    {
        SetStatusColor(completedColor);
        
        if (progressText != null)
            progressText.text = "COMPLETED!";
        
        if (progressBar != null)
            progressBar.value = 1f;

        // Add some visual feedback
        StartCoroutine(FlashEffect());
    }

    public void ShowFailed()
    {
        SetStatusColor(failedColor);
        
        if (progressText != null)
            progressText.text = "FAILED!";

        // Add some visual feedback
        StartCoroutine(FlashEffect());
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

    private System.Collections.IEnumerator FlashEffect()
    {
        // Simple flash effect
        for (int i = 0; i < 3; i++)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.white;
            }
            yield return new WaitForSeconds(0.1f);
            
            SetStatusColor(currentQuest.IsCompleted() ? completedColor : failedColor);
            yield return new WaitForSeconds(0.1f);
        }
    }
}