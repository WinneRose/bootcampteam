using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace UI
{
    public class CharacterSelection : NetworkBehaviour
    {
        [Header("Light Settings")]
        public Light dewPointLight;
        public Light solPointLight;
        
        [Header("Checkbox Images")]
        public Image dewCheckbox;
        public Image solCheckbox;
        
        [Header("Checkbox Sprites")]
        public Sprite checkboxActive;
        public Sprite checkboxPassive;
        
        [Header("Game Start")]
        public Button startGameButton;
        public string gameSceneName = "GameScene";
        
        [Header("Auto Assignment Settings")]
        [Tooltip("Should host automatically be assigned to Dew character?")]
        public bool autoAssignHostToDew = true;
        [Tooltip("Should client automatically be assigned to Sol character?")]
        public bool autoAssignClientToSol = true;
        
        // Network variables to sync selection state
        private NetworkVariable<bool> isDewSelected = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> isSolSelected = new NetworkVariable<bool>(false);
        private NetworkVariable<ulong> dewPlayerID = new NetworkVariable<ulong>(999999);
        private NetworkVariable<ulong> solPlayerID = new NetworkVariable<ulong>(999999);
        
        // Local state tracking
        private bool _dewLightEnabled = false;
        private bool _solLightEnabled = false;
        private bool _hasAutoAssigned = false;
        
        public override void OnNetworkSpawn()
        {
            // Subscribe to network variable changes
            isDewSelected.OnValueChanged += OnDewSelectionChanged;
            isSolSelected.OnValueChanged += OnSolSelectionChanged;
            dewPlayerID.OnValueChanged += OnDewPlayerChanged;
            solPlayerID.OnValueChanged += OnSolPlayerChanged;
            
            // Subscribe to client connection events if we're the server
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
            
            // Setup UI
            UpdateUI();
            SetupStartGameButton();
            
            // Auto-assign characters if enabled - with a small delay to ensure everything is ready
            if (IsServer)
            {
                Invoke(nameof(HandleAutoAssignment), 0.1f);
            }
        }
        
        void Start()
        {
            // If not networked yet, hide start button by default
            if (startGameButton != null && !IsSpawned)
            {
                startGameButton.gameObject.SetActive(false);
            }
            
            UpdateUI();
        }
        
        private void HandleAutoAssignment()
        {
            if (_hasAutoAssigned) return;
            
            _hasAutoAssigned = true;
            
            // Get all connected clients
            var connectedClients = NetworkManager.Singleton.ConnectedClients;
            
            foreach (var client in connectedClients)
            {
                ulong clientId = client.Key;
                bool isHost = (clientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost);
                
                if (isHost && autoAssignHostToDew && !isDewSelected.Value)
                {
                    // Auto-assign host to Dew
                    isDewSelected.Value = true;
                    dewPlayerID.Value = clientId;
                }
                else if (!isHost && autoAssignClientToSol && !isSolSelected.Value)
                {
                    // Auto-assign client to Sol
                    isSolSelected.Value = true;
                    solPlayerID.Value = clientId;
                }
            }
            
            // Update UI for all clients
            UpdateUIClientRpc();
        }
        
        private void SetupStartGameButton()
        {
            if (startGameButton != null)
            {
                // Only host can see/use start game button
                bool shouldShowButton = IsHost;
                startGameButton.gameObject.SetActive(shouldShowButton);
                
                if (shouldShowButton)
                {
                    startGameButton.onClick.RemoveAllListeners();
                    startGameButton.onClick.AddListener(OnStartGameClicked);
                }
            }
            else
            {
                Debug.LogWarning("Start Game button reference not assigned!");
            }
        }
        
        // PUBLIC methods for button events (manual selection)
        public void OnDewButtonClick()
        {
            RequestCharacterSelectionServerRpc("Dew", NetworkManager.Singleton.LocalClientId);
        }
        
        public void OnSolButtonClick()
        {
            RequestCharacterSelectionServerRpc("Sol", NetworkManager.Singleton.LocalClientId);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void RequestCharacterSelectionServerRpc(string characterName, ulong clientId)
        {
            if (characterName == "Dew")
            {
                HandleDewSelection(clientId);
            }
            else if (characterName == "Sol")
            {
                HandleSolSelection(clientId);
            }
            
            // Update UI for all clients
            UpdateUIClientRpc();
        }
        
        private void HandleDewSelection(ulong clientId)
        {
            if (isDewSelected.Value && dewPlayerID.Value == clientId)
            {
                // Deselect Dew
                isDewSelected.Value = false;
                dewPlayerID.Value = 999999;
            }
            else if (!isDewSelected.Value)
            {
                // Select Dew
                isDewSelected.Value = true;
                dewPlayerID.Value = clientId;
            }
        }
        
        private void HandleSolSelection(ulong clientId)
        {
            if (isSolSelected.Value && solPlayerID.Value == clientId)
            {
                // Deselect Sol
                isSolSelected.Value = false;
                solPlayerID.Value = 999999;
            }
            else if (!isSolSelected.Value)
            {
                // Select Sol
                isSolSelected.Value = true;
                solPlayerID.Value = clientId;
            }
        }
        
        // Handle when a new client connects (for late joining)
        public void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            
            // Check if we need to auto-assign the new client
            bool isNewClientHost = (clientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost);
            
            if (isNewClientHost && autoAssignHostToDew && !isDewSelected.Value)
            {
                isDewSelected.Value = true;
                dewPlayerID.Value = clientId;
                UpdateUIClientRpc();
            }
            else if (!isNewClientHost && autoAssignClientToSol && !isSolSelected.Value)
            {
                isSolSelected.Value = true;
                solPlayerID.Value = clientId;
                UpdateUIClientRpc();
            }
        }
        
        // Network variable change handlers
        private void OnDewSelectionChanged(bool previousValue, bool newValue)
        {
            _dewLightEnabled = newValue;
            UpdateDewUI();
            UpdateLights();
        }
        
        private void OnSolSelectionChanged(bool previousValue, bool newValue)
        {
            _solLightEnabled = newValue;
            UpdateSolUI();
            UpdateLights();
        }
        
        private void OnDewPlayerChanged(ulong previousValue, ulong newValue)
        {
            // Handle player ID changes if needed
        }
        
        private void OnSolPlayerChanged(ulong previousValue, ulong newValue)
        {
            // Handle player ID changes if needed
        }
        
        [ClientRpc]
        private void UpdateUIClientRpc()
        {
            UpdateUI();
        }
        
        // UI Update methods
        private void UpdateUI()
        {
            UpdateDewUI();
            UpdateSolUI();
            UpdateLights();
            UpdateStartGameButton();
        }
        
        private void UpdateDewUI()
        {
            if (dewCheckbox != null && checkboxActive != null && checkboxPassive != null)
            {
                dewCheckbox.sprite = isDewSelected.Value ? checkboxActive : checkboxPassive;
            }
            else
            {
                Debug.LogWarning("Dew checkbox or sprites not assigned!");
            }
        }
        
        private void UpdateSolUI()
        {
            if (solCheckbox != null && checkboxActive != null && checkboxPassive != null)
            {
                solCheckbox.sprite = isSolSelected.Value ? checkboxActive : checkboxPassive;
            }
            else
            {
                Debug.LogWarning("Sol checkbox or sprites not assigned!");
            }
        }
        
        private void UpdateLights()
        {
            if (dewPointLight != null)
            {
                dewPointLight.enabled = _dewLightEnabled;
            }
            
            if (solPointLight != null)
            {
                solPointLight.enabled = _solLightEnabled;
            }
        }
        
        private void UpdateStartGameButton()
        {
            // Only update if this is the host and button exists
            if (startGameButton != null && IsHost)
            {
                // Enable start game button only if both characters are selected
                bool canStart = isDewSelected.Value && isSolSelected.Value;
                startGameButton.interactable = canStart;
                
                // Update button text
                Text buttonText = startGameButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    if (canStart)
                    {
                        buttonText.text = "Start Game";
                        buttonText.color = Color.white;
                    }
                    else
                    {
                        string waitingFor = "";
                        if (!isDewSelected.Value && !isSolSelected.Value)
                        {
                            waitingFor = "Waiting for players...";
                        }
                        else if (!isDewSelected.Value)
                        {
                            waitingFor = "Waiting for Dew player...";
                        }
                        else if (!isSolSelected.Value)
                        {
                            waitingFor = "Waiting for Sol player...";
                        }
                        
                        buttonText.text = waitingFor;
                        buttonText.color = Color.gray;
                    }
                }
            }
        }
        
        public void OnStartGameClicked()
        {
            if (!IsHost)
            {
                Debug.LogWarning("Only host can start the game!");
                return;
            }
            
            if (!isDewSelected.Value || !isSolSelected.Value)
            {
                Debug.LogWarning("Both characters must be selected before starting!");
                return;
            }
            
            // Store selection data before changing scenes
            CharacterSelectionManager.SetDewSelection(dewPlayerID.Value, null);
            CharacterSelectionManager.SetSolSelection(solPlayerID.Value, null);
            
            // Load game scene
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        
        // Public query methods
        public bool IsDewSelected() => isDewSelected.Value;
        public bool IsSolSelected() => isSolSelected.Value;
        public ulong GetDewPlayerID() => dewPlayerID.Value;
        public ulong GetSolPlayerID() => solPlayerID.Value;
        
        public string GetCharacterForPlayer(ulong clientId)
        {
            if (dewPlayerID.Value == clientId) return "Dew";
            if (solPlayerID.Value == clientId) return "Sol";
            return "None";
        }
        
        // Context Menu Debug Methods
        [ContextMenu("Debug: Show Selection State")]
        public void DebugSelectionState()
        {
            Debug.Log("=== Character Selection State ===");
            Debug.Log($"Dew Selected: {isDewSelected.Value} (Player: {dewPlayerID.Value})");
            Debug.Log($"Sol Selected: {isSolSelected.Value} (Player: {solPlayerID.Value})");
            Debug.Log($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
            Debug.Log($"Is Host: {IsHost}");
            Debug.Log($"Is Client: {IsClient}");
            Debug.Log($"Is Spawned: {IsSpawned}");
            Debug.Log($"Auto-assigned: {_hasAutoAssigned}");
            
            if (startGameButton != null)
            {
                Debug.Log($"Start Button Active: {startGameButton.gameObject.activeInHierarchy}");
                Debug.Log($"Start Button Interactable: {startGameButton.interactable}");
            }
            else
            {
                Debug.Log("Start Button: NULL");
            }
        }
        
        [ContextMenu("Debug: Show UI Components")]
        public void DebugUIComponents()
        {
            Debug.Log("=== UI Components State ===");
            Debug.Log($"Dew Light: {(dewPointLight != null ? "Found" : "NULL")} - Enabled: {(dewPointLight != null ? dewPointLight.enabled.ToString() : "N/A")}");
            Debug.Log($"Sol Light: {(solPointLight != null ? "Found" : "NULL")} - Enabled: {(solPointLight != null ? solPointLight.enabled.ToString() : "N/A")}");
            Debug.Log($"Dew Checkbox: {(dewCheckbox != null ? "Found" : "NULL")}");
            Debug.Log($"Sol Checkbox: {(solCheckbox != null ? "Found" : "NULL")}");
            Debug.Log($"Checkbox Active Sprite: {(checkboxActive != null ? "Found" : "NULL")}");
            Debug.Log($"Checkbox Passive Sprite: {(checkboxPassive != null ? "Found" : "NULL")}");
            Debug.Log($"Start Game Button: {(startGameButton != null ? "Found" : "NULL")}");
        }
        
        [ContextMenu("Debug: Show Network State")]
        public void DebugNetworkState()
        {
            Debug.Log("=== Network State ===");
            Debug.Log($"Network Manager: {(NetworkManager.Singleton != null ? "Found" : "NULL")}");
            
            if (NetworkManager.Singleton != null)
            {
                Debug.Log($"Is Host: {NetworkManager.Singleton.IsHost}");
                Debug.Log($"Is Client: {NetworkManager.Singleton.IsClient}");
                Debug.Log($"Is Server: {NetworkManager.Singleton.IsServer}");
                Debug.Log($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
                Debug.Log($"Connected Clients: {NetworkManager.Singleton.ConnectedClients.Count}");
                
                foreach (var client in NetworkManager.Singleton.ConnectedClients)
                {
                    Debug.Log($"  Client {client.Key}");
                }
            }
        }
        
        [ContextMenu("Debug: Show Auto Assignment Settings")]
        public void DebugAutoAssignmentSettings()
        {
            Debug.Log("=== Auto Assignment Settings ===");
            Debug.Log($"Auto Assign Host to Dew: {autoAssignHostToDew}");
            Debug.Log($"Auto Assign Client to Sol: {autoAssignClientToSol}");
            Debug.Log($"Has Auto Assigned: {_hasAutoAssigned}");
            Debug.Log($"Game Scene Name: {gameSceneName}");
        }
        
        [ContextMenu("Debug: Force Auto Assignment")]
        public void DebugForceAutoAssignment()
        {
            if (IsServer)
            {
                _hasAutoAssigned = false;
                HandleAutoAssignment();
                Debug.Log("Forced auto assignment");
            }
            else
            {
                Debug.LogWarning("Can only force auto assignment on server!");
            }
        }
        
        [ContextMenu("Debug: Manual Assign Host to Dew")]
        public void DebugManualAssignHostToDew()
        {
            if (IsServer)
            {
                ulong hostId = NetworkManager.Singleton.LocalClientId;
                isDewSelected.Value = true;
                dewPlayerID.Value = hostId;
                UpdateUIClientRpc();
                Debug.Log($"Manually assigned host (Client {hostId}) to Dew");
            }
            else
            {
                Debug.LogWarning("Can only assign characters on server!");
            }
        }
        
        [ContextMenu("Debug: Manual Assign Client to Sol")]
        public void DebugManualAssignClientToSol()
        {
            if (IsServer)
            {
                // Find first non-host client
                foreach (var client in NetworkManager.Singleton.ConnectedClients)
                {
                    if (client.Key != NetworkManager.Singleton.LocalClientId)
                    {
                        isSolSelected.Value = true;
                        solPlayerID.Value = client.Key;
                        UpdateUIClientRpc();
                        Debug.Log($"Manually assigned client {client.Key} to Sol");
                        break;
                    }
                }
            }
            else
            {
                Debug.LogWarning("Can only assign characters on server!");
            }
        }
        
        [ContextMenu("Debug: Clear All Selections")]
        public void DebugClearAllSelections()
        {
            if (IsServer)
            {
                isDewSelected.Value = false;
                isSolSelected.Value = false;
                dewPlayerID.Value = 999999;
                solPlayerID.Value = 999999;
                _hasAutoAssigned = false;
                UpdateUIClientRpc();
                Debug.Log("Cleared all character selections");
            }
            else
            {
                Debug.LogWarning("Can only clear selections on server!");
            }
        }
        
        [ContextMenu("Debug: Test Dew Selection")]
        public void DebugTestDewSelection()
        {
            OnDewButtonClick();
            Debug.Log("Tested Dew selection button");
        }
        
        [ContextMenu("Debug: Test Sol Selection")]
        public void DebugTestSolSelection()
        {
            OnSolButtonClick();
            Debug.Log("Tested Sol selection button");
        }
        
        [ContextMenu("Debug: Test Start Game")]
        public void DebugTestStartGame()
        {
            if (IsHost)
            {
                OnStartGameClicked();
                Debug.Log("Tested start game button");
            }
            else
            {
                Debug.LogWarning("Only host can start the game!");
            }
        }
        
        [ContextMenu("Debug: Show Character Assignment")]
        public void DebugShowCharacterAssignment()
        {
            Debug.Log("=== Character Assignment ===");
            
            if (NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                string myCharacter = GetCharacterForPlayer(localId);
                Debug.Log($"My Client ID: {localId}");
                Debug.Log($"My Character: {myCharacter}");
                
                Debug.Log("All Assignments:");
                if (isDewSelected.Value)
                {
                    Debug.Log($"  Dew: Client {dewPlayerID.Value}");
                }
                else
                {
                    Debug.Log("  Dew: Not Selected");
                }
                
                if (isSolSelected.Value)
                {
                    Debug.Log($"  Sol: Client {solPlayerID.Value}");
                }
                else
                {
                    Debug.Log("  Sol: Not Selected");
                }
            }
        }
        
        public override void OnDestroy()
        {
            // Unsubscribe from events
            if (isDewSelected != null)
            {
                isDewSelected.OnValueChanged -= OnDewSelectionChanged;
                isSolSelected.OnValueChanged -= OnSolSelectionChanged;
                dewPlayerID.OnValueChanged -= OnDewPlayerChanged;
                solPlayerID.OnValueChanged -= OnSolPlayerChanged;
            }
            
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameClicked);
            }
            
            // Unsubscribe from network events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }
    }
}