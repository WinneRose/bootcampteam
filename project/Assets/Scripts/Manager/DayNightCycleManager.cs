using UnityEngine;
using Unity.Netcode;

public class DayNightCycleManager : NetworkBehaviour
{
    [Header("Time")]
    [Range(0, 24)] public float startTime = 12f;
    
    [Header("References")]
    public Light sun;
    public Material skyboxMaterial;
    public DayNightProfile profile;
    
    [Header("Network Settings")]
    public bool hostControlsTime = true; // Only host controls time progression
    
    // Network Variables
    private NetworkVariable<float> currentTime = new NetworkVariable<float>(
        12f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private float timeSpeed;
    
    // Singleton instance for easy access
    public static DayNightCycleManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize time
        if (IsServer)
        {
            currentTime.Value = startTime;
        }
        
        // Subscribe to time changes
        currentTime.OnValueChanged += OnTimeChanged;
        
        // Initialize time speed
        if (profile != null)
        {
            timeSpeed = 24f / (profile.dayLengthInMinutes * 60f);
        }
        
        // Initial update
        UpdateVisuals();
    }
    
    private void Update()
    {
        if (profile == null) return;
        
        // Only server/host updates time progression
        if (IsServer && hostControlsTime)
        {
            currentTime.Value += Time.deltaTime * timeSpeed;
            if (currentTime.Value >= 24f) 
                currentTime.Value -= 24f;
        }
        
        // All clients update visuals based on network time
        UpdateVisuals();
    }
    
    private void OnTimeChanged(float previousValue, float newValue)
    {
        // Update visuals when time changes from network
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        UpdateSun();
        UpdateSkybox();
    }
    
    private void UpdateSun()
    {
        if (sun == null) return;
        
        float timePercent = currentTime.Value / 24f;
        float sunAngle = timePercent * 360f - 90f;

        sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        sun.color = LerpByTime(
            profile.morningSunColor,
            profile.noonSunColor,
            profile.eveningSunColor,
            profile.nightSunColor
        );

        sun.intensity = profile.sunIntensity.Evaluate(timePercent);
    }

    private void UpdateSkybox()
    {
        if (skyboxMaterial == null || profile == null) return;

        float timePercent = currentTime.Value / 24f;

        // 🌅 Sky gradient
        Color top = LerpByTime(
            profile.morningSkyTop,
            profile.noonSkyTop,
            profile.eveningSkyTop,
            profile.nightSkyTop
        );

        Color bottom = LerpByTime(
            profile.morningSkyBottom,
            profile.noonSkyBottom,
            profile.eveningSkyBottom,
            profile.nightSkyBottom
        );

        skyboxMaterial.SetColor("_MainColor", top);
        skyboxMaterial.SetColor("_SecondColor", bottom);

        // 🌠 Stars
        skyboxMaterial.SetFloat("_StarVisibility", profile.starVisibility.Evaluate(timePercent));
        skyboxMaterial.SetFloat("_StarsDensity", profile.starsDensity);

        // 🌄 Skybox positioning
        skyboxMaterial.SetFloat("_Height", profile.skyboxHeight);
        skyboxMaterial.SetVector("_Tiling", profile.skyboxTiling);

        // 💡 Lighting
        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientLight = LerpByTime(
            profile.morningAmbient,
            profile.noonAmbient,
            profile.eveningAmbient,
            profile.nightAmbient
        );
    }

    private Color LerpByTime(Color morning, Color noon, Color evening, Color night)
    {
        float time = currentTime.Value;
        
        if (time >= 5f && time < 9f)
            return Color.Lerp(morning, noon, Mathf.InverseLerp(5f, 9f, time));
        else if (time >= 9f && time < 17f)
            return Color.Lerp(noon, evening, Mathf.InverseLerp(9f, 17f, time));
        else if (time >= 17f && time < 20f)
            return Color.Lerp(evening, night, Mathf.InverseLerp(17f, 20f, time));
        else
        {
            float t = time < 5f
                ? Mathf.InverseLerp(0f, 5f, time)
                : Mathf.InverseLerp(20f, 24f, time);
            return Color.Lerp(night, morning, t);
        }
    }
    
    // Public methods for other systems to use
    public float GetCurrentTime()
    {
        return currentTime.Value;
    }
    
    public bool IsInDaylight()
    {
        // Consider daylight between 6 AM and 6 PM
        float time = currentTime.Value;
        return time >= 6f && time <= 18f;
    }
    
    public bool IsInSunlight()
    {
        // More strict sunlight check (9 AM to 5 PM)
        float time = currentTime.Value;
        return time >= 9f && time <= 17f;
    }
    
    public float GetSunIntensity()
    {
        if (profile == null) return 1f;
        float timePercent = currentTime.Value / 24f;
        return profile.sunIntensity.Evaluate(timePercent);
    }
    
    // Admin/Debug methods (only work on server)
    [ServerRpc(RequireOwnership = false)]
    public void SetTimeServerRpc(float newTime)
    {
        if (IsServer)
        {
            currentTime.Value = Mathf.Clamp(newTime, 0f, 24f);
            Debug.Log($"Time set to: {newTime:F1}");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SetTimeSpeedServerRpc(float speedMultiplier)
    {
        if (IsServer && profile != null)
        {
            timeSpeed = (24f / (profile.dayLengthInMinutes * 60f)) * speedMultiplier;
            Debug.Log($"Time speed set to: {speedMultiplier}x");
        }
    }
    
    // Context menu for easy testing
    [ContextMenu("Set to Dawn (6 AM)")]
    private void SetToDawn()
    {
        if (IsServer)
            currentTime.Value = 6f;
    }
    
    [ContextMenu("Set to Noon (12 PM)")]
    private void SetToNoon()
    {
        if (IsServer)
            currentTime.Value = 12f;
    }
    
    [ContextMenu("Set to Dusk (6 PM)")]
    private void SetToDusk()
    {
        if (IsServer)
            currentTime.Value = 18f;
    }
    
    [ContextMenu("Set to Midnight (12 AM)")]
    private void SetToMidnight()
    {
        if (IsServer)
            currentTime.Value = 0f;
    }
    
    // Clean up
    public override void OnNetworkDespawn()
    {
        currentTime.OnValueChanged -= OnTimeChanged;
        base.OnNetworkDespawn();
    }
}