
using UnityEngine;
using Unity.Netcode;
public class PlayerNetworkManager : NetworkBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private AbilityUI abilityUI;
    
    [Header("Auto-Setup")]
    public bool autoFindComponents = true;
    public bool setupOnSpawn = true;
    
    [Header("UI Setup")]
    public Canvas playerCanvas; // For UI that should be specific to this player
    
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
        
        // Find AbilitySystem
        if (abilitySystem == null)
        {
            abilitySystem = GetComponent<AbilitySystem>();
            if (abilitySystem == null)
                abilitySystem = GetComponentInChildren<AbilitySystem>();
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
        
        if (abilitySystem == null)
            Debug.LogWarning($"AbilitySystem not found on {gameObject.name}");
        
        if (inputManager == null)
            Debug.LogWarning($"InputManager not found on {gameObject.name}");
        
        if (abilityUI == null)
            Debug.LogWarning($"AbilityUI not found on {gameObject.name}");
    }
    
    private void LinkComponents()
    {
        // Link InputManager to other components
        if (inputManager != null)
        {
            if (playerController != null)
                inputManager.playerController = playerController;
            
            if (abilitySystem != null)
                inputManager.abilitySystem = abilitySystem;
            
            // Refresh references in case InputManager has auto-find enabled
            var inputMgr = inputManager.GetComponent<InputManager>();
            if (inputMgr != null)
            {
                inputMgr.SetReferences(playerController, abilitySystem);
            }
        }
        
        // Link AbilitySystem to UI (with additional safety checks)
        if (abilitySystem != null && abilityUI != null)
        {
            abilitySystem.abilityUI = abilityUI;
            
            // Use the new SetAbilityUI method if available
            abilitySystem.SetAbilityUI(abilityUI);
            
            Debug.Log($"Linked AbilitySystem to AbilityUI: {abilityUI.gameObject.name}");
        }
        else if (abilitySystem != null && abilityUI == null)
        {
            // Try to refresh UI if AbilitySystem exists but UI doesn't
            abilitySystem.RefreshUI();
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
    
    public void SetAbilitySystem(AbilitySystem abilities)
    {
        abilitySystem = abilities;
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
    public AbilitySystem GetAbilitySystem() => abilitySystem;
    public InputManager GetInputManager() => inputManager;
    public AbilityUI GetAbilityUI() => abilityUI;
    
    // Debug method to check all references
    [ContextMenu("Debug Component Status")]
    public void DebugComponentStatus()
    {
        Debug.Log($"=== Player Component Status for {gameObject.name} ===");
        Debug.Log($"IsOwner: {IsOwner}");
        Debug.Log($"PlayerController: {(playerController != null ? "✓" : "✗")}");
        Debug.Log($"AbilitySystem: {(abilitySystem != null ? "✓" : "✗")}");
        Debug.Log($"InputManager: {(inputManager != null ? "✓" : "✗")}");
        Debug.Log($"AbilityUI: {(abilityUI != null ? "✓" : "✗")}");
        Debug.Log($"PlayerCanvas: {(playerCanvas != null ? "✓" : "✗")}");
        
        if (inputManager != null && abilitySystem != null)
        {
            Debug.Log($"InputManager -> AbilitySystem link: {(inputManager.abilitySystem == abilitySystem ? "✓" : "✗")}");
        }
        
        if (abilitySystem != null && abilityUI != null)
        {
            Debug.Log($"AbilitySystem -> AbilityUI link: {(abilitySystem.abilityUI == abilityUI ? "✓" : "✗")}");
        }
    }
}
