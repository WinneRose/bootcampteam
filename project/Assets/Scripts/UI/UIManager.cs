using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI Gameobjects")]
    [Tooltip("The initial menu with Host/Client buttons")]
    public GameObject serverUI;
    
    [Tooltip("The character selection screen with podiums")]
    public GameObject afterJoinUI;
    
    [Header("Buttons")]
    [Tooltip("Host button - starts the host")]
    public Button hostButton;
    
    [Tooltip("Client button - joins as client")]
    public Button clientButton;
    
    [Header("Network Manager")]
    public NetworkManager networkManager;
    
    private bool isHostStarted = false;
    
    void Start()
    {
        // Find components if not assigned
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();
            
        if (serverUI == null)
            serverUI = GameObject.Find("ServerUI");
            
        if (afterJoinUI == null)
            afterJoinUI = GameObject.Find("AfterJoin");
        
        // Find buttons if not assigned
        if (hostButton == null)
        {
            GameObject hostObj = GameObject.Find("Host_Button");
            if (hostObj != null)
                hostButton = hostObj.GetComponent<Button>();
        }
        
        if (clientButton == null)
        {
            GameObject clientObj = GameObject.Find("Client_Button");
            if (clientObj != null)
                clientButton = clientObj.GetComponent<Button>();
        }
        
        // Setup initial UI state
        SetupInitialUI();
        
        // Add button listeners
        SetupButtonListeners();
        
        Debug.Log("UIManager initialized");
    }
    
    private void SetupInitialUI()
    {
        // Show ServerUI initially
        if (serverUI != null)
        {
            serverUI.SetActive(true);
            Debug.Log("ServerUI shown");
        }
        
        // Hide AfterJoin initially
        if (afterJoinUI != null)
        {
            afterJoinUI.SetActive(false);
            Debug.Log("AfterJoin hidden");
        }
        
        // Enable host button
        if (hostButton != null)
        {
            hostButton.interactable = true;
            Debug.Log("Host button enabled");
        }
        
        // Enable client button
        if (clientButton != null)
        {
            clientButton.interactable = true;
            Debug.Log("Client button enabled");
        }
    }
    
    private void SetupButtonListeners()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() => OnHostButtonClick());
        }
        
        if (clientButton != null)
        {
            clientButton.onClick.AddListener(() => OnClientButtonClick());
        }
    }
    
    public void OnHostButtonClick()
    {
        Debug.Log("Host button clicked");
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found!");
            return;
        }
        
        // Start host
        if (networkManager.StartHost())
        {
            Debug.Log("Host started successfully");
            isHostStarted = true;
            
            // Switch to character selection UI
            StartCoroutine(WaitAndSwitchToCharacterSelection());
        }
        else
        {
            Debug.LogError("Failed to start host");
        }
    }
    
    public void OnClientButtonClick()
    {
        Debug.Log("Client button clicked");
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found!");
            return;
        }
        
        // Start client
        if (networkManager.StartClient())
        {
            Debug.Log("Client connection started");
            
            // Subscribe to connection events
            networkManager.OnClientConnectedCallback += OnClientConnected;
            
            // Start timeout for connection
            StartCoroutine(ClientConnectionTimeout());
        }
        else
        {
            Debug.LogError("Failed to start client");
        }
    }
    
    private IEnumerator WaitAndSwitchToCharacterSelection()
    {
        // Wait for host to be fully ready
        yield return new WaitUntil(() => networkManager.IsHost && networkManager.IsConnectedClient);
        
        // Additional delay to ensure everything is initialized
        yield return new WaitForSeconds(0.3f);
        
        SwitchToCharacterSelection();
    }
    
    private void OnClientConnected(ulong clientId)
    {
        // Only handle our own client connection
        if (clientId == networkManager.LocalClientId && !networkManager.IsHost)
        {
            Debug.Log($"Local client {clientId} connected - switching to character selection");
            SwitchToCharacterSelection();
        }
    }
    
    private IEnumerator ClientConnectionTimeout()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (elapsed < timeout && !networkManager.IsConnectedClient)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (!networkManager.IsConnectedClient)
        {
            Debug.LogError("Client connection timed out!");
            ShowConnectionError();
        }
    }
    
    private void SwitchToCharacterSelection()
    {
        Debug.Log("Switching to character selection UI");
        
        // Hide ServerUI
        if (serverUI != null)
        {
            serverUI.SetActive(false);
            Debug.Log("ServerUI hidden");
        }
        
        // Show AfterJoin
        if (afterJoinUI != null)
        {
            afterJoinUI.SetActive(true);
            Debug.Log("AfterJoin shown");
        }
    }
    
    private void ShowConnectionError()
    {
        Debug.LogError("Failed to connect to host!");
        
        // Could show an error message here
        // You might want to add an error popup or message
    }
    
    // Public method to manually switch back to menu (useful for disconnect)
    public void SwitchToMenu()
    {
        Debug.Log("Switching back to menu");
        
        // Show ServerUI
        if (serverUI != null)
        {
            serverUI.SetActive(true);
        }
        
        // Hide AfterJoin
        if (afterJoinUI != null)
        {
            afterJoinUI.SetActive(false);
        }
        
        // Reset button states
        SetupInitialUI();
        
        isHostStarted = false;
    }
    
    // Method to handle disconnection
    public void OnDisconnected()
    {
        Debug.Log("Player disconnected - returning to menu");
        SwitchToMenu();
    }
    
    // Network event handlers
    void OnEnable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    
    void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        // If local client disconnected, return to menu
        if (clientId == networkManager.LocalClientId)
        {
            Debug.Log("Local client disconnected");
            SwitchToMenu();
        }
    }
    
    // Debug methods
    [ContextMenu("Debug UI State")]
    public void DebugUIState()
    {
        Debug.Log("=== UI Manager State ===");
        Debug.Log($"ServerUI: {(serverUI != null ? (serverUI.activeInHierarchy ? "Active" : "Inactive") : "Null")}");
        Debug.Log($"AfterJoin: {(afterJoinUI != null ? (afterJoinUI.activeInHierarchy ? "Active" : "Inactive") : "Null")}");
        Debug.Log($"Host Button: {(hostButton != null ? (hostButton.interactable ? "Enabled" : "Disabled") : "Null")}");
        Debug.Log($"Client Button: {(clientButton != null ? (clientButton.interactable ? "Enabled" : "Disabled") : "Null")}");
        Debug.Log($"Is Host Started: {isHostStarted}");
        
        if (networkManager != null)
        {
            Debug.Log($"Network - IsHost: {networkManager.IsHost}, IsClient: {networkManager.IsClient}, IsConnected: {networkManager.IsConnectedClient}");
        }
    }
    
    [ContextMenu("Force Switch to Character Selection")]
    public void ForceSwitchToCharacterSelection()
    {
        SwitchToCharacterSelection();
    }
    
    [ContextMenu("Force Switch to Menu")]
    public void ForceSwitchToMenu()
    {
        SwitchToMenu();
    }
}