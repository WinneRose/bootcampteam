using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class InputManager : NetworkBehaviour
{
    [Header("References")]
    public PlayerController playerController;
    
    [Header("Auto-Find Settings")]
    public bool autoFindReferences = true;
    public bool searchInParent = true;
    public bool searchInChildren = true;
    
    [Header("Input Actions")]
    public InputAction moveAction;
    public InputAction jumpAction;
    public InputAction lookAction;
    public InputAction interactAction;
    public InputAction chargeAbilityAction;
    public InputAction useAbilityAction;
    
    // Ability systems (support for both character types)
    private DewAbilitySystem dewAbilitySystem;
    private SolAbilitySystem solAbilitySystem;
    private MonoBehaviour currentAbilitySystem; // Generic reference
    
    // Input state tracking
    private bool wasChargingLastFrame = false;
    private bool isInitialized = false;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only initialize for the owner
        if (!IsOwner) return;
        
        InitializeReferences();
        InitializeInputActions();
        
        Cursor.visible = false;
        isInitialized = true;
    }
    
    private void InitializeReferences()
    {
        if (autoFindReferences)
        {
            FindPlayerController();
            FindAbilitySystems();
        }
        
        ValidateReferences();
    }
    
    private void FindPlayerController()
    {
        if (playerController != null) return;
        
        // Try to find on the same GameObject first
        playerController = GetComponent<PlayerController>();
        
        if (playerController == null && searchInParent)
        {
            // Try to find in parent
            playerController = GetComponentInParent<PlayerController>();
        }
        
        if (playerController == null && searchInChildren)
        {
            // Try to find in children
            playerController = GetComponentInChildren<PlayerController>();
        }
        
        if (playerController != null)
        {
            Debug.Log($"Found PlayerController on {playerController.gameObject.name}");
        }
    }
    
    private void FindAbilitySystems()
    {
        // Try to find Dew ability system
        dewAbilitySystem = GetComponent<DewAbilitySystem>();
        if (dewAbilitySystem == null && searchInParent)
            dewAbilitySystem = GetComponentInParent<DewAbilitySystem>();
        if (dewAbilitySystem == null && searchInChildren)
            dewAbilitySystem = GetComponentInChildren<DewAbilitySystem>();
        
        // Try to find Sol ability system
        solAbilitySystem = GetComponent<SolAbilitySystem>();
        if (solAbilitySystem == null && searchInParent)
            solAbilitySystem = GetComponentInParent<SolAbilitySystem>();
        if (solAbilitySystem == null && searchInChildren)
            solAbilitySystem = GetComponentInChildren<SolAbilitySystem>();
        
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
    }
    
    private void ValidateReferences()
    {
        if (playerController == null)
        {
            Debug.LogError($"PlayerController not found on {gameObject.name}! Please assign manually or enable auto-find.");
        }
        
        if (currentAbilitySystem == null)
        {
            Debug.LogError($"No AbilitySystem found on {gameObject.name}! Please assign DewAbilitySystem or SolAbilitySystem.");
        }
    }
    
    private void InitializeInputActions()
    {
        // Find input actions
        var actionMap = InputSystem.actions;
        
        if (actionMap == null)
        {
            Debug.LogError("No Input Action Asset found! Please assign one in the Input System Package settings.");
            return;
        }
        
        moveAction = actionMap.FindAction("Move");
        jumpAction = actionMap.FindAction("Jump");
        lookAction = actionMap.FindAction("Look");
        interactAction = actionMap.FindAction("Interact");
        chargeAbilityAction = actionMap.FindAction("ChargeAbility");
        useAbilityAction = actionMap.FindAction("UseAbility");
        
        ValidateInputActions();
    }
    
    private void ValidateInputActions()
    {
        if (moveAction == null) Debug.LogWarning("Move action not found in Input Actions!");
        if (jumpAction == null) Debug.LogWarning("Jump action not found in Input Actions!");
        if (lookAction == null) Debug.LogWarning("Look action not found in Input Actions!");
        if (interactAction == null) Debug.LogWarning("Interact action not found in Input Actions!");
        if (chargeAbilityAction == null) Debug.LogWarning("ChargeAbility action not found in Input Actions!");
        if (useAbilityAction == null) Debug.LogWarning("UseAbility action not found in Input Actions!");
    }

    private void Update()
    {
        // Only process input for the owner
        if (!IsOwner || !isInitialized) return;
        
        HandleMovementInput();
        HandleAbilityInput();
        HandleMiscInput();
    }
    
    private void HandleMovementInput()
    {
        if (playerController == null) return;
        
        // Movement
        if (moveAction != null)
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            playerController.Move(new Vector3(input.x, 0f, input.y));
        }
        
        // Look
        if (lookAction != null)
        {
            Vector2 lookVector = lookAction.ReadValue<Vector2>();
            playerController.Look(lookVector);
        }
        
        // Jump
        if (jumpAction != null && jumpAction.triggered)
        {
            playerController.Jump();
            Debug.Log("Jump triggered");
        }
    }
    
    private void HandleAbilityInput()
    {
        if (currentAbilitySystem == null) return;
        
        // Handle charging ability
        if (chargeAbilityAction != null)
        {
            bool isChargingNow = chargeAbilityAction.IsPressed();
            
            // Start charging when button is first pressed
            if (isChargingNow && !wasChargingLastFrame)
            {
                if (dewAbilitySystem != null)
                    dewAbilitySystem.StartCharging();
                else if (solAbilitySystem != null)
                    solAbilitySystem.StartCharging();
                    
       
            }
            // Stop charging when button is released
            else if (!isChargingNow && wasChargingLastFrame)
            {
                if (dewAbilitySystem != null)
                    dewAbilitySystem.StopCharging();
                else if (solAbilitySystem != null)
                    solAbilitySystem.StopCharging();
                    
          
            }
            
            wasChargingLastFrame = isChargingNow;
        }
        
        // Handle using ability
        if (useAbilityAction != null && useAbilityAction.triggered)
        {
            if (dewAbilitySystem != null)
                dewAbilitySystem.UseAbility();
            else if (solAbilitySystem != null)
                solAbilitySystem.UseAbility();
                
  
        }
    }
    
    private void HandleMiscInput()
    {
        // Interact
        if (interactAction != null && interactAction.triggered)
        {
            Debug.Log("Interact triggered");
            // Add your interaction logic here
        }
    }
    
    // Public method to manually set references (useful for runtime assignment)
    public void SetDewAbilitySystem(DewAbilitySystem dewSystem)
    {
        dewAbilitySystem = dewSystem;
        currentAbilitySystem = dewSystem;
        Debug.Log("DewAbilitySystem manually set for InputManager");
    }
    
    public void SetSolAbilitySystem(SolAbilitySystem solSystem)
    {
        solAbilitySystem = solSystem;
        currentAbilitySystem = solSystem;
        Debug.Log("SolAbilitySystem manually set for InputManager");
    }
    
    // Public method to refresh references (useful if components are added later)
    public void RefreshReferences()
    {
        if (autoFindReferences)
        {
            FindPlayerController();
            FindAbilitySystems();
            ValidateReferences();
        }
    }
    
    // Public getters
    public bool HasDewAbilities() => dewAbilitySystem != null;
    public bool HasSolAbilities() => solAbilitySystem != null;
    public DewAbilitySystem GetDewAbilitySystem() => dewAbilitySystem;
    public SolAbilitySystem GetSolAbilitySystem() => solAbilitySystem;
    
    public override void OnNetworkDespawn()
    {
        isInitialized = false;
        base.OnNetworkDespawn();
    }
}