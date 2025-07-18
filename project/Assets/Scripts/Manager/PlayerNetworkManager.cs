using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkManager : NetworkBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private AbilityUI abilityUI;
    
    [Header("Character-Specific Ability Systems")]
    [SerializeField] private DewAbilitySystem dewAbilitySystem;
    [SerializeField] private SolAbilitySystem solAbilitySystem;
    
    [Header("Auto-Setup")]
    public bool autoFindComponents = true;
    public bool setupOnSpawn = true;
    
    [Header("UI Setup")]
    public Canvas playerCanvas; // For UI that should be specific to this player
    
    // Current ability system reference (whichever one exists)
    private MonoBehaviour currentAbilitySystem;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (setupOnSpawn)
        {
            SetupPlayerComponents();
        }
    }
    
    [ContextMenu("Setup Player Components")]
    public void SetupPlayerComponents()
    {
        Debug.Log($"Setting up player components for {gameObject.name} (IsOwner: {IsOwner})");
        
        if (autoFindComponents)
        {
            FindAllComponents();
        }
        
        ValidateComponents();
        LinkComponents();
        SetupUI();
        
        Debug.Log("Player components setup complete!");
    }
    
    private void FindAllComponents()
    {
        // Find PlayerController
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
                playerController = GetComponentInChildren<PlayerController>();
        }
        
        // Find DewAbilitySystem
        if (dewAbilitySystem == null)
        {
            dewAbilitySystem = GetComponent<DewAbilitySystem>();
            if (dewAbilitySystem == null)
                dewAbilitySystem = GetComponentInChildren<DewAbilitySystem>();
        }
        
        // Find SolAbilitySystem
        if (solAbilitySystem == null)
        {
            solAbilitySystem = GetComponent<SolAbilitySystem>();
            if (solAbilitySystem == null)
                solAbilitySystem = GetComponentInChildren<SolAbilitySystem>();
        }
        
        // Set current ability system
        if (dewAbilitySystem != null)
        {
            currentAbilitySystem = dewAbilitySystem;
            Debug.Log($"Found DewAbilitySystem on {dewAbilitySystem.gameObject.name}");
        }
        else if (solAbilitySystem != null)
        {
            currentAbilitySystem = solAbilitySystem;
            Debug.Log($"Found SolAbilitySystem on {solAbilitySystem.gameObject.name}");
        }
        
        // Find InputManager
        if (inputManager == null)
        {
            inputManager = GetComponent<InputManager>();
            if (inputManager == null)
                inputManager = GetComponentInChildren<InputManager>();
        }
        
        // Find AbilityUI
        if (abilityUI == null)
        {
            abilityUI = GetComponentInChildren<AbilityUI>();
            
            // If not found in children, try to find in Canvas
            if (abilityUI == null && playerCanvas != null)
            {
                abilityUI = playerCanvas.GetComponentInChildren<AbilityUI>();
            }
        }
        
        // Find player canvas if not assigned
        if (playerCanvas == null)
        {
            playerCanvas = GetComponentInChildren<Canvas>();
        }
    }
    
    private void ValidateComponents()
    {
        if (playerController == null)
            Debug.LogWarning($"PlayerController not found on {gameObject.name}");
        
        if (currentAbilitySystem == null)
            Debug.LogWarning($"No AbilitySystem (Dew or Sol) found on {gameObject.name}");
        
        if (inputManager == null)
            Debug.LogWarning($"InputManager not found on {gameObject.name}");
        
        if (abilityUI == null)
            Debug.LogWarning($"AbilityUI not found on {gameObject.name}");
            
        // Validate DewAbilitySystem specific components
        if (dewAbilitySystem != null)
        {
            if (dewAbilitySystem.projectilePrefab == null)
                Debug.LogWarning($"DewAbilitySystem on {gameObject.name} is missing projectilePrefab");
            
            if (dewAbilitySystem.projectileSpawnPoint == null)
                Debug.LogWarning($"DewAbilitySystem on {gameObject.name} is missing projectileSpawnPoint");
                
            // Check for camera reference
            Camera playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
                Debug.LogWarning($"No camera found for DewAbilitySystem projectile direction on {gameObject.name}");
        }
    }
    
    private void LinkComponents()
    {
        // Link InputManager to other components
        if (inputManager != null)
        {
            // Set player controller reference
            if (playerController != null)
                inputManager.playerController = playerController;
            
            // Set ability system references in InputManager
            if (dewAbilitySystem != null)
                inputManager.SetDewAbilitySystem(dewAbilitySystem);
            else if (solAbilitySystem != null)
                inputManager.SetSolAbilitySystem(solAbilitySystem);
            
            Debug.Log($"Linked InputManager to components");
        }
        
        // Link AbilitySystem to UI
        if (currentAbilitySystem != null && abilityUI != null)
        {
            if (dewAbilitySystem != null)
            {
                dewAbilitySystem.abilityUI = abilityUI;
                abilityUI.Initialize(dewAbilitySystem);
                Debug.Log($"Linked DewAbilitySystem to AbilityUI: {abilityUI.gameObject.name}");
            }
            else if (solAbilitySystem != null)
            {
                solAbilitySystem.abilityUI = abilityUI;
                abilityUI.Initialize(solAbilitySystem);
                Debug.Log($"Linked SolAbilitySystem to AbilityUI: {abilityUI.gameObject.name}");
            }
        }
    }
    
    private void SetupUI()
    {
        if (!IsOwner) 
        {
            // Disable ALL UI for non-owners (other players)
            DisableUIForNonOwner();
            return;
        }
        
        // Enable and setup UI only for the local player (owner)
        SetupUIForOwner();
    }
    
    private void SetupUIForOwner()
    {
        Debug.Log($"Setting up UI for local player: {gameObject.name}");
        
        // Enable AbilityUI for owner
        if (abilityUI != null)
        {
            abilityUI.gameObject.SetActive(true);
            
            // Force UI refresh based on character type
            if (dewAbilitySystem != null)
            {
                // Update Dew UI with current values
                abilityUI.UpdateWaterInfo(dewAbilitySystem.GetCurrentWaterCapacity(), dewAbilitySystem.GetMaxWaterCapacity());
                abilityUI.UpdateWaterChargingState(dewAbilitySystem.IsCharging());
                abilityUI.RefreshWaterDistance();
            }
            else if (solAbilitySystem != null)
            {
                // Update Sol UI with current values
                abilityUI.UpdateSolarInfo(
                    solAbilitySystem.GetCurrentSolarEnergy(),
                    solAbilitySystem.GetMaxSolarEnergy(),
                    solAbilitySystem.GetIsInSunlight(),
                    solAbilitySystem.GetTimeOfDay()
                );
            }
        }
        
        // Setup player canvas for owner
        if (playerCanvas != null)
        {
            playerCanvas.gameObject.SetActive(true);
            
            // Ensure proper canvas settings for local player UI
            playerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            playerCanvas.sortingOrder = 100; // High sorting order to be on top
            
            // Make sure it's not set to World Space which could cause issues
            if (playerCanvas.renderMode == RenderMode.WorldSpace)
            {
                Debug.LogWarning("Player Canvas was in World Space mode, changing to Screen Space Overlay for local player");
                playerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            
            Debug.Log($"Enabled and configured Canvas for local player: {playerCanvas.gameObject.name}");
        }
        
        // Find any other Canvas components that might need setup
        Canvas[] allCanvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas != playerCanvas) // Don't modify the main player canvas again
            {
                canvas.gameObject.SetActive(true);
                // Ensure they're also in overlay mode
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                canvas.sortingOrder = 99; // Slightly lower than main canvas
            }
        }
    }
    
    private void DisableUIForNonOwner()
    {
        Debug.Log($"Disabling UI for non-owner player: {gameObject.name}");
        
        // Disable AbilityUI for non-owners
        if (abilityUI != null)
        {
            abilityUI.gameObject.SetActive(false);
        }
        
        // Disable player canvas for non-owners
        if (playerCanvas != null)
        {
            playerCanvas.gameObject.SetActive(false);
        }
        
        // Find and disable ALL Canvas components in this player
        Canvas[] allCanvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            canvas.gameObject.SetActive(false);
            Debug.Log($"Disabled Canvas for non-owner: {canvas.gameObject.name}");
        }
        
        // Also find and disable any UI components directly
        AbilityUI[] allAbilityUIs = GetComponentsInChildren<AbilityUI>(true);
        foreach (AbilityUI ui in allAbilityUIs)
        {
            ui.gameObject.SetActive(false);
            Debug.Log($"Disabled AbilityUI for non-owner: {ui.gameObject.name}");
        }
    }
    
    // Public methods for manual setup
    public void SetPlayerController(PlayerController controller)
    {
        playerController = controller;
        LinkComponents();
    }
    
    public void SetDewAbilitySystem(DewAbilitySystem dewSystem)
    {
        dewAbilitySystem = dewSystem;
        currentAbilitySystem = dewSystem;
        LinkComponents();
    }
    
    public void SetSolAbilitySystem(SolAbilitySystem solSystem)
    {
        solAbilitySystem = solSystem;
        currentAbilitySystem = solSystem;
        LinkComponents();
    }
    
    public void SetInputManager(InputManager input)
    {
        inputManager = input;
        LinkComponents();
    }
    
    public void SetAbilityUI(AbilityUI ui)
    {
        abilityUI = ui;
        LinkComponents();
    }
    
    // Getters for external access
    public PlayerController GetPlayerController() => playerController;
    public DewAbilitySystem GetDewAbilitySystem() => dewAbilitySystem;
    public SolAbilitySystem GetSolAbilitySystem() => solAbilitySystem;
    public MonoBehaviour GetCurrentAbilitySystem() => currentAbilitySystem;
    public InputManager GetInputManager() => inputManager;
    public AbilityUI GetAbilityUI() => abilityUI;
    
    // Helper methods
    public bool IsDewCharacter() => dewAbilitySystem != null;
    public bool IsSolCharacter() => solAbilitySystem != null;
    public string GetCharacterType() => IsDewCharacter() ? "Dew" : IsSolCharacter() ? "Sol" : "Unknown";
    
    // Water source management for Dew characters
    public string GetWaterSourceStatus()
    {
        if (dewAbilitySystem == null) return "Not a Dew character";
        
        return dewAbilitySystem.GetWaterStatusString();
    }
    
    public WaterSourceInfo[] GetAllWaterSources()
    {
        if (dewAbilitySystem == null) return new WaterSourceInfo[0];
        
        return dewAbilitySystem.GetAllWaterSourcesInfo();
    }
    
    // Debug method to check all references
    [ContextMenu("Debug Component Status")]
    public void DebugComponentStatus()
    {
        Debug.Log($"=== Player Component Status for {gameObject.name} ===");
        Debug.Log($"IsOwner: {IsOwner}");
        Debug.Log($"Character Type: {GetCharacterType()}");
        Debug.Log($"PlayerController: {(playerController != null ? "✓" : "✗")}");
        Debug.Log($"DewAbilitySystem: {(dewAbilitySystem != null ? "✓" : "✗")}");
        Debug.Log($"SolAbilitySystem: {(solAbilitySystem != null ? "✓" : "✗")}");
        Debug.Log($"InputManager: {(inputManager != null ? "✓" : "✗")}");
        Debug.Log($"AbilityUI: {(abilityUI != null ? "✓" : "✗")}");
        Debug.Log($"PlayerCanvas: {(playerCanvas != null ? "✓" : "✗")}");
        
        // Debug DewAbilitySystem specific components
        if (dewAbilitySystem != null)
        {
            Debug.Log($"--- Dew System Status ---");
            Debug.Log($"Water Capacity: {dewAbilitySystem.GetCurrentWaterCapacity():F1}/{dewAbilitySystem.GetMaxWaterCapacity()}");
            Debug.Log($"Can Use Ability: {dewAbilitySystem.CanUseAbility()}");
            Debug.Log($"Is Charging: {dewAbilitySystem.IsCharging()}");
            Debug.Log($"In Water Zone: {dewAbilitySystem.IsInWaterZone()}");
            Debug.Log($"Projectile Prefab: {(dewAbilitySystem.projectilePrefab != null ? "✓" : "✗")}");
            Debug.Log($"Spawn Point: {(dewAbilitySystem.projectileSpawnPoint != null ? "✓" : "✗")}");
            Debug.Log($"Water Status: {GetWaterSourceStatus()}");
        }
        
        if (inputManager != null && currentAbilitySystem != null)
        {
            bool inputLinked = false;
            if (dewAbilitySystem != null)
                inputLinked = inputManager.HasDewAbilities();
            else if (solAbilitySystem != null)
                inputLinked = inputManager.HasSolAbilities();
                
            Debug.Log($"InputManager -> AbilitySystem link: {(inputLinked ? "✓" : "✗")}");
        }
        
        if (currentAbilitySystem != null && abilityUI != null)
        {
            bool uiLinked = false;
            if (dewAbilitySystem != null)
                uiLinked = dewAbilitySystem.abilityUI == abilityUI;
            else if (solAbilitySystem != null)
                uiLinked = solAbilitySystem.abilityUI == abilityUI;
                
            Debug.Log($"AbilitySystem -> AbilityUI link: {(uiLinked ? "✓" : "✗")}");
            
            // Debug UI state
            if (uiLinked && abilityUI.IsInitialized())
            {
                Debug.Log($"UI Character Type: {(abilityUI.IsDewCharacter() ? "Dew" : abilityUI.IsSolCharacter() ? "Sol" : "Unknown")}");
            }
        }
    }
    
    // Debug water sources specifically
    [ContextMenu("Debug Water Sources")]
    public void DebugWaterSources()
    {
        if (dewAbilitySystem == null)
        {
            Debug.Log("Not a Dew character - no water sources to debug");
            return;
        }
        
        WaterSourceInfo[] waterSources = GetAllWaterSources();
        Debug.Log($"=== Water Sources Debug for {gameObject.name} ===");
        Debug.Log($"Found {waterSources.Length} water sources");
        Debug.Log($"Collection Range: {dewAbilitySystem.waterCollectionRange}m");
        Debug.Log($"Detection Range: {dewAbilitySystem.maxWaterDetectionRange}m");
        
        for (int i = 0; i < waterSources.Length; i++)
        {
            var source = waterSources[i];
            string status = source.inCollectionRange ? "COLLECTION RANGE" : 
                           source.inDetectionRange ? "DETECTION RANGE" : "OUT OF RANGE";
            Debug.Log($"{i + 1}. {source.name} - {source.distance:F1}m ({status})");
        }
        
        Debug.Log($"Current Status: {GetWaterSourceStatus()}");
    }
    
    // Context menu for easy testing
    [ContextMenu("Force Setup Components")]
    public void ForceSetupComponents()
    {
        SetupPlayerComponents();
    }
    
    [ContextMenu("Refresh All References")]
    public void RefreshAllReferences()
    {
        // Force re-find all components
        dewAbilitySystem = null;
        solAbilitySystem = null;
        currentAbilitySystem = null;
        playerController = null;
        inputManager = null;
        abilityUI = null;
        playerCanvas = null;
        
        // Re-setup everything
        SetupPlayerComponents();
    }
    
    [ContextMenu("Test Dew Abilities")]
    public void TestDewAbilities()
    {
        if (dewAbilitySystem == null)
        {
            Debug.Log("No DewAbilitySystem found");
            return;
        }
        
        if (!IsOwner)
        {
            Debug.Log("Cannot test abilities - not the owner");
            return;
        }
        
        Debug.Log("=== Testing Dew Abilities ===");
        Debug.Log($"Adding water...");
        dewAbilitySystem.AddWater(20f);
        
        Debug.Log($"Water after adding: {dewAbilitySystem.GetCurrentWaterCapacity():F1}");
        
        if (dewAbilitySystem.CanUseAbility())
        {
            Debug.Log("Using ability...");
            dewAbilitySystem.UseAbility();
            Debug.Log($"Water after using ability: {dewAbilitySystem.GetCurrentWaterCapacity():F1}");
        }
        else
        {
            Debug.Log("Cannot use ability - not enough water");
        }
    }
}