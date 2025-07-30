using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[System.Serializable]
public class SceneMusic
{
    [Tooltip("Scene name (must match exactly)")]
    public string sceneName;
    
    [Tooltip("Music to play in this scene")]
    public AudioClip musicClip;
    
    [Tooltip("Pitch for this scene's music")]
    [Range(0.25f, 3f)]
    public float pitch = 1f;
}

public class MusicManager : MonoBehaviour
{
    [Header("Scene Music List")]
    [Tooltip("List of scenes and their music")]
    public SceneMusic[] sceneMusicList;
    
    [Header("Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;
    
    [Tooltip("Fade time between tracks")]
    public float fadeTime = 1f;
    
    [Tooltip("Pitch transition time when changing scenes")]
    public float pitchTransitionTime = 0.5f;
    
    // Components
    private AudioSource audioSource;
    
    // Singleton
    private static MusicManager instance;
    public static MusicManager Instance => instance;
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Get or create AudioSource
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Configure AudioSource
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.volume = musicVolume;
            audioSource.pitch = 1f; // Default pitch
            
            Debug.Log("🎵 Music Manager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Subscribe to scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Play music for current scene
        PlayMusicForCurrentScene();
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"🎵 Scene loaded: {scene.name}");
        PlayMusicForCurrentScene();
    }
    
    private void PlayMusicForCurrentScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        
        // Find music for this scene
        SceneMusic sceneMusicData = GetSceneMusicData(currentSceneName);
        
        if (sceneMusicData != null && sceneMusicData.musicClip != null)
        {
            Debug.Log($"🎵 Playing music for scene '{currentSceneName}': {sceneMusicData.musicClip.name} (Pitch: {sceneMusicData.pitch})");
            PlayMusic(sceneMusicData.musicClip, sceneMusicData.pitch);
        }
        else
        {
            Debug.Log($"🎵 No music found for scene: {currentSceneName}");
            StopMusic();
        }
    }
    
    private SceneMusic GetSceneMusicData(string sceneName)
    {
        foreach (SceneMusic sceneMusic in sceneMusicList)
        {
            if (sceneMusic.sceneName == sceneName)
            {
                return sceneMusic;
            }
        }
        return null;
    }
    
    private AudioClip GetMusicForScene(string sceneName)
    {
        SceneMusic sceneData = GetSceneMusicData(sceneName);
        return sceneData?.musicClip;
    }
    
    private void PlayMusic(AudioClip newClip, float targetPitch = 1f)
    {
        // If same music is already playing, just adjust pitch if needed
        if (audioSource.clip == newClip && audioSource.isPlaying)
        {
            if (Mathf.Abs(audioSource.pitch - targetPitch) > 0.01f)
            {
                Debug.Log($"🎵 Adjusting pitch for '{newClip.name}' from {audioSource.pitch:F2} to {targetPitch:F2}");
                StartCoroutine(TransitionPitch(targetPitch));
            }
            else
            {
                Debug.Log($"🎵 Music '{newClip.name}' is already playing at correct pitch");
            }
            return;
        }
        
        // Start transition
        StartCoroutine(TransitionToMusic(newClip, targetPitch));
    }
    
    private IEnumerator TransitionToMusic(AudioClip newClip, float targetPitch)
    {
        // Fade out current music if playing
        if (audioSource.isPlaying)
        {
            yield return StartCoroutine(FadeOut());
        }
        
        // Set new clip, pitch, and fade in
        audioSource.clip = newClip;
        audioSource.pitch = targetPitch;
        audioSource.Play();
        yield return StartCoroutine(FadeIn());
        
        Debug.Log($"🎵 ✅ Now playing: {newClip.name} (Pitch: {targetPitch:F2})");
    }
    
    private IEnumerator TransitionPitch(float targetPitch)
    {
        float startPitch = audioSource.pitch;
        float timer = 0f;
        
        while (timer < pitchTransitionTime)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.pitch = Mathf.Lerp(startPitch, targetPitch, timer / pitchTransitionTime);
            yield return null;
        }
        
        audioSource.pitch = targetPitch;
        Debug.Log($"🎵 ✅ Pitch transition complete: {targetPitch:F2}");
    }
    
    private IEnumerator FadeOut()
    {
        float startVolume = audioSource.volume;
        float timer = 0f;
        
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeTime);
            yield return null;
        }
        
        audioSource.volume = 0f;
        audioSource.Stop();
    }
    
    private IEnumerator FadeIn()
    {
        audioSource.volume = 0f;
        float timer = 0f;
        
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(0f, musicVolume, timer / fadeTime);
            yield return null;
        }
        
        audioSource.volume = musicVolume;
    }
    
    private void StopMusic()
    {
        if (audioSource.isPlaying)
        {
            StartCoroutine(FadeOut());
            Debug.Log("🎵 Music stopped");
        }
    }
    
    // Public methods
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        audioSource.volume = musicVolume;
        Debug.Log($"🎵 Volume set to: {musicVolume}");
    }
    
    public void SetPitch(float pitch)
    {
        float clampedPitch = Mathf.Clamp(pitch, 0.25f, 3f);
        StartCoroutine(TransitionPitch(clampedPitch));
        Debug.Log($"🎵 Pitch manually set to: {clampedPitch:F2}");
    }
    
    public float GetCurrentPitch()
    {
        return audioSource.pitch;
    }
    
    // Debug methods
    [ContextMenu("Debug Current Music")]
    public void DebugCurrentMusic()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        SceneMusic currentMusicData = GetSceneMusicData(currentScene);
        
        Debug.Log("🎵 === Music Manager Debug ===");
        Debug.Log($"🎵 Current Scene: {currentScene}");
        Debug.Log($"🎵 Current Music: {(currentMusicData?.musicClip != null ? currentMusicData.musicClip.name : "None")}");
        Debug.Log($"🎵 Scene Pitch: {(currentMusicData != null ? currentMusicData.pitch.ToString("F2") : "N/A")}");
        Debug.Log($"🎵 Current Pitch: {audioSource.pitch:F2}");
        Debug.Log($"🎵 Is Playing: {audioSource.isPlaying}");
        Debug.Log($"🎵 Volume: {audioSource.volume}");
        
        Debug.Log($"🎵 Configured Scenes ({sceneMusicList.Length}):");
        foreach (SceneMusic sceneMusic in sceneMusicList)
        {
            Debug.Log($"🎵   - '{sceneMusic.sceneName}' => {(sceneMusic.musicClip != null ? sceneMusic.musicClip.name : "No Music")} (Pitch: {sceneMusic.pitch:F2})");
        }
    }
    
    [ContextMenu("Force Play Current Scene Music")]
    public void ForcePlayCurrentSceneMusic()
    {
        PlayMusicForCurrentScene();
    }
    
    [ContextMenu("Test Pitch Variations")]
    public void TestPitchVariations()
    {
        StartCoroutine(TestPitchSequence());
    }
    
    private IEnumerator TestPitchSequence()
    {
        float[] testPitches = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
        
        foreach (float pitch in testPitches)
        {
            Debug.Log($"🎵 Testing pitch: {pitch:F2}");
            yield return StartCoroutine(TransitionPitch(pitch));
            yield return new WaitForSeconds(2f);
        }
        
        // Return to normal pitch
        yield return StartCoroutine(TransitionPitch(1f));
        Debug.Log("🎵 Pitch test complete - returned to normal");
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}