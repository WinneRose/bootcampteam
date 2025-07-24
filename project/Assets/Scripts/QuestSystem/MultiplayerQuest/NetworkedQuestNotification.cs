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
    [SerializeField] private int maxQueuedNotifications = 5;

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

    private Queue<NotificationData> notificationQueue = new Queue<NotificationData>();
    private bool isShowingNotification = false;

    private struct NotificationData
    {
        public string message;
        public Sprite icon;
        public Color color;
        public AudioClip sound;
    }

    private void Start()
    {
        // Subscribe to networked quest events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted += OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed += OnQuestFailed;
            NetworkedQuestManager.Instance.OnQuestUpdated += OnQuestUpdated;
        }
        else
        {
            // If quest manager isn't ready yet, try again in a moment
            StartCoroutine(WaitForQuestManager());
        }

        // Hide notification panel initially
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private IEnumerator WaitForQuestManager()
    {
        while (NetworkedQuestManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Subscribe once the manager is available
        NetworkedQuestManager.Instance.OnQuestStarted += OnQuestStarted;
        NetworkedQuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
        NetworkedQuestManager.Instance.OnQuestFailed += OnQuestFailed;
        NetworkedQuestManager.Instance.OnQuestUpdated += OnQuestUpdated;
    }

    private void OnQuestStarted(NetworkedQuestInstance quest)
    {
        string message = $"New Quest: {quest.GetQuestTitle()}";
        
        // Add some variety to the message for multiplayer context
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            message = $"New Quest: {quest.GetQuestTitle()}";
        }
        
        QueueNotification(message, questStartIcon, startColor, questStartSound);
    }

    private void OnQuestCompleted(NetworkedQuestInstance quest)
    {
        string message = $"✅ Quest Completed: {quest.GetQuestTitle()}";
        QueueNotification(message, questCompleteIcon, completeColor, questCompleteSound);
    }

    private void OnQuestFailed(NetworkedQuestInstance quest)
    {
        string message = $"❌ Quest Failed: {quest.GetQuestTitle()}";
        QueueNotification(message, questFailIcon, failColor, questFailSound);
    }

    private void OnQuestUpdated(NetworkedQuestInstance quest)
    {
        // Only show update notifications for significant progress (like collection milestones)
        if (quest.IsCollectionBased())
        {
            int collected = quest.GetCollectedCount();
            int total = quest.template.collectionCount;
            
            // Show notification at 25%, 50%, 75% progress
            float progress = (float)collected / total;
            if (progress == 0.25f || progress == 0.5f || progress == 0.75f)
            {
                string message = $"📈 {quest.GetQuestTitle()}: {quest.GetProgressText()}";
                QueueNotification(message, questUpdateIcon, updateColor, null);
            }
        }
    }

    private void QueueNotification(string message, Sprite icon, Color color, AudioClip sound)
    {
        var notification = new NotificationData
        {
            message = message,
            icon = icon,
            color = color,
            sound = sound
        };

        // Limit queue size
        if (notificationQueue.Count >= maxQueuedNotifications)
        {
            notificationQueue.Dequeue();
        }

        notificationQueue.Enqueue(notification);

        // Start processing queue if not already showing notifications
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
            
            // Small delay between notifications
            if (notificationQueue.Count > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        isShowingNotification = false;
    }

    private IEnumerator DisplayNotification(NotificationData notification)
    {
        // Setup notification
        if (notificationText != null)
        {
            notificationText.text = notification.message;
            notificationText.color = notification.color;
        }

        if (notificationIcon != null && notification.icon != null)
        {
            notificationIcon.sprite = notification.icon;
            notificationIcon.color = notification.color;
        }

        // Play sound effect
        if (audioSource != null && notification.sound != null)
        {
            audioSource.PlayOneShot(notification.sound);
        }

        // Show notification with animation
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);
            yield return StartCoroutine(SlideInAnimation());
        }

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration);

        // Hide notification with animation
        yield return StartCoroutine(SlideOutAnimation());

        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private IEnumerator SlideInAnimation()
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
            {
                canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
            }

            Vector3 startPos = rectTransform.localPosition;
            Vector3 targetPos = startPos;
            
            // Start from right side
            startPos.x += 300f;
            rectTransform.localPosition = startPos;
            
            // Start transparent
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
            
            rectTransform.localPosition = targetPos;
            canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator SlideOutAnimation()
    {
        if (notificationPanel != null)
        {
            RectTransform rectTransform = notificationPanel.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            
            Vector3 startPos = rectTransform.localPosition;
            Vector3 targetPos = startPos;
            targetPos.x += 300f; // Slide to right
            
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
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (NetworkedQuestManager.Instance != null)
        {
            NetworkedQuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            NetworkedQuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            NetworkedQuestManager.Instance.OnQuestFailed -= OnQuestFailed;
            NetworkedQuestManager.Instance.OnQuestUpdated -= OnQuestUpdated;
        }
    }

    // Public method to manually trigger notifications (for testing or special cases)
    public void ShowCustomNotification(string message, Sprite icon = null, Color? color = null)
    {
        Color notificationColor = color ?? startColor;
        Sprite notificationIcon = icon ?? questStartIcon;
        
        QueueNotification(message, notificationIcon, notificationColor, null);
    }
}