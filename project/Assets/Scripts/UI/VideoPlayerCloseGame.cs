using UnityEngine;
using UnityEngine.Video;
using System.Collections;

public class VideoPlayerCloseGame : MonoBehaviour
{
    [Header("Video Player Settings")]
    [SerializeField] private VideoPlayer videoPlayer;
    
    [Header("Close Settings")]
    [SerializeField] private float delayAfterVideo = 2f; // Delay before closing (in seconds)
    [SerializeField] private bool showCountdown = true; // Show countdown UI
    [SerializeField] private bool allowSkip = true; // Allow ESC to skip video
    
    [Header("UI References (Optional)")]
    [SerializeField] private UnityEngine.UI.Text countdownText; // Optional countdown display
    [SerializeField] private GameObject countdownPanel; // Optional countdown panel
    
    private bool videoFinished = false;
    private bool gameClosing = false;

    void Start()
    {
        // Get VideoPlayer component if not assigned
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }
        
        if (videoPlayer == null)
        {
            Debug.LogError("[VideoPlayerCloseGame] No VideoPlayer found!");
            return;
        }
        
        // Subscribe to video events
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
        
        // Start playing the video
        videoPlayer.Play();
        
        Debug.Log("[VideoPlayerCloseGame] Video started playing");
    }

    void Update()
    {
        // Allow skipping with ESC key
        if (allowSkip && Input.GetKeyDown(KeyCode.Escape) && !gameClosing)
        {
            Debug.Log("[VideoPlayerCloseGame] Video skipped with ESC");
            SkipToClose();
        }
        
        // Alternative skip keys
        if (allowSkip && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) && !gameClosing)
        {
            Debug.Log("[VideoPlayerCloseGame] Video skipped with Space/Enter");
            SkipToClose();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("[VideoPlayerCloseGame] Video finished playing");
        
        if (!gameClosing)
        {
            videoFinished = true;
            StartCoroutine(CloseGameAfterDelay());
        }
    }
    
    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[VideoPlayerCloseGame] Video error: {message}");
        
        // Close game even if video has error
        if (!gameClosing)
        {
            StartCoroutine(CloseGameAfterDelay());
        }
    }

    private void SkipToClose()
    {
        if (gameClosing) return;
        
        // Stop the video
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
        
        videoFinished = true;
        StartCoroutine(CloseGameAfterDelay());
    }

    private IEnumerator CloseGameAfterDelay()
    {
        gameClosing = true;
        
        Debug.Log($"[VideoPlayerCloseGame] Game will close in {delayAfterVideo} seconds");
        
        // Show countdown if enabled
        if (showCountdown && countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }
        
        // Countdown loop
        float remainingTime = delayAfterVideo;
        while (remainingTime > 0)
        {
            // Update countdown text
            if (countdownText != null)
            {
                countdownText.text = $"Game closing in {remainingTime:F0} seconds...";
            }
            
            // Log countdown
            if (remainingTime <= 5f) // Only log last 5 seconds
            {
                Debug.Log($"[VideoPlayerCloseGame] Closing in {remainingTime:F0}...");
            }
            
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
        }
        
        // Final message
        Debug.Log("[VideoPlayerCloseGame] Closing game now!");
        
        if (countdownText != null)
        {
            countdownText.text = "Closing game...";
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Close the game
        CloseGame();
    }

    private void CloseGame()
    {
        Debug.Log("[VideoPlayerCloseGame] Attempting to close game...");
        
#if UNITY_EDITOR
        // In the Unity Editor, stop playing
        UnityEditor.EditorApplication.isPlaying = false;
        Debug.Log("[VideoPlayerCloseGame] Stopped Unity Editor playmode");
#else
        // In a built game, quit the application
        Application.Quit();
        Debug.Log("[VideoPlayerCloseGame] Application.Quit() called");
#endif
    }

    // Public methods for external control
    public void ForceCloseGame()
    {
        Debug.Log("[VideoPlayerCloseGame] Force close requested");
        StopAllCoroutines();
        CloseGame();
    }
    
    public void SkipVideo()
    {
        Debug.Log("[VideoPlayerCloseGame] Skip video requested");
        SkipToClose();
    }
    
    public void SetCloseDelay(float newDelay)
    {
        delayAfterVideo = newDelay;
        Debug.Log($"[VideoPlayerCloseGame] Close delay set to {newDelay} seconds");
    }

    // Cleanup
    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}