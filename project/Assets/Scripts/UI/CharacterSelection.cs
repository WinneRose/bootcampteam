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
        
        [Header("Character Settings")]
        public Button dewButton;
        public Button solButton;
        
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
            
            Debug.Log($"CharacterSelection NetworkBehaviour spawned - IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
        }
        
        void Start()
        {
            // If not networked yet, hide start button by default
            if (startGameButton != null && !IsSpawned)
            {
                startGameButton.gameObject.SetActive(false);
            }
            
            UpdateUI();
            Debug.Log("CharacterSelection Started!");
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
                    Debug.Log($"Auto-assigned HOST (Client {clientId}) to Dew character");
                }
                else if (!isHost && autoAssignClientToSol && !isSolSelected.Value)
                {
                    // Auto-assign client to Sol
                    isSolSelected.Value = true;
                    solPlayerID.Value = clientId;
                    Debug.Log($"Auto-assigned CLIENT (Client {clientId}) to Sol character");
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
                    Debug.Log("Start Game button set up for HOST");
                }
                else
                {
                    Debug.Log("Start Game button HIDDEN for CLIENT");
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
            Debug.Log($"Server received character selection request: {characterName} from client {clientId}");
            
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
                Debug.Log($"Dew deselected by client {clientId}");
            }
            else if (!isDewSelected.Value)
            {
                // Select Dew
                isDewSelected.Value = true;
                dewPlayerID.Value = clientId;
                Debug.Log($"Dew selected by client {clientId}");
            }
            else
            {
                Debug.Log($"Dew already selected by another player (Player {dewPlayerID.Value})");
            }
        }
        
        private void HandleSolSelection(ulong clientId)
        {
            if (isSolSelected.Value && solPlayerID.Value == clientId)
            {
                // Deselect Sol
                isSolSelected.Value = false;
                solPlayerID.Value = 999999;
                Debug.Log($"Sol deselected by client {clientId}");
            }
            else if (!isSolSelected.Value)
            {
                // Select Sol
                isSolSelected.Value = true;
                solPlayerID.Value = clientId;
                Debug.Log($"Sol selected by client {clientId}");
            }
            else
            {
                Debug.Log($"Sol already selected by another player (Player {solPlayerID.Value})");
            }
        }
        
        // Handle when a new client connects (for late joining)
        public void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            
            Debug.Log($"New client {clientId} connected - checking auto-assignment");
            
            // Check if we need to auto-assign the new client
            bool isNewClientHost = (clientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost);
            
            if (isNewClientHost && autoAssignHostToDew && !isDewSelected.Value)
            {
                isDewSelected.Value = true;
                dewPlayerID.Value = clientId;
                Debug.Log($"Auto-assigned new HOST (Client {clientId}) to Dew character");
                UpdateUIClientRpc();
            }
            else if (!isNewClientHost && autoAssignClientToSol && !isSolSelected.Value)
            {
                isSolSelected.Value = true;
                solPlayerID.Value = clientId;
                Debug.Log($"Auto-assigned new CLIENT (Client {clientId}) to Sol character");
                UpdateUIClientRpc();
            }
        }
        
        // Network variable change handlers
        private void OnDewSelectionChanged(bool previousValue, bool newValue)
        {
            _dewLightEnabled = newValue;
            UpdateDewUI();
            UpdateLights();
            Debug.Log($"Dew selection changed: {previousValue} -> {newValue}");
        }
        
        private void OnSolSelectionChanged(bool previousValue, bool newValue)
        {
            _solLightEnabled = newValue;
            UpdateSolUI();
            UpdateLights();
            Debug.Log($"Sol selection changed: {previousValue} -> {newValue}");
        }
        
        private void OnDewPlayerChanged(ulong previousValue, ulong newValue)
        {
            Debug.Log($"Dew player changed from {previousValue} to {newValue}");
        }
        
        private void OnSolPlayerChanged(ulong previousValue, ulong newValue)
        {
            Debug.Log($"Sol player changed from {previousValue} to {newValue}");
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
            
            Debug.Log($"UI Updated - Dew: {isDewSelected.Value} (Player {dewPlayerID.Value}), Sol: {isSolSelected.Value} (Player {solPlayerID.Value})");
        }
        
        private void UpdateDewUI()
        {
            if (dewCheckbox != null && checkboxActive != null && checkboxPassive != null)
            {
                dewCheckbox.sprite = isDewSelected.Value ? checkboxActive : checkboxPassive;
                Debug.Log($"Dew UI Updated: {(isDewSelected.Value ? "Active" : "Passive")}");
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
                Debug.Log($"Sol UI Updated: {(isSolSelected.Value ? "Active" : "Passive")}");
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
                
                Debug.Log($"Start Game button updated - Can Start: {canStart}");
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
            
            Debug.Log($"Starting game with Dew: {dewPlayerID.Value}, Sol: {solPlayerID.Value}");
            
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
        
        // Public methods to force character assignment (for testing)
        [ContextMenu("Force Auto Assignment")]
        public void ForceAutoAssignment()
        {
            if (IsServer)
            {
                _hasAutoAssigned = false;
                HandleAutoAssignment();
            }
        }
        
        [ContextMenu("Manual Assign Host to Dew")]
        public void ManualAssignHostToDew()
        {
            if (IsServer)
            {
                ulong hostId = NetworkManager.Singleton.LocalClientId;
                isDewSelected.Value = true;
                dewPlayerID.Value = hostId;
                UpdateUIClientRpc();
            }
        }
        
        [ContextMenu("Clear All Selections")]
        public void ClearAllSelections()
        {
            if (IsServer)
            {
                isDewSelected.Value = false;
                isSolSelected.Value = false;
                dewPlayerID.Value = 999999;
                solPlayerID.Value = 999999;
                UpdateUIClientRpc();
            }
        }
        
        // Debug methods
        [ContextMenu("Debug Selection State")]
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