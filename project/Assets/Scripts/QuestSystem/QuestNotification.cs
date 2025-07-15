using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuestNotification : MonoBehaviour
{
    [Header("Notification UI")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private Image notificationIcon;
    [SerializeField] private float displayDuration = 3f;

    [Header("Notification Icons")]
    [SerializeField] private Sprite questStartIcon;
    [SerializeField] private Sprite questCompleteIcon;
    [SerializeField] private Sprite questFailIcon;

    [Header("Colors")]
    [SerializeField] private Color startColor = Color.blue;
    [SerializeField] private Color completeColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private Coroutine currentNotification;

    private void Start()
    {
        // Subscribe to quest events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted += OnQuestStarted;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            QuestManager.Instance.OnQuestFailed += OnQuestFailed;
        }

        // Hide notification panel initially
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private void OnQuestStarted(QuestInstance quest)
    {
        ShowNotification($"New Quest: {quest.GetQuestTitle()}", questStartIcon, startColor);
    }

    private void OnQuestCompleted(QuestInstance quest)
    {
        ShowNotification($"Quest Completed: {quest.GetQuestTitle()}", questCompleteIcon, completeColor);
    }

    private void OnQuestFailed(QuestInstance quest)
    {
        ShowNotification($"Quest Failed: {quest.GetQuestTitle()}", questFailIcon, failColor);
    }

    private void ShowNotification(string message, Sprite icon, Color color)
    {
        // Stop any current notification
        if (currentNotification != null)
        {
            StopCoroutine(currentNotification);
        }

        // Start new notification
        currentNotification = StartCoroutine(DisplayNotification(message, icon, color));
    }

    private IEnumerator DisplayNotification(string message, Sprite icon, Color color)
    {
        // Setup notification
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.color = color;
        }

        if (notificationIcon != null && icon != null)
        {
            notificationIcon.sprite = icon;
            notificationIcon.color = color;
        }

        // Show notification
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);
            
            // Optional: Add slide-in animation
            StartCoroutine(SlideInAnimation());
        }

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration);

        // Optional: Add slide-out animation
        yield return StartCoroutine(SlideOutAnimation());

        // Hide notification
        if (notificationPanel != null)
            notificationPanel.SetActive(false);

        currentNotification = null;
    }

    private IEnumerator SlideInAnimation()
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            Vector3 startPos = rectTransform.localPosition;
            Vector3 targetPos = startPos;
            startPos.x += 300f; // Start from right
            
            rectTransform.localPosition = startPos;
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            
            rectTransform.localPosition = targetPos;
        }
    }

    private IEnumerator SlideOutAnimation()
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            Vector3 startPos = rectTransform.localPosition;
            Vector3 targetPos = startPos;
            targetPos.x += 300f; // Slide to right
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            QuestManager.Instance.OnQuestFailed -= OnQuestFailed;
        }
    }
}