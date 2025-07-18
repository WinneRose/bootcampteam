using UnityEngine;
using Unity.Netcode;

public class SolAbilitySystem : NetworkBehaviour
{
    [Header("Solar Energy System")]
    public float maxSolarEnergy = 100f;
    public float currentSolarEnergy = 0f;
    public float solarGenerationRate = 5f; // Energy per second during daytime
    public float manualChargeRate = 10f; // Extra energy per second when manually charging
    
    [Header("Solar Blast")]
    public float blastCost = 25f;
    public GameObject blastPrefab;
    public Transform blastSpawnPoint;
    public float blastSpeed = 30f;
    public float blastLifetime = 5f;
    
    [Header("UI References")]
    public AbilityUI abilityUI;
    
    [Header("Day/Night System")]
    public bool useDayNightManager = true;
    public float fallbackTimeOfDay = 12f; // Used if no DayNightManager
    public bool useRealTime = false;
    
    // Network Variables
    private NetworkVariable<float> networkSolarEnergy = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsCharging = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsInSunlight = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Local variables
    private Camera playerCamera;
    private DayNightCycleManager dayNightManager;
    private bool isCharging = false;
    private bool isInSunlight = true;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            // Initialize local energy from network variable
            currentSolarEnergy = networkSolarEnergy.Value;
            
            // Find camera for blast direction
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            
            // Find UI if not assigned
            if (abilityUI == null)
                abilityUI = GetComponentInChildren<AbilityUI>();
            
            // Find DayNightCycleManager
            if (useDayNightManager)
            {
                dayNightManager = DayNightCycleManager.Instance;
                if (dayNightManager == null)
                {
                    dayNightManager = FindObjectOfType<DayNightCycleManager>();
                }
                
                if (dayNightManager == null)
                {
                    useDayNightManager = false;
                    Debug.LogWarning("DayNightCycleManager not found, using fallback time system");
                }
            }
            
            // Initialize UI
            if (abilityUI != null)
                abilityUI.Initialize(this);
                
            Debug.Log("SolAbilitySystem: Initialized for owner");
        }
        
        // Subscribe to network variable changes for all clients
        networkSolarEnergy.OnValueChanged += OnSolarEnergyChanged;
        networkIsCharging.OnValueChanged += OnChargingStateChanged;
        networkIsInSunlight.OnValueChanged += OnSunlightChanged;
        
        // Initial UI update
        if (IsOwner)
            UpdateUI();
    }
    
    void Update()
    {
        if (!IsOwner) return;
        
        // Update time and sunlight status
        UpdateTimeOfDay();
        isInSunlight = IsInDaylight();
        
        // Generate solar energy automatically during daytime
        if (isInSunlight)
        {
            GenerateSolarEnergy();
        }
        
        // Manual charging boost
        if (isCharging && isInSunlight)
        {
            float manualEnergyGain = manualChargeRate * Time.deltaTime;
            currentSolarEnergy = Mathf.Min(currentSolarEnergy + manualEnergyGain, maxSolarEnergy);
        }
        
        // Update UI
        UpdateUI();
        
        // Sync local state with network variables
        SyncNetworkVariables();
    }
    
    private void GenerateSolarEnergy()
    {
        if (currentSolarEnergy >= maxSolarEnergy) return;
        
        float energyGain = solarGenerationRate * Time.deltaTime;
        currentSolarEnergy = Mathf.Min(currentSolarEnergy + energyGain, maxSolarEnergy);
    }
    
    private void UpdateTimeOfDay()
    {
        if (!useDayNightManager || dayNightManager == null)
        {
            if (useRealTime)
            {
                System.DateTime now = System.DateTime.Now;
                fallbackTimeOfDay = now.Hour + (now.Minute / 60f);
            }
            else
            {
                // Simple day/night cycle for testing
                fallbackTimeOfDay += Time.deltaTime * 0.1f; // Adjust speed as needed
                if (fallbackTimeOfDay >= 24f)
                    fallbackTimeOfDay = 0f;
            }
        }
    }
    
    private float GetCurrentTimeOfDay()
    {
        if (useDayNightManager && dayNightManager != null)
        {
            return dayNightManager.GetCurrentTime();
        }
        return fallbackTimeOfDay;
    }
    
    private bool IsInDaylight()
    {
        if (useDayNightManager && dayNightManager != null)
        {
            return dayNightManager.IsInDaylight();
        }
        
        // Fallback: Consider daylight between 6 AM and 6 PM
        float time = GetCurrentTimeOfDay();
        return time >= 6f && time <= 18f;
    }
    
    private void SyncNetworkVariables()
    {
        // Sync local state with network variables
        if (networkSolarEnergy.Value != currentSolarEnergy)
            networkSolarEnergy.Value = currentSolarEnergy;
            
        if (networkIsCharging.Value != isCharging)
            networkIsCharging.Value = isCharging;
            
        if (networkIsInSunlight.Value != isInSunlight)
            networkIsInSunlight.Value = isInSunlight;
    }
    
    // Input methods
    public void StartCharging()
    {
        if (!IsOwner) return;
        
        if (!isInSunlight)
        {
            Debug.Log("SolAbilitySystem: Cannot charge - it's nighttime!");
            return;
        }
        
        if (currentSolarEnergy >= maxSolarEnergy)
        {
            Debug.Log("SolAbilitySystem: Cannot charge - solar energy is full");
            return;
        }
        
        isCharging = true;
        Debug.Log("SolAbilitySystem: Started manual charging");
    }
    
    public void StopCharging()
    {
        if (!IsOwner) return;
        
        if (isCharging)
        {
            isCharging = false;
            Debug.Log("SolAbilitySystem: Stopped manual charging");
        }
    }
    
    public void UseAbility()
    {
        if (!IsOwner) return;
        
        if (currentSolarEnergy < blastCost)
        {
            Debug.Log($"SolAbilitySystem: Cannot use solar blast - not enough energy ({currentSolarEnergy:F1}/{blastCost})");
            return;
        }
        
        // Consume energy
        currentSolarEnergy -= blastCost;
        currentSolarEnergy = Mathf.Max(0f, currentSolarEnergy);
        
        // Fire solar blast
        FireSolarBlast();
        
        Debug.Log($"SolAbilitySystem: Used solar blast. Energy remaining: {currentSolarEnergy:F1}/{maxSolarEnergy}");
    }
    
    private void FireSolarBlast()
    {
        if (blastPrefab == null || blastSpawnPoint == null || playerCamera == null)
        {
            Debug.LogError("SolAbilitySystem: Missing solar blast setup components");
            return;
        }
        
        Vector3 spawnPosition = blastSpawnPoint.position;
        Vector3 direction = playerCamera.transform.forward;
        
        // Spawn blast locally
        GameObject blast = Instantiate(blastPrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        // Configure blast appearance
        Renderer renderer = blast.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.yellow; // Solar blast is yellow
        
        // Apply physics
        Rigidbody rb = blast.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(direction * blastSpeed, ForceMode.Impulse);
        
        // Destroy after lifetime
        Destroy(blast, blastLifetime);
        
        // Notify other clients
        FireSolarBlastClientRpc(spawnPosition, direction);
    }
    
    [ClientRpc]
    private void FireSolarBlastClientRpc(Vector3 spawnPosition, Vector3 direction)
    {
        // Don't spawn duplicate blast for the owner
        if (IsOwner) return;
        
        if (blastPrefab == null) return;
        
        GameObject blast = Instantiate(blastPrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        Renderer renderer = blast.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.yellow;
        
        Rigidbody rb = blast.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(direction * blastSpeed, ForceMode.Impulse);
        
        Destroy(blast, blastLifetime);
    }
    
    // Network variable change callbacks
    private void OnSolarEnergyChanged(float previousValue, float newValue)
    {
        currentSolarEnergy = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void OnChargingStateChanged(bool previousValue, bool newValue)
    {
        isCharging = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void OnSunlightChanged(bool previousValue, bool newValue)
    {
        isInSunlight = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (abilityUI == null) return;
        
        // Update solar info
        abilityUI.UpdateSolarInfo(currentSolarEnergy, maxSolarEnergy, isInSunlight, GetCurrentTimeOfDay());
        
        // Update charging state
        abilityUI.UpdateChargingState(isCharging);
    }
    
    // Public getters for external access
    public float GetCurrentSolarEnergy() => currentSolarEnergy;
    public float GetMaxSolarEnergy() => maxSolarEnergy;
    public bool IsCharging() => isCharging;
    public bool GetIsInSunlight() => isInSunlight;
    public float GetTimeOfDay() => GetCurrentTimeOfDay();
    public bool CanUseAbility() => currentSolarEnergy >= blastCost;
    
    // Utility methods
    public void AddSolarEnergy(float amount)
    {
        if (!IsOwner) return;
        
        currentSolarEnergy = Mathf.Min(currentSolarEnergy + amount, maxSolarEnergy);
        Debug.Log($"SolAbilitySystem: Added {amount} solar energy. Current: {currentSolarEnergy:F1}/{maxSolarEnergy}");
    }
    
    public void SetSolarEnergy(float amount)
    {
        if (!IsOwner) return;
        
        currentSolarEnergy = Mathf.Clamp(amount, 0f, maxSolarEnergy);
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        networkSolarEnergy.OnValueChanged -= OnSolarEnergyChanged;
        networkIsCharging.OnValueChanged -= OnChargingStateChanged;
        networkIsInSunlight.OnValueChanged -= OnSunlightChanged;
        
        base.OnNetworkDespawn();
    }
    
    // Debug methods
    [ContextMenu("Set Time to Noon")]
    public void SetTimeToNoon()
    {
        if (!useDayNightManager)
        {
            fallbackTimeOfDay = 12f;
        }
    }
    
    [ContextMenu("Set Time to Night")]
    public void SetTimeToNight()
    {
        if (!useDayNightManager)
        {
            fallbackTimeOfDay = 0f;
        }
    }
    
    [ContextMenu("Add Solar Energy")]
    public void DebugAddEnergy()
    {
        AddSolarEnergy(25f);
    }
}