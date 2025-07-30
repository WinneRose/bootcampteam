using UnityEngine;
using Unity.Netcode;

public class DewAbilitySystem : NetworkBehaviour
{
    [Header("Water System Variables")]
    public float maxWaterCapacity = 100f;
    public float currentWaterCapacity = 0f;
    public float waterCollectRate = 10f; // Water collected per second
    
    [Header("Water Collection Settings")]
    public float waterCollectionRange = 5f; // Range to collect water from sources
    public float maxWaterDetectionRange = 50f; // Maximum range to detect and display water sources
    public string waterTag = "Water";
    
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 20f;
    public float projectileLifetime = 5f;
    public float waterCostPerShot = 15f;
    
    [Header("UI References")]
    public AbilityUI abilityUI;
    
    [Header("Debug Settings")]
    public bool showDebugInfo = true;
    
    // Network Variables for multiplayer sync
    private NetworkVariable<float> networkWaterCapacity = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsCharging = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsInWaterZone = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Local variables
    private Camera playerCamera;
    private bool isCharging = false;
    private bool isInWaterZone = false;
    private float nearestWaterDistance = -1f;
    private GameObject nearestWaterSource = null;
    private WaterCollectionZone nearestWaterCollectionZone = null;
    
    // Update intervals
    private float lastWaterDistanceUpdate = 0f;
    private float lastWaterCollection = 0f;
    private const float WATER_DISTANCE_UPDATE_INTERVAL = 0.2f; // Update distance every 0.2 seconds
    private const float WATER_COLLECTION_INTERVAL = 0.1f; // Collect water every 0.1 seconds when charging
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            // Initialize local water capacity from network variable
            currentWaterCapacity = networkWaterCapacity.Value;
            
            // Find camera for projectile direction
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            
            // Find UI if not assigned
            if (abilityUI == null)
                abilityUI = GetComponentInChildren<AbilityUI>();
            
            // Initialize UI
            if (abilityUI != null)
                abilityUI.Initialize(this);
                
            Debug.Log("DewAbilitySystem: Initialized for owner");
        }
        
        // Subscribe to network variable changes for all clients
        networkWaterCapacity.OnValueChanged += OnWaterCapacityChanged;
        networkIsCharging.OnValueChanged += OnChargingStateChanged;
        networkIsInWaterZone.OnValueChanged += OnWaterZoneStateChanged;
        
        // Initial UI update
        if (IsOwner)
            UpdateUI();
    }
    
    void Update()
    {
        if (!IsOwner) return;
        
        // Update nearest water distance tracking
        if (Time.time >= lastWaterDistanceUpdate + WATER_DISTANCE_UPDATE_INTERVAL)
        {
            UpdateNearestWaterDistance();
            lastWaterDistanceUpdate = Time.time;
        }
        
        // Handle water collection when charging (NOT networked - local only)
        if (isCharging && isInWaterZone && Time.time >= lastWaterCollection + WATER_COLLECTION_INTERVAL)
        {
            CollectWater();
            lastWaterCollection = Time.time;
        }
        
        // Update UI
        UpdateUI();
        
        // Sync local state with network variables
        SyncNetworkVariables();
    }
    
    private void UpdateNearestWaterDistance()
    {
        GameObject[] waterSources = GameObject.FindGameObjectsWithTag(waterTag);
        
        if (waterSources.Length == 0)
        {
            nearestWaterDistance = -1f;
            nearestWaterSource = null;
            nearestWaterCollectionZone = null;
            isInWaterZone = false;
            return;
        }
        
        float closestDistance = float.MaxValue;
        GameObject closestWater = null;
        WaterCollectionZone closestWaterZone = null;
        Vector3 playerPosition = transform.position;
        
        foreach (GameObject water in waterSources)
        {
            if (water == null) continue;
            
            float distance = Vector3.Distance(playerPosition, water.transform.position);
            
            // Only consider water sources within detection range
            if (distance <= maxWaterDetectionRange && distance < closestDistance)
            {
                // Check if this water source can still provide water
                WaterCollectionZone waterZone = water.GetComponent<WaterCollectionZone>();
                if (waterZone != null && waterZone.CanProvideWater())
                {
                    closestDistance = distance;
                    closestWater = water;
                    closestWaterZone = waterZone;
                }
                else if (waterZone == null)
                {
                    // Basic water source without zone (infinite water)
                    closestDistance = distance;
                    closestWater = water;
                    closestWaterZone = null;
                }
            }
        }
        
        if (closestWater != null)
        {
            nearestWaterDistance = closestDistance;
            nearestWaterSource = closestWater;
            nearestWaterCollectionZone = closestWaterZone;
        }
        else
        {
            nearestWaterDistance = -1f;
            nearestWaterSource = null;
            nearestWaterCollectionZone = null;
        }
        
        // Check if we're in a water collection zone
        isInWaterZone = nearestWaterDistance != -1f && nearestWaterDistance <= waterCollectionRange;
    }
    
    private void CollectWater()
    {
        // Don't collect if water tank is full
        if (currentWaterCapacity >= maxWaterCapacity)
        {
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Water tank is full, stopping collection");
            StopCharging();
            return;
        }
        
        // Check if we're near a water source
        if (!isInWaterZone || nearestWaterSource == null)
        {
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Not in water zone, cannot collect water");
            return;
        }
        
        // Calculate water to collect
        float baseWaterToCollect = waterCollectRate * WATER_COLLECTION_INTERVAL;
        float actualCollected = 0f;
        
        if (nearestWaterCollectionZone != null)
        {
            // Use WaterCollectionZone's provision rate and consume from its capacity
            float zoneProvisionRate = nearestWaterCollectionZone.GetWaterProvisionRate();
            float waterToCollect = zoneProvisionRate * WATER_COLLECTION_INTERVAL;
            
            // Request water from the zone
            float waterFromZone = nearestWaterCollectionZone.ConsumeWater(waterToCollect);
            actualCollected = Mathf.Min(waterFromZone, maxWaterCapacity - currentWaterCapacity);
            
            if (showDebugInfo && waterFromZone > 0f)
                Debug.Log($"DewAbilitySystem: Collected {actualCollected:F1} water from zone {nearestWaterCollectionZone.name}");
        }
        else
        {
            // Basic water source (infinite)
            actualCollected = Mathf.Min(baseWaterToCollect, maxWaterCapacity - currentWaterCapacity);
            
            if (showDebugInfo)
                Debug.Log($"DewAbilitySystem: Collected {actualCollected:F1} water from basic source");
        }
        
        // Add collected water to player's capacity
        if (actualCollected > 0f)
        {
            currentWaterCapacity += actualCollected;
            currentWaterCapacity = Mathf.Clamp(currentWaterCapacity, 0f, maxWaterCapacity);
            
            if (showDebugInfo)
                Debug.Log($"DewAbilitySystem: Total water: {currentWaterCapacity:F1}/{maxWaterCapacity}");
        }
        
        // Check if water source was depleted and stop charging if it can't provide water anymore
        if (nearestWaterCollectionZone != null && !nearestWaterCollectionZone.CanProvideWater())
        {
            if (showDebugInfo)
                Debug.Log($"DewAbilitySystem: Water source {nearestWaterCollectionZone.name} can no longer provide water - stopping collection");
            
            // Clear references and stop charging
            // Note: Don't destroy the GameObject - let WaterCollectionZone handle its own lifecycle
            nearestWaterSource = null;
            nearestWaterCollectionZone = null;
            nearestWaterDistance = -1f;
            isInWaterZone = false;
            
            // Stop charging since source can't provide water
            StopCharging();
        }
    }
    
    private void SyncNetworkVariables()
    {
        // Sync local state with network variables
        if (networkWaterCapacity.Value != currentWaterCapacity)
            networkWaterCapacity.Value = currentWaterCapacity;
            
        if (networkIsCharging.Value != isCharging)
            networkIsCharging.Value = isCharging;
            
        if (networkIsInWaterZone.Value != isInWaterZone)
            networkIsInWaterZone.Value = isInWaterZone;
    }
    
    // Public methods for input handling
    public void StartCharging()
    {
        if (!IsOwner) return;
        
        if (!isInWaterZone)
        {
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Cannot charge - not in water zone");
            return;
        }
        
        if (currentWaterCapacity >= maxWaterCapacity)
        {
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Cannot charge - water tank is full");
            return;
        }
        
        // Check if water source can still provide water
        if (nearestWaterCollectionZone != null && !nearestWaterCollectionZone.CanProvideWater())
        {
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Cannot charge - water source is depleted");
            return;
        }
        
        isCharging = true;
        lastWaterCollection = Time.time;
        
        if (showDebugInfo)
            Debug.Log("DewAbilitySystem: Started charging water");
    }
    
    public void StopCharging()
    {
        if (!IsOwner) return;
        
        if (isCharging)
        {
            isCharging = false;
            if (showDebugInfo)
                Debug.Log("DewAbilitySystem: Stopped charging water");
        }
    }
    
    public void UseAbility()
    {
        if (!IsOwner) return;
        
        if (currentWaterCapacity < waterCostPerShot)
        {
            if (showDebugInfo)
                Debug.Log($"DewAbilitySystem: Cannot use ability - not enough water ({currentWaterCapacity:F1}/{waterCostPerShot})");
            return;
        }
        
        // Consume water
        currentWaterCapacity -= waterCostPerShot;
        currentWaterCapacity = Mathf.Max(0f, currentWaterCapacity);
        
        // Fire projectile
        FireWaterProjectile();
        
        if (showDebugInfo)
            Debug.Log($"DewAbilitySystem: Used ability. Water remaining: {currentWaterCapacity:F1}/{maxWaterCapacity}");
    }
    
    private void FireWaterProjectile()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null || playerCamera == null)
        {
            Debug.LogError("DewAbilitySystem: Missing projectile setup components");
            return;
        }
        
        Vector3 spawnPosition = projectileSpawnPoint.position;
        Vector3 direction = playerCamera.transform.forward;
        
        // Spawn projectile locally
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        // Configure projectile appearance
        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.blue;
        
        // Apply physics
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(direction * projectileSpeed, ForceMode.Impulse);
        
        // Destroy after lifetime
        Destroy(projectile, projectileLifetime);
        
        // Notify other clients
        FireWaterProjectileClientRpc(spawnPosition, direction);
    }
    
    [ClientRpc]
    private void FireWaterProjectileClientRpc(Vector3 spawnPosition, Vector3 direction)
    {
        // Don't spawn duplicate projectile for the owner
        if (IsOwner) return;
        
        if (projectilePrefab == null) return;
        
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.blue;
        
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(direction * projectileSpeed, ForceMode.Impulse);
        
        Destroy(projectile, projectileLifetime);
    }
    
    // Network variable change callbacks
    private void OnWaterCapacityChanged(float previousValue, float newValue)
    {
        currentWaterCapacity = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void OnChargingStateChanged(bool previousValue, bool newValue)
    {
        isCharging = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void OnWaterZoneStateChanged(bool previousValue, bool newValue)
    {
        isInWaterZone = newValue;
        if (IsOwner) UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (abilityUI == null) return;
    
        // Update water capacity bar and text
        abilityUI.UpdateWaterInfo(currentWaterCapacity, maxWaterCapacity);
    
        // Update charging state
        abilityUI.UpdateChargingState(isCharging);
    
        // Update water distance information
        if (nearestWaterDistance != -1f)
        {
            bool inRange = nearestWaterDistance <= waterCollectionRange;
            abilityUI.UpdateWaterDistance(nearestWaterDistance, inRange);
        }
        else
        {
            abilityUI.UpdateWaterDistance("No water sources found");
        }
    }
    
    // Public getters for external access
    public float GetCurrentWaterCapacity() => currentWaterCapacity;
    public float GetMaxWaterCapacity() => maxWaterCapacity;
    public bool IsCharging() => isCharging;
    public bool IsInWaterZone() => isInWaterZone;
    public bool CanUseAbility() => currentWaterCapacity >= waterCostPerShot;
    public float GetNearestWaterDistance() => nearestWaterDistance;
    public GameObject GetNearestWaterSource() => nearestWaterSource;
    public WaterCollectionZone GetNearestWaterCollectionZone() => nearestWaterCollectionZone;
    
    // Utility methods
    public void AddWater(float amount)
    {
        if (!IsOwner) return;
        
        currentWaterCapacity = Mathf.Min(currentWaterCapacity + amount, maxWaterCapacity);
        if (showDebugInfo)
            Debug.Log($"DewAbilitySystem: Added {amount} water. Current: {currentWaterCapacity:F1}/{maxWaterCapacity}");
    }
    
    public void SetWaterCapacity(float amount)
    {
        if (!IsOwner) return;
        
        currentWaterCapacity = Mathf.Clamp(amount, 0f, maxWaterCapacity);
    }
    
    public string GetWaterStatusString()
    {
        if (nearestWaterDistance == -1f)
            return $"No water sources within {maxWaterDetectionRange}m";
        
        string rangeStatus = nearestWaterDistance <= waterCollectionRange ? "IN RANGE" : "OUT OF RANGE";
        string sourceStatus = "";
        
        if (nearestWaterCollectionZone != null)
        {
            if (nearestWaterCollectionZone.IsWaterDepleted())
                sourceStatus = " (DEPLETED)";
            else if (nearestWaterCollectionZone.GetMaxWaterCapacity() > 0f)
                sourceStatus = $" ({nearestWaterCollectionZone.GetCurrentWaterRemaining():F0}/{nearestWaterCollectionZone.GetMaxWaterCapacity():F0})";
        }
        
        return $"Water: {nearestWaterDistance:F1}m ({rangeStatus}){sourceStatus}";
    }
    
    // Get all water sources for debugging
    public WaterSourceInfo[] GetAllWaterSourcesInfo()
    {
        GameObject[] waterSources = GameObject.FindGameObjectsWithTag(waterTag);
        WaterSourceInfo[] infos = new WaterSourceInfo[waterSources.Length];
        
        Vector3 playerPosition = transform.position;
        
        for (int i = 0; i < waterSources.Length; i++)
        {
            GameObject water = waterSources[i];
            if (water != null)
            {
                float distance = Vector3.Distance(playerPosition, water.transform.position);
                WaterCollectionZone waterZone = water.GetComponent<WaterCollectionZone>();
                
                infos[i] = new WaterSourceInfo
                {
                    gameObject = water,
                    distance = distance,
                    inCollectionRange = distance <= waterCollectionRange,
                    inDetectionRange = distance <= maxWaterDetectionRange,
                    position = water.transform.position,
                    name = water.name,
                    waterCollectionZone = waterZone,
                    canProvideWater = waterZone != null ? waterZone.CanProvideWater() : true,
                    isDepletd = waterZone != null ? waterZone.IsWaterDepleted() : false,
                    currentWaterRemaining = waterZone != null ? waterZone.GetCurrentWaterRemaining() : -1f,
                    maxWaterCapacity = waterZone != null ? waterZone.GetMaxWaterCapacity() : -1f
                };
            }
        }
        
        // Sort by distance (closest first)
        System.Array.Sort(infos, (a, b) => a.distance.CompareTo(b.distance));
        
        return infos;
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        networkWaterCapacity.OnValueChanged -= OnWaterCapacityChanged;
        networkIsCharging.OnValueChanged -= OnChargingStateChanged;
        networkIsInWaterZone.OnValueChanged -= OnWaterZoneStateChanged;
        
        base.OnNetworkDespawn();
    }
    
    // Debug methods
    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;
        
        // Draw water collection range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, waterCollectionRange);
        
        // Draw water detection range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxWaterDetectionRange);
        
        // Draw line to nearest water source
        if (nearestWaterSource != null)
        {
            Gizmos.color = isInWaterZone ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, nearestWaterSource.transform.position);
            
            // Draw additional info for water collection zones
            if (nearestWaterCollectionZone != null)
            {
                Gizmos.color = nearestWaterCollectionZone.IsWaterDepleted() ? Color.red : Color.yellow;
                Gizmos.DrawWireCube(nearestWaterSource.transform.position, Vector3.one * 2f);
            }
        }
    }
}

// Enhanced helper struct for water source information
[System.Serializable]
public struct WaterSourceInfo
{
    public GameObject gameObject;
    public float distance;
    public bool inCollectionRange;
    public bool inDetectionRange;
    public Vector3 position;
    public string name;
    
    // Enhanced water zone information
    public WaterCollectionZone waterCollectionZone;
    public bool canProvideWater;
    public bool isDepletd;
    public float currentWaterRemaining;
    public float maxWaterCapacity;
}