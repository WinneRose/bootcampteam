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
        AudioClip musicToPlay = GetMusicForScene(currentSceneName);
        
        if (musicToPlay != null)
        {
            Debug.Log($"🎵 Playing music for scene '{currentSceneName}': {musicToPlay.name}");
            PlayMusic(musicToPlay);
        }
        else
        {
            Debug.Log($"🎵 No music found for scene: {currentSceneName}");
            StopMusic();
        }
    }
    
    private AudioClip GetMusicForScene(string sceneName)
    {
        foreach (SceneMusic sceneMusic in sceneMusicList)
        {
            if (sceneMusic.sceneName == sceneName)
            {
                return sceneMusic.musicClip;
            }
        }
        return null;
    }
    
    private void PlayMusic(AudioClip newClip)
    {
        // If same music is already playing, don't restart
        if (audioSource.clip == newClip && audioSource.isPlaying)
        {
            Debug.Log($"🎵 Music '{newClip.name}' is already playing");
            return;
        }
        
        // Start transition
        StartCoroutine(TransitionToMusic(newClip));
    }
    
    private IEnumerator TransitionToMusic(AudioClip newClip)
    {
        // Fade out current music if playing
        if (audioSource.isPlaying)
        {
            yield return StartCoroutine(FadeOut());
        }
        
        // Set new clip and fade in
        audioSource.clip = newClip;
        audioSource.Play();
        yield return StartCoroutine(FadeIn());
        
        Debug.Log($"🎵 ✅ Now playing: {newClip.name}");
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
    
    // Debug methods
    [ContextMenu("Debug Current Music")]
    public void DebugCurrentMusic()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        AudioClip currentMusic = GetMusicForScene(currentScene);
        
        Debug.Log("🎵 === Music Manager Debug ===");
        Debug.Log($"🎵 Current Scene: {currentScene}");
        Debug.Log($"🎵 Current Music: {(currentMusic != null ? currentMusic.name : "None")}");
        Debug.Log($"🎵 Is Playing: {audioSource.isPlaying}");
        Debug.Log($"🎵 Volume: {audioSource.volume}");
        
        Debug.Log($"🎵 Configured Scenes ({sceneMusicList.Length}):");
        foreach (SceneMusic sceneMusic in sceneMusicList)
        {
            Debug.Log($"🎵   - '{sceneMusic.sceneName}' => {(sceneMusic.musicClip != null ? sceneMusic.musicClip.name : "No Music")}");
        }
    }
    
    [ContextMenu("Force Play Current Scene Music")]
    public void ForcePlayCurrentSceneMusic()
    {
        PlayMusicForCurrentScene();
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}