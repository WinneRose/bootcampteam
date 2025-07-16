using UnityEngine;
using Unity.Netcode;

public class AbilitySystem : NetworkBehaviour
{
    [Header("Ability Settings")]
    public float maxAbilityPoints = 100f;
    public float maxPointsPerZone = 20f;
    public float chargeRate = 1f;
    public float consumeAmount = 20f;
    
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 20f;
    public float projectileLifetime = 5f;
    
    [Header("UI References")]
    public AbilityUI abilityUI;
    
    [Header("Auto-Find Settings")]
    public bool autoFindUI = true;
    public bool searchInChildren = true;
    public bool searchInScene = true;
    
    // Network Variables
    private NetworkVariable<float> currentAbilityPoints = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> isInAbilityZone = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> currentZonePoints = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> isCharging = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Local variables
    private bool canUseAbility = false;
    private Camera playerCamera;
    private float lastChargeTime = 0f;
    private const float CHARGE_INTERVAL = 1f; // Charge every second
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            // Find camera
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            
            // Find and initialize UI
            FindAndInitializeUI();
        }
        
        // Subscribe to network variable changes for all clients
        currentAbilityPoints.OnValueChanged += OnAbilityPointsChanged;
        isInAbilityZone.OnValueChanged += OnZoneStateChanged;
        currentZonePoints.OnValueChanged += OnZonePointsChanged;
        isCharging.OnValueChanged += OnChargingStateChanged;
        
        // Initial UI update
        if (IsOwner)
            UpdateUI();
    }
    
    private void FindAndInitializeUI()
    {
        // Only setup UI for the owner (local player)
        if (!IsOwner)
        {
            // Disable any UI components for non-owners
            DisableUIForNonOwner();
            return;
        }
        
        if (abilityUI != null)
        {
            // UI is already assigned, just initialize it
            SetupUIForOwner();
            abilityUI.Initialize(this);
            Debug.Log($"Using assigned AbilityUI: {abilityUI.gameObject.name}");
            return;
        }
        
        if (!autoFindUI) 
        {
            Debug.LogWarning("AbilityUI not assigned and auto-find is disabled!");
            return;
        }
        
        // Try to find AbilityUI component
        FindAbilityUI();
        
        if (abilityUI != null)
        {
            SetupUIForOwner();
            abilityUI.Initialize(this);
            Debug.Log($"Found and initialized AbilityUI: {abilityUI.gameObject.name}");
        }
        else
        {
            Debug.LogError($"AbilityUI not found for {gameObject.name}! Please assign manually or ensure it exists in the hierarchy.");
        }
    }
    
    private void SetupUIForOwner()
    {
        if (abilityUI == null) return;
        
        // Enable the UI for the owner
        abilityUI.gameObject.SetActive(true);
        
        // Find the canvas and make sure it's set up correctly for local player
        Canvas canvas = abilityUI.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // Ensure canvas is in Screen Space - Overlay mode for local player
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // High sorting order to be on top
            canvas.gameObject.SetActive(true);
            
            Debug.Log($"Enabled UI Canvas for owner: {canvas.gameObject.name}");
        }
    }
    
    private void DisableUIForNonOwner()
    {
        // Find and disable any UI components that belong to this player but shouldn't show for others
        AbilityUI[] uiComponents = GetComponentsInChildren<AbilityUI>(true);
        foreach (AbilityUI ui in uiComponents)
        {
            ui.gameObject.SetActive(false);
            Debug.Log($"Disabled AbilityUI for non-owner: {ui.gameObject.name}");
        }
        
        // Also disable any Canvas components
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            canvas.gameObject.SetActive(false);
            Debug.Log($"Disabled Canvas for non-owner: {canvas.gameObject.name}");
        }
    }
    
    private void FindAbilityUI()
    {
        // Method 1: Try to find in children first (most common case for per-player UI)
        if (searchInChildren)
        {
            abilityUI = GetComponentInChildren<AbilityUI>(true); // Include inactive objects
            if (abilityUI != null)
            {
                Debug.Log("Found AbilityUI in children");
                return;
            }
        }
        
        // For per-player UI, we usually don't want to search globally
        // But keeping this as fallback for shared UI scenarios
        if (searchInScene)
        {
            // Method 2: Try to find by looking for Canvas with AbilityUI
            Canvas[] canvases = FindObjectsOfType<Canvas>(true); // Include inactive
            foreach (Canvas canvas in canvases)
            {
                AbilityUI ui = canvas.GetComponentInChildren<AbilityUI>(true);
                if (ui != null)
                {
                    // Check if this UI belongs to this player (no other player reference)
                    AbilitySystem[] otherAbilitySystems = FindObjectsOfType<AbilitySystem>();
                    bool isAlreadyUsed = false;
                    
                    foreach (AbilitySystem other in otherAbilitySystems)
                    {
                        if (other != this && other.abilityUI == ui)
                        {
                            isAlreadyUsed = true;
                            break;
                        }
                    }
                    
                    if (!isAlreadyUsed)
                    {
                        abilityUI = ui;
                        Debug.Log($"Found AbilityUI in canvas: {canvas.gameObject.name}");
                        return;
                    }
                }
            }
            
            // Method 3: Try to find any AbilityUI in scene (last resort)
            AbilityUI[] allUIs = FindObjectsOfType<AbilityUI>(true);
            foreach (AbilityUI ui in allUIs)
            {
                // Check if this UI is not already assigned to another AbilitySystem
                AbilitySystem[] otherAbilitySystems = FindObjectsOfType<AbilitySystem>();
                bool isAlreadyUsed = false;
                
                foreach (AbilitySystem other in otherAbilitySystems)
                {
                    if (other != this && other.abilityUI == ui)
                    {
                        isAlreadyUsed = true;
                        break;
                    }
                }
                
                if (!isAlreadyUsed)
                {
                    abilityUI = ui;
                    Debug.Log($"Found AbilityUI in scene: {ui.gameObject.name}");
                    return;
                }
            }
        }
    }
    
    void Update()
    {
        if (!IsOwner) return;
        
        // Update ability state
        UpdateAbilityState();
        
        // Handle charging with proper timing
        if (isCharging.Value && isInAbilityZone.Value && Time.time >= lastChargeTime + CHARGE_INTERVAL)
        {
            ProcessCharging();
            lastChargeTime = Time.time;
        }
    }
    
    private void UpdateAbilityState()
    {
        canUseAbility = currentAbilityPoints.Value >= consumeAmount;
        UpdateUI();
    }
    
    private void ProcessCharging()
    {
        if (currentZonePoints.Value < maxPointsPerZone && currentAbilityPoints.Value < maxAbilityPoints)
        {
            float newAbilityPoints = Mathf.Min(currentAbilityPoints.Value + chargeRate, maxAbilityPoints);
            float newZonePoints = Mathf.Min(currentZonePoints.Value + chargeRate, maxPointsPerZone);
            
            currentAbilityPoints.Value = newAbilityPoints;
            currentZonePoints.Value = newZonePoints;
            
            Debug.Log($"Charging ability: {currentAbilityPoints.Value}/{maxAbilityPoints}");
        }
        else
        {
            // Stop charging if limits reached
            isCharging.Value = false;
            Debug.Log("Charging stopped - limits reached");
        }
    }
    
    // Input methods called by InputManager
    public void StartCharging()
    {
        if (!IsOwner) return;
        
        Debug.Log($"StartCharging - In zone: {isInAbilityZone.Value}, Currently charging: {isCharging.Value}");
        
        if (isInAbilityZone.Value && !isCharging.Value)
        {
            if (currentZonePoints.Value < maxPointsPerZone && currentAbilityPoints.Value < maxAbilityPoints)
            {
                isCharging.Value = true;
                lastChargeTime = Time.time;
                Debug.Log("Started charging ability!");
            }
        }
    }
    
    public void StopCharging()
    {
        if (!IsOwner) return;
        
        if (isCharging.Value)
        {
            isCharging.Value = false;
            Debug.Log("Stopped charging ability!");
        }
    }
    
    public void UseAbility()
    {
        if (!IsOwner) return;
        
        Debug.Log($"UseAbility - Can use: {canUseAbility}");
        
        if (canUseAbility)
        {
            // Consume points locally first for immediate feedback
            currentAbilityPoints.Value -= consumeAmount;
            
            // Spawn projectile
            SpawnProjectile();
            
            Debug.Log("Used ability!");
        }
    }
    
    private void SpawnProjectile()
    {
        if (projectilePrefab != null && projectileSpawnPoint != null && playerCamera != null)
        {
            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector3 direction = playerCamera.transform.forward;
            
            // Spawn projectile locally for immediate feedback
            GameObject projectile = Instantiate(projectilePrefab, spawnPos, projectileSpawnPoint.rotation);
            
            // Initialize projectile
            Projectile projScript = projectile.GetComponent<Projectile>();
            if (projScript != null)
            {
                projScript.Initialize(direction, projectileSpeed, projectileLifetime);
            }
            else
            {
                // Fallback
                Rigidbody rb = projectile.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(direction * projectileSpeed, ForceMode.Impulse);
                    Destroy(projectile, projectileLifetime);
                }
            }
            
            // Notify other clients about the projectile
            SpawnProjectileClientRpc(spawnPos, direction);
        }
    }
    
    [ClientRpc]
    private void SpawnProjectileClientRpc(Vector3 spawnPosition, Vector3 direction)
    {
        // Don't spawn for the owner since they already have it
        if (IsOwner) return;
        
        if (projectilePrefab != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
            
            Projectile projScript = projectile.GetComponent<Projectile>();
            if (projScript != null)
            {
                projScript.Initialize(direction, projectileSpeed, projectileLifetime);
            }
            else
            {
                Rigidbody rb = projectile.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(direction * projectileSpeed, ForceMode.Impulse);
                    Destroy(projectile, projectileLifetime);
                }
            }
        }
    }
    
    // Zone interaction methods
    public void EnterAbilityZone()
    {
        if (!IsOwner) return;
        
        Debug.Log("Entered ability zone");
        isInAbilityZone.Value = true;
    }
    
    public void ExitAbilityZone()
    {
        if (!IsOwner) return;
        
        Debug.Log("Exited ability zone");
        isInAbilityZone.Value = false;
        currentZonePoints.Value = 0f;
        isCharging.Value = false;
    }
    
    // Network Variable change callbacks
    private void OnAbilityPointsChanged(float previousValue, float newValue)
    {
        if (IsOwner) UpdateUI();
    }
    
    private void OnZoneStateChanged(bool previousValue, bool newValue)
    {
        if (IsOwner) UpdateUI();
    }
    
    private void OnZonePointsChanged(float previousValue, float newValue)
    {
        if (IsOwner) UpdateUI();
    }
    
    private void OnChargingStateChanged(bool previousValue, bool newValue)
    {
        if (IsOwner) UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (abilityUI != null)
        {
            abilityUI.UpdateAbilityBar(currentAbilityPoints.Value, maxAbilityPoints);
            abilityUI.UpdateZoneInfo(currentZonePoints.Value, maxPointsPerZone, isInAbilityZone.Value);
            abilityUI.UpdateChargingState(isCharging.Value && isInAbilityZone.Value);
        }
    }
    
    // Clean up
    public override void OnNetworkDespawn()
    {
        currentAbilityPoints.OnValueChanged -= OnAbilityPointsChanged;
        isInAbilityZone.OnValueChanged -= OnZoneStateChanged;
        currentZonePoints.OnValueChanged -= OnZonePointsChanged;
        isCharging.OnValueChanged -= OnChargingStateChanged;
        
        base.OnNetworkDespawn();
    }
    
    // Public getters for UI and other systems
    public float GetCurrentAbilityPoints() => currentAbilityPoints.Value;
    public float GetMaxAbilityPoints() => maxAbilityPoints;
    public bool IsInAbilityZone() => isInAbilityZone.Value;
    public bool IsCharging() => isCharging.Value;
    public bool CanUseAbility() => canUseAbility;
    public float GetCurrentZonePoints() => currentZonePoints.Value;
    public float GetMaxPointsPerZone() => maxPointsPerZone;
    
    // Public method to manually set UI (useful for runtime assignment)
    public void SetAbilityUI(AbilityUI ui)
    {
        if (abilityUI != null && abilityUI != ui)
        {
            Debug.Log("Replacing existing AbilityUI reference");
        }
        
        abilityUI = ui;
        
        if (abilityUI != null && IsOwner)
        {
            abilityUI.Initialize(this);
            UpdateUI();
            Debug.Log($"AbilityUI manually set to: {abilityUI.gameObject.name}");
        }
    }
    
    // Public method to refresh UI reference (useful if UI is added later)
    public void RefreshUI()
    {
        if (IsOwner)
        {
            FindAndInitializeUI();
        }
    }
}