using UnityEngine;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
    [Header("UI References")]
    public Slider volumeSlider;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float defaultVolume = 1f;
    
    private void Awake()
    {
        // Make this GameObject persist across scene loads
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        // Load saved volume or use default
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);
        
        // Initialize slider value
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
        
        // Set initial volume
        AudioListener.volume = savedVolume;
    }
    
    public void OnSliderValueChanged(float value)
    {
        // Update the global audio volume
        AudioListener.volume = value;
        
        // Optional: Save the volume setting to PlayerPrefs
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
    }
    
    private void OnDestroy()
    {
        // Clean up the listener to prevent memory leaks
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }
}