using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DayNightCycleManager : NetworkBehaviour
{
    [Header("Time")]
    [Range(0, 24)] public float startTime = 12f;
    
    [Header("References")]
    public Light sun;
    public Material skyboxMaterial;
    public DayNightProfile profile;
    
    [Header("Network Settings")]
    public bool hostControlsTime = true;
    
    [Header("Client Interpolation")]
    [Range(0.1f, 2f)] public float interpolationSpeed = 1f; // How fast clients catch up to server time
    public bool useClientPrediction = true; // Predict time progression on clients
    
    [Header("Performance")]
    [Range(1, 60)] public int visualUpdateRate = 30;
    
    [Header("Debug")]
    public bool debugMode = false;
    
    // Network Variables - sent less frequently to reduce network traffic
    private NetworkVariable<float> serverTime = new NetworkVariable<float>(
        12f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private NetworkVariable<float> timeSpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Client-side smooth time (interpolated)
    private float smoothCurrentTime = 12f;
    private float targetTime = 12f;
    private float baseTimeSpeed;
    private float lastServerUpdate = 0f;
    
    // Static reference
    public static DayNightCycleManager Instance { get; private set; }
    
    // Persistent data across scenes
    private static DayNightPersistentData persistentData = new DayNightPersistentData();
    
    [System.Serializable]
    private class DayNightPersistentData
    {
        public float savedTime = -1f;
        public float savedTimeSpeed = 1f;
        public bool hasData = false;
    }
    
    // Performance optimization
    private bool isUpdating = false;
    private float visualUpdateInterval;
    private float lastVisualUpdate = 0f;
    
    private void Awake()
    {
        Instance = this;
        visualUpdateInterval = 1f / Mathf.Max(1, visualUpdateRate);
        
        if (debugMode)
            Debug.Log($"[DayNight] Manager created. Visual rate: {visualUpdateRate}/sec");
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (debugMode)
            Debug.Log($"[DayNight] OnNetworkSpawn - IsServer: {IsServer}");
        
        StartCoroutine(InitializeManager());
    }
    
    private IEnumerator InitializeManager()
    {
        yield return new WaitForEndOfFrame();
        
        // Calculate base time speed
        if (profile != null)
        {
            baseTimeSpeed = 24f / (profile.dayLengthInMinutes * 60f);
        }
        else
        {
            baseTimeSpeed = 1f / 60f;
            Debug.LogWarning("[DayNight] No profile assigned");
        }
        
        // Initialize based on role
        if (IsServer)
        {
            InitializeAsServer();
        }
        else
        {
            InitializeAsClient();
        }
        
        // Subscribe to network changes
        serverTime.OnValueChanged += OnServerTimeChanged;
        timeSpeedMultiplier.OnValueChanged += OnTimeSpeedChanged;
        
        isUpdating = true;
        UpdateVisuals();
        
        if (debugMode)
            Debug.Log($"[DayNight] Init complete. Time: {smoothCurrentTime:F2}");
    }
    
    private void InitializeAsServer()
    {
        if (persistentData.hasData)
        {
            serverTime.Value = persistentData.savedTime;
            timeSpeedMultiplier.Value = persistentData.savedTimeSpeed;
            smoothCurrentTime = persistentData.savedTime;
            targetTime = persistentData.savedTime;
            
            if (debugMode)
                Debug.Log($"[DayNight] Server: Loaded time: {persistentData.savedTime:F2}");
        }
        else
        {
            serverTime.Value = startTime;
            timeSpeedMultiplier.Value = 1f;
            smoothCurrentTime = startTime;
            targetTime = startTime;
            
            persistentData.savedTime = startTime;
            persistentData.savedTimeSpeed = 1f;
            persistentData.hasData = true;
            
            if (debugMode)
                Debug.Log($"[DayNight] Server: First init: {startTime:F2}");
        }
    }
    
    private void InitializeAsClient()
    {
        // Initialize with server time
        smoothCurrentTime = serverTime.Value;
        targetTime = serverTime.Value;
        lastServerUpdate = Time.time;
        
        persistentData.savedTime = serverTime.Value;
        persistentData.savedTimeSpeed = timeSpeedMultiplier.Value;
        persistentData.hasData = true;
        
        if (debugMode)
            Debug.Log($"[DayNight] Client: Synced to: {serverTime.Value:F2}");
    }
    
    private void Update()
    {
        if (!isUpdating || profile == null) return;
        
        if (IsServer)
        {
            UpdateServerTime();
        }
        else
        {
            UpdateClientTime();
        }
        
        // Update visuals at controlled rate
        if (Time.time - lastVisualUpdate >= visualUpdateInterval)
        {
            UpdateVisuals();
            lastVisualUpdate = Time.time;
        }
    }
    
    private void UpdateServerTime()
    {
        // Server updates the authoritative time
        float deltaTime = Time.deltaTime * baseTimeSpeed * timeSpeedMultiplier.Value;
        serverTime.Value += deltaTime;
        
        if (serverTime.Value >= 24f) 
            serverTime.Value -= 24f;
        
        // Server uses its own time directly (no interpolation needed)
        smoothCurrentTime = serverTime.Value;
        targetTime = serverTime.Value;
        
        // Update persistent data
        persistentData.savedTime = serverTime.Value;
        persistentData.hasData = true;
    }
    
    private void UpdateClientTime()
    {
        // Client smoothly interpolates toward server time
        if (useClientPrediction)
        {
            // Predict time progression between server updates
            float timeSinceLastUpdate = Time.time - lastServerUpdate;
            float predictedServerTime = targetTime + (timeSinceLastUpdate * baseTimeSpeed * timeSpeedMultiplier.Value);
            
            // Handle day rollover
            if (predictedServerTime >= 24f)
                predictedServerTime -= 24f;
            
            // Smoothly interpolate toward predicted time
            smoothCurrentTime = Mathf.Lerp(smoothCurrentTime, predictedServerTime, Time.deltaTime * interpolationSpeed * 10f);
        }
        else
        {
            // Simple interpolation toward last known server time
            smoothCurrentTime = Mathf.Lerp(smoothCurrentTime, targetTime, Time.deltaTime * interpolationSpeed * 5f);
        }
        
        // Handle day boundary crossing
        if (smoothCurrentTime >= 24f)
            smoothCurrentTime -= 24f;
        else if (smoothCurrentTime < 0f)
            smoothCurrentTime += 24f;
        
        // Debug client interpolation
        if (debugMode && Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
        {
            Debug.Log($"[DayNight] Client - Server: {serverTime.Value:F2}, Smooth: {smoothCurrentTime:F2}, Target: {targetTime:F2}");
        }
    }
    
    private void OnServerTimeChanged(float previousValue, float newValue)
    {
        // Update target time for client interpolation
        targetTime = newValue;
        lastServerUpdate = Time.time;
        
        // Update persistent data
        persistentData.savedTime = newValue;
        persistentData.hasData = true;
        
        if (debugMode)
            Debug.Log($"[DayNight] Server time update: {previousValue:F2} -> {newValue:F2}");
        
        // If the time jumped significantly (like admin set time), snap to it
        float timeDifference = Mathf.Abs(newValue - smoothCurrentTime);
        if (timeDifference > 1f) // More than 1 hour difference
        {
            smoothCurrentTime = newValue;
            if (debugMode)
                Debug.Log($"[DayNight] Large time jump detected, snapping to: {newValue:F2}");
        }
    }
    
    private void OnTimeSpeedChanged(float previousValue, float newValue)
    {
        persistentData.savedTimeSpeed = newValue;
        
        if (debugMode)
            Debug.Log($"[DayNight] Speed changed: {newValue}x");
    }
    
    private void UpdateVisuals()
    {
        if (!isUpdating) return;
        
        UpdateSun();
        UpdateSkybox();
    }
    
    private void UpdateSun()
    {
        if (sun == null || profile == null) return;
        
        float timePercent = smoothCurrentTime / 24f;
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

        float timePercent = smoothCurrentTime / 24f;

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

        // Batch skybox updates for performance
        skyboxMaterial.SetColor("_MainColor", top);
        skyboxMaterial.SetColor("_SecondColor", bottom);
        skyboxMaterial.SetFloat("_StarVisibility", profile.starVisibility.Evaluate(timePercent));
        skyboxMaterial.SetFloat("_StarsDensity", profile.starsDensity);
        skyboxMaterial.SetFloat("_Height", profile.skyboxHeight);
        skyboxMaterial.SetVector("_Tiling", profile.skyboxTiling);

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
        float time = smoothCurrentTime; // Use smooth time for visuals
        
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
    
    // Public methods - return smooth time for clients
    public float GetCurrentTime() => smoothCurrentTime;
    public float GetServerTime() => serverTime.Value; // For debugging
    public bool IsInDaylight() => smoothCurrentTime >= 6f && smoothCurrentTime <= 18f;
    public bool IsInSunlight() => smoothCurrentTime >= 9f && smoothCurrentTime <= 17f;
    public float GetSunIntensity() => profile?.sunIntensity.Evaluate(smoothCurrentTime / 24f) ?? 1f;
    public bool IsTimeUpdating() => isUpdating;
    
    // Server RPC methods
    [ServerRpc(RequireOwnership = false)]
    public void SetTimeServerRpc(float newTime)
    {
        if (IsServer)
        {
            serverTime.Value = Mathf.Clamp(newTime, 0f, 24f);
            Debug.Log($"[DayNight] Time set to: {newTime:F1}");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SetTimeSpeedServerRpc(float speedMultiplier)
    {
        if (IsServer)
        {
            timeSpeedMultiplier.Value = Mathf.Max(0f, speedMultiplier);
            Debug.Log($"[DayNight] Speed set to: {speedMultiplier}x");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void PauseTimeServerRpc()
    {
        if (IsServer)
        {
            timeSpeedMultiplier.Value = 0f;
            Debug.Log("[DayNight] Time paused");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ResumeTimeServerRpc()
    {
        if (IsServer)
        {
            timeSpeedMultiplier.Value = persistentData.savedTimeSpeed > 0f ? persistentData.savedTimeSpeed : 1f;
            Debug.Log($"[DayNight] Time resumed");
        }
    }
    
    // Context menu methods
    [ContextMenu("Set to Dawn (6 AM)")]
    private void SetToDawn() { if (IsServer) serverTime.Value = 6f; }
    
    [ContextMenu("Set to Noon (12 PM)")]
    private void SetToNoon() { if (IsServer) serverTime.Value = 12f; }
    
    [ContextMenu("Set to Dusk (6 PM)")]
    private void SetToDusk() { if (IsServer) serverTime.Value = 18f; }
    
    [ContextMenu("Set to Midnight (12 AM)")]
    private void SetToMidnight() { if (IsServer) serverTime.Value = 0f; }
    
    [ContextMenu("Debug Interpolation")]
    private void DebugInterpolation()
    {
        Debug.Log($"[DayNight] Interpolation Debug:\n" +
                  $"- Server Time: {serverTime.Value:F3}\n" +
                  $"- Smooth Time: {smoothCurrentTime:F3}\n" +
                  $"- Target Time: {targetTime:F3}\n" +
                  $"- Time Speed: {timeSpeedMultiplier.Value}x\n" +
                  $"- Client Prediction: {useClientPrediction}\n" +
                  $"- Interpolation Speed: {interpolationSpeed}");
    }
    
    // Cleanup
    public override void OnNetworkDespawn()
    {
        if (debugMode)
            Debug.Log("[DayNight] OnNetworkDespawn");
            
        isUpdating = false;
        
        serverTime.OnValueChanged -= OnServerTimeChanged;
        timeSpeedMultiplier.OnValueChanged -= OnTimeSpeedChanged;
        
        base.OnNetworkDespawn();
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
            
        if (debugMode)
            Debug.Log("[DayNight] Manager destroyed");
    }
    
    // Static methods
    public static float GetPersistentTime() => persistentData.savedTime;
    public static bool HasPersistentData() => persistentData.hasData;
}