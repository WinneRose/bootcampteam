using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; 

public class AbilitySystem : NetworkBehaviour
{
    [Header("Ability Settings")]
    public float maxAbilityPoints = 100f;
    public float maxPointsPerZone = 20f; // Maximum points that can be collected per zone
    public float chargeRate = 1f; // Points per click when charging
    public float consumeAmount = 20f; // Points consumed per projectile
    
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 20f;
    public float projectileLifetime = 5f;
    
    [Header("UI References")]
    public AbilityUI abilityUI;
    
    // Network Variables
    private NetworkVariable<float> currentAbilityPoints = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> isInAbilityZone = new NetworkVariable<bool>(false);
    private NetworkVariable<float> currentZonePoints = new NetworkVariable<float>(0f); // Points collected in current zone;
    
    // Input states
    private bool isChargingAbility = false;
    private bool canUseAbility = false;
    
    // Camera reference for projectile direction
    private Camera playerCamera;
    
    void Start()
    {
        if (!IsOwner) return;
        
        // Find camera (usually tagged as MainCamera or child of player)
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        // Initialize UI
        if (abilityUI != null)
            abilityUI.Initialize(this);
    }
    
    void Update()
    {
        if (!IsOwner) 
        {
            Debug.Log("Not owner, skipping input");
            return;
        }
        
        if (Input.GetMouseButtonDown(1))
            Debug.Log("Right click + IsOwner: " + IsOwner);
        
        HandleInput();
        UpdateAbilityState();
    }
    
    private void HandleInput()
    {
        // Input System mouse kontrolü
    var mouse = UnityEngine.InputSystem.Mouse.current;
    if (mouse == null)
    {
        Debug.Log("Mouse.current is NULL!");
        return;
    }
    
    Debug.Log("Mouse right button: " + mouse.rightButton.isPressed);
    Debug.Log("Mouse left button: " + mouse.leftButton.wasPressedThisFrame);
    Debug.Log("In ability zone: " + isInAbilityZone.Value);
    Debug.Log("Can use ability: " + canUseAbility);
    
    // Right click to charge ability (only in ability zones)
    if (mouse.rightButton.isPressed && isInAbilityZone.Value)
    {
        Debug.Log("Right click detected and in zone!");
        if (!isChargingAbility)
        {
            isChargingAbility = true;
            StartChargingServerRpc();
        }
    }
    else
    {
        if (isChargingAbility)
        {
            isChargingAbility = false;
            StopChargingServerRpc();
        }
    }
    
    // Left click to use ability
    if (mouse.leftButton.wasPressedThisFrame && canUseAbility)
    {
        Debug.Log("Left click detected and can use ability!");
        UseAbilityServerRpc();
    }
    }
    
    private void UpdateAbilityState()
    {
        canUseAbility = currentAbilityPoints.Value >= consumeAmount;
        
        // Update UI
        if (abilityUI != null)
        {
            abilityUI.UpdateAbilityBar(currentAbilityPoints.Value, maxAbilityPoints);
            abilityUI.UpdateZoneInfo(currentZonePoints.Value, maxPointsPerZone, isInAbilityZone.Value);
            abilityUI.UpdateChargingState(isChargingAbility && isInAbilityZone.Value);
        }
    }
    
    [ServerRpc]
    private void StartChargingServerRpc()
    {
        if (isInAbilityZone.Value && currentZonePoints.Value < maxPointsPerZone && currentAbilityPoints.Value < maxAbilityPoints)
        {
            InvokeRepeating(nameof(ChargeAbility), 0f, 1f); // Her saniye 1 puan
        }
    }
    
    [ServerRpc]
    private void StopChargingServerRpc()
    {
        CancelInvoke(nameof(ChargeAbility));
    }
    
    private void ChargeAbility()
    {
        // Check if we can still charge in this zone and haven't reached max
        if (currentZonePoints.Value < maxPointsPerZone && currentAbilityPoints.Value < maxAbilityPoints)
        {
            currentAbilityPoints.Value += chargeRate;
            currentZonePoints.Value += chargeRate;
            
            // Clamp values
            currentAbilityPoints.Value = Mathf.Min(currentAbilityPoints.Value, maxAbilityPoints);
            currentZonePoints.Value = Mathf.Min(currentZonePoints.Value, maxPointsPerZone);
        }
        else
        {
            // Stop charging if limits reached
            CancelInvoke(nameof(ChargeAbility));
        }
    }
    
    [ServerRpc]
    private void UseAbilityServerRpc()
    {
        if (currentAbilityPoints.Value >= consumeAmount)
        {
            currentAbilityPoints.Value -= consumeAmount;
            SpawnProjectileClientRpc();
        }
    }
    
    [ClientRpc]
    private void SpawnProjectileClientRpc()
    {
        if (!IsOwner) return;
        
        if (projectilePrefab != null && projectileSpawnPoint != null && playerCamera != null)
        {
            // Spawn projectile at spawn point
            GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
            
            // Get projectile component and set direction
            Projectile projScript = projectile.GetComponent<Projectile>();
            if (projScript != null)
            {
                Vector3 direction = playerCamera.transform.forward;
                projScript.Initialize(direction, projectileSpeed, projectileLifetime);
            }
            else
            {
                // Fallback: just add force
                Rigidbody rb = projectile.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(playerCamera.transform.forward * projectileSpeed, ForceMode.Impulse);
                    Destroy(projectile, projectileLifetime);
                }
            }
        }
    }
    
    // Called by AbilityZone triggers
    public void EnterAbilityZone()
    {
        if (IsOwner)
        {
            SetAbilityZoneStateServerRpc(true);
        }
    }
    
    public void ExitAbilityZone()
    {
        if (IsOwner)
        {
            SetAbilityZoneStateServerRpc(false);
        }
    }
    
    [ServerRpc]
    private void SetAbilityZoneStateServerRpc(bool inZone)
    {
        isInAbilityZone.Value = inZone;
        
        if (!inZone)
        {
            // Reset zone points when leaving zone
            currentZonePoints.Value = 0f;
            // Stop charging when leaving zone
            CancelInvoke(nameof(ChargeAbility));
        }
    }
    
    // Public getters for UI and other systems
    public float GetCurrentAbilityPoints() => currentAbilityPoints.Value;
    public float GetMaxAbilityPoints() => maxAbilityPoints;
    public bool IsInAbilityZone() => isInAbilityZone.Value;
    public bool CanUseAbility() => canUseAbility;
    public float GetCurrentZonePoints() => currentZonePoints.Value;
    public float GetMaxPointsPerZone() => maxPointsPerZone;
}