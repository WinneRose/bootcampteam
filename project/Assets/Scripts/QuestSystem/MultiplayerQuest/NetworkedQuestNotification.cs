using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NetworkedQuestNotification : MonoBehaviour
{
    [Header("Notification UI")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private Image notificationIcon;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private int maxQueuedNotifications = 10;
    [SerializeField] private float delayBetweenNotifications = 0.8f;

    [Header("Notification Icons")]
    [SerializeField] private Sprite questStartIcon;
    [SerializeField] private Sprite questCompleteIcon;
    [SerializeField] private Sprite questFailIcon;
    [SerializeField] private Sprite questUpdateIcon;

    [Header("Colors")]
    [SerializeField] private Color startColor = Color.blue;
    [SerializeField] private Color completeColor = Color.green;
    [SerializeField] private Color failColor = Color.red;
    [SerializeField] private Color updateColor = Color.yellow;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip questStartSound;
    [SerializeField] private AudioClip questCompleteSound;
    [SerializeField] private AudioClip questFailSound;

    [Header("Multiple Quest Handling")]
    [SerializeField] private bool batchSimilarNotifications = true;
    [SerializeField] private float batchTimeWindow = 1f;

    // Position tracking
    private Vector3 originalPosition;
    private bool hasStoredOriginalPosition = false;

    // Queue management
    private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
    private bool isShowingNotification = false;
    private Coroutine queueProcessorCoroutine;
    private List<NotificationData> pendingBatch = new List<NotificationData>();
    private float lastNotificationTime = 0f;

    private struct NotificationData
    {
        public string message;
        public Sprite icon;
        public Color color;
        public AudioClip sound;
        public NotificationType type;
        public string questTitle;
        public float timestamp;
    }

    private enum NotificationType
    {
        QuestStart,
        QuestComplete,
        QuestFail,
        QuestUpdate,
        Custom
    }

    private void Start()
    {
        // Subscribe to networked quest events
        if (NetworkedQuestManager.Instance != null)
        {
            SubscribeToQuestEvents();
        }
        else
        {
            StartCoroutine(WaitForQuestManager());
        }

        // Hide notification panel initially and store original position
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
            StoreOriginalPosition();
        }

        // Validate UI components
        ValidateUIComponents();
    }

    private void StoreOriginalPosition()
    {
        if (notificationPanel != null && !hasStoredOriginalPosition)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalPosition = rectTransform.localPosition;
                hasStoredOriginalPosition = true;
            }
        }
    }

    private void ValidateUIComponents()
    {
        if (notificationText == null)
            Debug.LogError("[QuestNotification] Notification text component is not assigned!");
        
        if (notificationIcon == null)
            Debug.LogWarning("[QuestNotification] Notification icon component is not assigned!");
        
        if (questStartIcon == null)
            Debug.LogWarning("[QuestNotification] Quest start icon is not assigned!");
    }

    private void SubscribeToQuestEvents()
    {
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted += OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed += OnQuestFailed;
            NetworkedQuestManager.Instance.OnQuestUpdated += OnQuestUpdated;
        }
    }

    private IEnumerator WaitForQuestManager()
    {
        float waitTime = 0f;
        
        while (NetworkedQuestManager.Instance == null && waitTime < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (NetworkedQuestManager.Instance != null)
        {
            SubscribeToQuestEvents();
        }
    }

    private void OnQuestStarted(NetworkedQuestInstance quest)
    {
        string questTitle = quest.GetQuestTitle();
        string message = CreateQuestStartMessage(quest);
        
        var notification = new NotificationData
        {
            message = message,
            icon = questStartIcon,
            color = startColor,
            sound = questStartSound,
            type = NotificationType.QuestStart,
            questTitle = questTitle,
            timestamp = Time.time
        };

        QueueNotification(notification);
    }

    private void OnQuestCompleted(NetworkedQuestInstance quest)
    {
        if (quest == null)
        {
            Debug.LogError("[QuestNotification] Quest is null in OnQuestCompleted!");
            return;
        }

        string questTitle = quest.GetQuestTitle();
        if (string.IsNullOrEmpty(questTitle))
        {
            questTitle = "Unknown Quest";
        }

        string message = CreateQuestCompleteMessage(quest);
        
        var notification = new NotificationData
        {
            message = message,
            icon = questCompleteIcon,
            color = completeColor,
            sound = questCompleteSound,
            type = NotificationType.QuestComplete,
            questTitle = questTitle,
            timestamp = Time.time
        };

        QueueNotification(notification);
    }

    private void OnQuestFailed(NetworkedQuestInstance quest)
    {
        if (quest == null)
        {
            Debug.LogError("[QuestNotification] Quest is null in OnQuestFailed!");
            return;
        }

        string questTitle = quest.GetQuestTitle();
        if (string.IsNullOrEmpty(questTitle))
        {
            questTitle = "Unknown Quest";
        }

        string message = CreateQuestFailMessage(quest);
        
        var notification = new NotificationData
        {
            message = message,
            icon = questFailIcon,
            color = failColor,
            sound = questFailSound,
            type = NotificationType.QuestFail,
            questTitle = questTitle,
            timestamp = Time.time
        };

        QueueNotification(notification);
    }

    private void OnQuestUpdated(NetworkedQuestInstance quest)
    {
        if (quest == null) return;

        // Only show update notifications for significant progress
        if (quest.IsCollectionBased())
        {
            int collected = quest.GetCollectedCount();
            int total = quest.template.collectionCount;
            
            if (total > 0)
            {
                float progress = (float)collected / total;
                
                // Show notification at 25%, 50%, 75% progress
                if (total >= 4 && (Mathf.Approximately(progress, 0.25f) || 
                                 Mathf.Approximately(progress, 0.5f) || 
                                 Mathf.Approximately(progress, 0.75f)))
                {
                    string message = $"📈 Progress Update!\n{quest.GetQuestTitle()}: {collected}/{total} {quest.template.collectionNameTag}s";
                    
                    var notification = new NotificationData
                    {
                        message = message,
                        icon = questUpdateIcon,
                        color = updateColor,
                        sound = null,
                        type = NotificationType.QuestUpdate,
                        questTitle = quest.GetQuestTitle(),
                        timestamp = Time.time
                    };
                    
                    QueueNotification(notification);
                }
            }
        }
    }

    private string CreateQuestStartMessage(NetworkedQuestInstance quest)
    {
        string message = $"🎯 New Quest: {quest.GetQuestTitle()}";
        
        if (quest.IsCollectionBased() && quest.IsTimeBased())
        {
            message += $"\n📦 Collect {quest.template.collectionCount} {quest.template.collectionNameTag}s";
            message += $"\n⏱️ Time Limit: {quest.template.timeInMinute:F0} minutes";
        }
        else if (quest.IsCollectionBased())
        {
            message += $"\n📦 Collect {quest.template.collectionCount} {quest.template.collectionNameTag}s";
        }
        else if (quest.IsTimeBased())
        {
            message += $"\n⏱️ Complete in {quest.template.timeInMinute:F0} minutes";
        }
        
        return message;
    }

    private string CreateQuestCompleteMessage(NetworkedQuestInstance quest)
    {
        string message = $"🎉 Quest Completed!\n✅ {quest.GetQuestTitle()}";
        
        if (quest.IsCollectionBased())
        {
            message += $"\n📦 Collected all {quest.template.collectionCount} {quest.template.collectionNameTag}s!";
        }
        
        if (quest.IsTimeBased())
        {
            float timeUsed = (quest.template.timeInMinute * 60f) - quest.GetTimeRemaining();
            message += $"\n⏱️ Completed in {timeUsed:F1} seconds!";
        }
        
        return message;
    }

    private string CreateQuestFailMessage(NetworkedQuestInstance quest)
    {
        string message = $"💥 Quest Failed!\n❌ {quest.GetQuestTitle()}";
        
        if (quest.IsCollectionBased() && quest.IsTimeBased())
        {
            int collected = quest.GetCollectedCount();
            int needed = quest.template.collectionCount;
            message += $"\n⏰ Time ran out! Only collected {collected}/{needed} {quest.template.collectionNameTag}s";
        }
        else if (quest.IsTimeBased())
        {
            message += "\n⏰ Time ran out!";
        }
        
        return message;
    }

    private void QueueNotification(NotificationData notification)
    {
        // Handle batching for multiple similar notifications
        if (batchSimilarNotifications && notification.type == NotificationType.QuestStart)
        {
            float timeSinceLastNotification = Time.time - lastNotificationTime;
            
            if (timeSinceLastNotification < batchTimeWindow)
            {
                pendingBatch.Add(notification);
                
                // Start or restart the batch timer
                if (queueProcessorCoroutine != null)
                {
                    StopCoroutine(queueProcessorCoroutine);
                }
                queueProcessorCoroutine = StartCoroutine(ProcessBatchAfterDelay());
                return;
            }
            else if (pendingBatch.Count > 0)
            {
                ProcessPendingBatch();
            }
        }

        // Add to queue
        if (notificationQueue.Count >= maxQueuedNotifications)
        {
            notificationQueue.Dequeue();
        }

        notificationQueue.Enqueue(notification);
        lastNotificationTime = Time.time;

        // Start processing queue if not already showing notifications
        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessBatchAfterDelay()
    {
        yield return new WaitForSeconds(batchTimeWindow);
        
        if (pendingBatch.Count > 0)
        {
            ProcessPendingBatch();
        }
    }

    private void ProcessPendingBatch()
    {
        if (pendingBatch.Count == 0) return;

        if (pendingBatch.Count == 1)
        {
            notificationQueue.Enqueue(pendingBatch[0]);
        }
        else
        {
            string batchMessage = $"🎯 {pendingBatch.Count} New Quests Started!\n";
            
            for (int i = 0; i < pendingBatch.Count && i < 3; i++)
            {
                batchMessage += $"• {pendingBatch[i].questTitle}\n";
            }
            
            if (pendingBatch.Count > 3)
            {
                batchMessage += $"• ... and {pendingBatch.Count - 3} more!";
            }

            var batchNotification = new NotificationData
            {
                message = batchMessage,
                icon = questStartIcon,
                color = startColor,
                sound = questStartSound,
                type = NotificationType.QuestStart,
                questTitle = "Multiple Quests",
                timestamp = Time.time
            };

            notificationQueue.Enqueue(batchNotification);
        }

        pendingBatch.Clear();

        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;

        while (notificationQueue.Count > 0)
        {
            var notification = notificationQueue.Dequeue();
            yield return StartCoroutine(DisplayNotification(notification));
            
            if (notificationQueue.Count > 0)
            {
                yield return new WaitForSeconds(delayBetweenNotifications);
            }
        }

        isShowingNotification = false;
    }

    private IEnumerator DisplayNotification(NotificationData notification)
    {
        // Validate UI components before showing
        if (notificationPanel == null)
        {
            Debug.LogError("[QuestNotification] Cannot display notification - panel is null!");
            yield break;
        }

        if (notificationText == null)
        {
            Debug.LogError("[QuestNotification] Cannot display notification - text component is null!");
            yield break;
        }

        // Store original position on first use if not already stored
        if (!hasStoredOriginalPosition)
        {
            StoreOriginalPosition();
        }

        // Setup notification
        notificationText.text = notification.message;
        notificationText.color = notification.color;

        if (notificationIcon != null)
        {
            if (notification.icon != null)
            {
                notificationIcon.sprite = notification.icon;
                notificationIcon.color = notification.color;
                notificationIcon.gameObject.SetActive(true);
            }
            else
            {
                notificationIcon.gameObject.SetActive(false);
            }
        }

        // Play sound effect
        if (audioSource != null && notification.sound != null)
        {
            audioSource.PlayOneShot(notification.sound);
        }

        // Show notification with animation
        notificationPanel.SetActive(true);
        yield return StartCoroutine(SlideInAnimation());

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration);

        // Hide notification with animation
        yield return StartCoroutine(SlideOutAnimation());
        notificationPanel.SetActive(false);
    }

    private IEnumerator SlideInAnimation()
    {
        if (notificationPanel == null) yield break;

        RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
        
        if (rectTransform == null)
        {
            Debug.LogError("[QuestNotification] No RectTransform found on notification panel!");
            yield break;
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
        }

        // Use the stored original position as target
        Vector3 targetPos = hasStoredOriginalPosition ? originalPosition : rectTransform.localPosition;
        Vector3 startPos = targetPos;
        
        // Start from off-screen right (relative to canvas)
        startPos.x = targetPos.x + 400f;
        
        // Set initial state
        rectTransform.localPosition = startPos;
        canvasGroup.alpha = 0f;
        
        float duration = 0.4f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            
            rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            canvasGroup.alpha = t;
            
            yield return null;
        }
        
        // Ensure final position is exact
        rectTransform.localPosition = targetPos;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator SlideOutAnimation()
    {
        if (notificationPanel == null) yield break;

        RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
        
        if (rectTransform == null) yield break;
        
        Vector3 startPos = rectTransform.localPosition;
        Vector3 targetPos = startPos;
        targetPos.x = startPos.x + 400f;
        
        float duration = 0.4f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            
            rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - t;
            }
            
            yield return null;
        }
        
        // Reset to original position for next time (but keep it hidden)
        if (hasStoredOriginalPosition)
        {
            rectTransform.localPosition = originalPosition;
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    // Public method to manually set the notification position
    public void SetNotificationPosition(Vector3 position)
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localPosition = position;
                originalPosition = position;
                hasStoredOriginalPosition = true;
            }
        }
    }

    // Method to reset the notification position if needed
    [ContextMenu("Reset Notification Position")]
    public void ResetNotificationPosition()
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            if (rectTransform != null && hasStoredOriginalPosition)
            {
                rectTransform.localPosition = originalPosition;
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up coroutines
        if (queueProcessorCoroutine != null)
        {
            StopCoroutine(queueProcessorCoroutine);
        }

        // Unsubscribe from events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed -= OnQuestFailed;
            NetworkedQuestManager.Instance.OnQuestUpdated -= OnQuestUpdated;
        }
    }

    // Debug and test methods
    [ContextMenu("Test Multiple Notifications")]
    public void TestMultipleNotifications()
    {
        for (int i = 1; i <= 5; i++)
        {
            ShowCustomNotification($"🎯 Test Quest {i}\n📦 Collect {i * 2} coins!", questStartIcon, startColor);
        }
    }

    [ContextMenu("Test Notification Queue")]
    public void TestNotificationQueue()
    {
        ShowCustomNotification("🎯 Quest 1 Started!", questStartIcon, startColor);
        ShowCustomNotification("🎉 Quest 2 Completed!", questCompleteIcon, completeColor);
        ShowCustomNotification("💥 Quest 3 Failed!", questFailIcon, failColor);
        ShowCustomNotification("📈 Quest 4 Progress!", questUpdateIcon, updateColor);
    }

    [ContextMenu("Test Single Notification")]
    public void TestSingleNotification()
    {
        ShowCustomNotification("🧪 Single Test Notification!\nThis is a test message.", questStartIcon, startColor);
    }

    public void ShowCustomNotification(string message, Sprite icon = null, Color? color = null)
    {
        Color notificationColor = color ?? startColor;
        Sprite notificationIcon = icon ?? questStartIcon;
        
        var notification = new NotificationData
        {
            message = message,
            icon = notificationIcon,
            color = notificationColor,
            sound = null,
            type = NotificationType.Custom,
            questTitle = "Custom",
            timestamp = Time.time
        };
        
        QueueNotification(notification);
    }

    // Public method to get queue status
    public string GetQueueStatus()
    {
        return $"Queue: {notificationQueue.Count}, Batch: {pendingBatch.Count}, Showing: {isShowingNotification}";
    }
}