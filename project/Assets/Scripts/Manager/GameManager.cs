using UnityEngine;
using Unity.Netcode;
using System.Collections;

// Character Selection Data Structure
[System.Serializable]
public class CharacterSelectionData
{
    public bool isDewSelected;
    public bool isSolSelected;
    public ulong dewPlayerID = 999999;
    public ulong solPlayerID = 999999;
    public GameObject dewPrefab;
    public GameObject solPrefab;
    
    public override string ToString()
    {
        return $"Dew: {isDewSelected} (Player {dewPlayerID}), Sol: {isSolSelected} (Player {solPlayerID})";
    }
}

// Static Character Selection Manager with DontDestroyOnLoad persistence
public static class CharacterSelectionManager
{
    private static CharacterSelectionData _selectionData;
    private static GameObject _persistentObject;
    
    public static CharacterSelectionData SelectionData
    {
        get
        {
            if (_selectionData == null)
            {
                _selectionData = new CharacterSelectionData();
            }
            return _selectionData;
        }
        set
        {
            _selectionData = value;
        }
    }
    
    public static void SetDewSelection(ulong playerId, GameObject prefab)
    {
        SelectionData.isDewSelected = true;
        SelectionData.dewPlayerID = playerId;
        SelectionData.dewPrefab = prefab;
        CreatePersistentObject();
    }
    
    public static void SetSolSelection(ulong playerId, GameObject prefab)
    {
        SelectionData.isSolSelected = true;
        SelectionData.solPlayerID = playerId;
        SelectionData.solPrefab = prefab;
        CreatePersistentObject();
    }
    
    private static void CreatePersistentObject()
    {
        if (_persistentObject == null)
        {
            _persistentObject = new GameObject("CharacterSelectionPersistence");
            Object.DontDestroyOnLoad(_persistentObject);
        }
    }
    
    public static void ClearSelections()
    {
        _selectionData = new CharacterSelectionData();
        if (_persistentObject != null)
        {
            Object.Destroy(_persistentObject);
            _persistentObject = null;
        }
    }
    
    public static bool HasValidSelections()
    {
        return _selectionData != null && 
               _selectionData.isDewSelected && 
               _selectionData.isSolSelected &&
               _selectionData.dewPlayerID != 999999 && 
               _selectionData.solPlayerID != 999999;
    }
}

public class GameManager : NetworkBehaviour
{
    [Header("Spawn Points")]
    public Transform dewSpawnPoint;
    public Transform solSpawnPoint;
    
    [Header("Default Character Prefabs (fallback)")]
    public GameObject defaultDewPrefab;
    public GameObject defaultSolPrefab;
    
    [Header("Settings")]
    public bool autoSpawnOnSceneLoad = true;
    public float spawnDelay = 2f;
    public float clientWaitTime = 3f;
    
    // Network variables to track spawned players
    private NetworkVariable<bool> _dewSpawned = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> _solSpawned = new NetworkVariable<bool>(false);
    
    // Network variables for spawn positions (to ensure sync)
    private NetworkVariable<Vector3> _dewSpawnPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> _solSpawnPosition = new NetworkVariable<Vector3>();
    
    // References to spawned player objects
    private GameObject _spawnedDewPlayer;
    private GameObject _spawnedSolPlayer;
    
    public override void OnNetworkSpawn()
    {
        Debug.Log($"GameManager NetworkSpawn - IsServer: {IsServer}, IsHost: {IsHost}, IsClient: {IsClient}");
        
        if (IsServer)
        {
            // Initialize spawn positions IMMEDIATELY
            ValidateSpawnPoints();
            
            Vector3 dewPos = dewSpawnPoint != null ? dewSpawnPoint.position : transform.position + Vector3.left * 2f;
            Vector3 solPos = solSpawnPoint != null ? solSpawnPoint.position : transform.position + Vector3.right * 2f;
            
            _dewSpawnPosition.Value = dewPos;
            _solSpawnPosition.Value = solPos;
            
            Debug.Log($"Server initialized spawn positions - Dew: {dewPos}, Sol: {solPos}");
            
            // Wait for all clients to connect before spawning
            if (autoSpawnOnSceneLoad)
            {
                StartCoroutine(WaitForClientsAndSpawn());
            }
        }
        
        // Subscribe to spawn position changes on all clients
        _dewSpawnPosition.OnValueChanged += OnDewSpawnPositionChanged;
        _solSpawnPosition.OnValueChanged += OnSolSpawnPositionChanged;
        
        // On clients, log the received spawn positions
        if (!IsServer)
        {
            Debug.Log($"[CLIENT] Received spawn positions - Dew: {_dewSpawnPosition.Value}, Sol: {_solSpawnPosition.Value}");
        }
    }
    
    private void OnDewSpawnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        Debug.Log($"[CLIENT] Dew spawn position updated: {newValue}");
    }
    
    private void OnSolSpawnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        Debug.Log($"[CLIENT] Sol spawn position updated: {newValue}");
    }
    
    private IEnumerator WaitForClientsAndSpawn()
    {
        Debug.Log("Waiting for clients before spawning...");
        
        // Ensure spawn points are valid FIRST
        ValidateSpawnPoints();
        
        // Set network spawn positions immediately
        Vector3 dewPos = dewSpawnPoint != null ? dewSpawnPoint.position : transform.position + Vector3.left * 2f;
        Vector3 solPos = solSpawnPoint != null ? solSpawnPoint.position : transform.position + Vector3.right * 2f;
        
        _dewSpawnPosition.Value = dewPos;
        _solSpawnPosition.Value = solPos;
        
        Debug.Log($"Server set spawn positions - Dew: {dewPos}, Sol: {solPos}");
        
        // Wait for initial spawn delay
        yield return new WaitForSeconds(spawnDelay);
        
        // Wait for clients to be ready
        int maxWaitTime = 10; // Maximum 10 seconds
        int waitedTime = 0;
        
        while (NetworkManager.Singleton.ConnectedClients.Count < 2 && waitedTime < maxWaitTime)
        {
            Debug.Log($"Waiting for clients... Current count: {NetworkManager.Singleton.ConnectedClients.Count}");
            yield return new WaitForSeconds(1f);
            waitedTime++;
        }
        
        // Additional wait for clients to fully load the scene
        yield return new WaitForSeconds(clientWaitTime);
        
        Debug.Log($"Starting spawn process. Connected clients: {NetworkManager.Singleton.ConnectedClients.Count}");
        Debug.Log($"Final spawn positions - Dew: {_dewSpawnPosition.Value}, Sol: {_solSpawnPosition.Value}");
        
        SpawnPlayersFromSelection();
    }
    
    private void ValidateSpawnPoints()
    {
        if (dewSpawnPoint == null)
        {
            Debug.LogError("DEW SPAWN POINT IS NULL! Looking for fallback...");
            GameObject dewSpawn = GameObject.Find("DewSpawnPoint");
            if (dewSpawn != null)
            {
                dewSpawnPoint = dewSpawn.transform;
                Debug.Log($"Found fallback Dew spawn point: {dewSpawn.name}");
            }
        }
        
        if (solSpawnPoint == null)
        {
            Debug.LogError("SOL SPAWN POINT IS NULL! Looking for fallback...");
            GameObject solSpawn = GameObject.Find("SolSpawnPoint");
            if (solSpawn != null)
            {
                solSpawnPoint = solSpawn.transform;
                Debug.Log($"Found fallback Sol spawn point: {solSpawn.name}");
            }
        }
        
        // Final validation
        if (dewSpawnPoint != null)
        {
            Debug.Log($"Dew spawn point validated: {dewSpawnPoint.name} at {dewSpawnPoint.position}");
        }
        if (solSpawnPoint != null)
        {
            Debug.Log($"Sol spawn point validated: {solSpawnPoint.name} at {solSpawnPoint.position}");
        }
    }
    
    private void SpawnPlayersFromSelection()
    {
        if (!IsServer) return;
        
        CharacterSelectionData selectionData = CharacterSelectionManager.SelectionData;
        
        Debug.Log($"Spawning players from selection. Connected clients: {NetworkManager.Singleton.ConnectedClients.Count}");
        
        if (selectionData == null || !CharacterSelectionManager.HasValidSelections())
        {
            Debug.Log("No valid selection data, using fallback spawn");
            SpawnPlayersFallback();
            return;
        }
        
        // Spawn with a small delay between each spawn
        StartCoroutine(SpawnPlayersSequentially(selectionData));
    }
    
    private IEnumerator SpawnPlayersSequentially(CharacterSelectionData selectionData)
    {
        // Spawn Dew character first
        if (selectionData.isDewSelected && selectionData.dewPlayerID != 999999)
        {
            if (IsClientConnected(selectionData.dewPlayerID))
            {
                Debug.Log($"Spawning Dew for player {selectionData.dewPlayerID}");
                SpawnCharacterForPlayer(selectionData.dewPlayerID, "Dew", selectionData.dewPrefab ?? defaultDewPrefab);
                yield return new WaitForSeconds(0.5f); // Small delay between spawns
            }
            else
            {
                Debug.LogWarning($"Dew player {selectionData.dewPlayerID} is not connected!");
            }
        }
        
        // Spawn Sol character
        if (selectionData.isSolSelected && selectionData.solPlayerID != 999999)
        {
            if (IsClientConnected(selectionData.solPlayerID))
            {
                Debug.Log($"Spawning Sol for player {selectionData.solPlayerID}");
                SpawnCharacterForPlayer(selectionData.solPlayerID, "Sol", selectionData.solPrefab ?? defaultSolPrefab);
            }
            else
            {
                Debug.LogWarning($"Sol player {selectionData.solPlayerID} is not connected!");
            }
        }
    }
    
    private bool IsClientConnected(ulong clientId)
    {
        bool isConnected = NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId);
        Debug.Log($"Client {clientId} connected: {isConnected}");
        return isConnected;
    }
    
    private void SpawnPlayersFallback()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        Debug.Log($"Fallback spawn for {connectedClients.Count} connected clients");
        
        // Get host/server client ID (this should be the first one and spawn as Dew)
        ulong hostClientId = NetworkManager.Singleton.LocalClientId;
        
        // Find the other client (this should spawn as Sol)
        ulong clientId = 999999;
        foreach (var client in connectedClients)
        {
            if (client.Key != hostClientId)
            {
                clientId = client.Key;
                break;
            }
        }
        
        // Spawn host as Dew
        Debug.Log($"Fallback: Spawning Host/Server {hostClientId} as Dew");
        SpawnCharacterForPlayer(hostClientId, "Dew", defaultDewPrefab);
        
        // Spawn client as Sol (if found)
        if (clientId != 999999)
        {
            Debug.Log($"Fallback: Spawning Client {clientId} as Sol");
            SpawnCharacterForPlayer(clientId, "Sol", defaultSolPrefab);
        }
        else
        {
            Debug.LogWarning("No client found to spawn as Sol in fallback mode!");
        }
    }
    
    private void SpawnCharacterForPlayer(ulong clientId, string characterType, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"No prefab assigned for {characterType} character!");
            return;
        }
        
        // Check if already spawned
        if (characterType == "Dew" && _dewSpawned.Value)
        {
            Debug.Log($"Dew already spawned, skipping");
            return;
        }
        if (characterType == "Sol" && _solSpawned.Value)
        {
            Debug.Log($"Sol already spawned, skipping");
            return;
        }
        
        // Determine spawn position and rotation directly from spawn points
        Vector3 spawnPosition;
        Quaternion spawnRotation = Quaternion.identity;
        
        if (characterType == "Dew")
        {
            if (dewSpawnPoint != null)
            {
                spawnPosition = dewSpawnPoint.position;
                spawnRotation = dewSpawnPoint.rotation;
            }
            else
            {
                spawnPosition = _dewSpawnPosition.Value != Vector3.zero ? _dewSpawnPosition.Value : transform.position + Vector3.left * 2f;
            }
        }
        else // Sol
        {
            if (solSpawnPoint != null)
            {
                spawnPosition = solSpawnPoint.position;
                spawnRotation = solSpawnPoint.rotation;
            }
            else
            {
                spawnPosition = _solSpawnPosition.Value != Vector3.zero ? _solSpawnPosition.Value : transform.position + Vector3.right * 2f;
            }
        }
        
        Debug.Log($"Spawning {characterType} for client {clientId} at position: {spawnPosition} (from {(characterType == "Dew" ? "dewSpawnPoint" : "solSpawnPoint")})");
        
        // Validate spawn position is not zero
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogError($"Spawn position is zero for {characterType}! Using fallback position.");
            spawnPosition = transform.position + (characterType == "Dew" ? Vector3.left * 2f : Vector3.right * 2f);
        }
        
        try
        {
            GameObject playerInstance = Instantiate(prefab, spawnPosition, spawnRotation);
            
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"Character prefab {prefab.name} does not have NetworkObject component!");
                Destroy(playerInstance);
                return;
            }
            
            // Spawn with ownership for the specific client
            networkObject.SpawnWithOwnership(clientId);
            
            // Store references and update network variables
            if (characterType == "Dew")
            {
                _spawnedDewPlayer = playerInstance;
                _dewSpawned.Value = true;
            }
            else if (characterType == "Sol")
            {
                _spawnedSolPlayer = playerInstance;
                _solSpawned.Value = true;
            }
            
            Debug.Log($"{characterType} spawned successfully for client {clientId} at {spawnPosition}");
            
            // Notify all clients about the spawn with the exact position
            NotifyPlayerSpawnedClientRpc(clientId, characterType, spawnPosition);
            
            // Force position synchronization with a longer delay to ensure network sync
            StartCoroutine(EnsureCorrectPositionExtended(playerInstance, spawnPosition, clientId, characterType));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to spawn {characterType} character: {e.Message}");
        }
    }
    
    private IEnumerator EnsureCorrectPositionExtended(GameObject playerInstance, Vector3 correctPosition, ulong ownerId, string characterType)
    {
        // Wait longer for network object to be fully spawned and synced
        yield return new WaitForSeconds(0.5f);
        
        int attempts = 0;
        int maxAttempts = 15; // More attempts for better reliability
        
        Debug.Log($"Starting position correction for {characterType} (Client {ownerId}). Target position: {correctPosition}");
        
        while (playerInstance != null && attempts < maxAttempts)
        {
            Vector3 currentPosition = playerInstance.transform.position;
            float distance = Vector3.Distance(currentPosition, correctPosition);
            
            Debug.Log($"Attempt {attempts + 1}: {characterType} current position: {currentPosition}, target: {correctPosition}, distance: {distance}");
            
            if (distance > 0.1f) // If position is off by more than 0.1 units
            {
                Debug.Log($"Correcting position for {characterType} (Client {ownerId})");
                
                // Disable character controller and rigidbody temporarily if they exist
                CharacterController cc = playerInstance.GetComponent<CharacterController>();
                Rigidbody rb = playerInstance.GetComponent<Rigidbody>();
                
                if (cc != null)
                {
                    cc.enabled = false;
                }
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                
                // Set the correct position
                playerInstance.transform.position = correctPosition;
                
                yield return new WaitForFixedUpdate(); // Wait for physics update
                
                // Re-enable components
                if (cc != null)
                {
                    cc.enabled = true;
                }
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
                
                // Send position update to all clients with character type info
                ForcePositionUpdateWithTypeClientRpc(ownerId, correctPosition, characterType);
                
                yield return new WaitForSeconds(0.2f); // Longer wait between attempts
                attempts++;
            }
            else
            {
                Debug.Log($"Position verified for {characterType} (Client {ownerId}) at {currentPosition}");
                break;
            }
        }
        
        if (attempts >= maxAttempts)
        {
            Debug.LogError($"Failed to correct position for {characterType} (Client {ownerId}) after {maxAttempts} attempts. Final position: {playerInstance.transform.position}");
            
            // Final attempt - force teleport
            ForceResetPositionClientRpc(ownerId, correctPosition, characterType);
        }
        else
        {
            Debug.Log($"Successfully positioned {characterType} for client {ownerId} after {attempts + 1} attempts");
        }
    }
    

    [ClientRpc]
    private void ForcePositionUpdateWithTypeClientRpc(ulong playerId, Vector3 correctPosition, string characterType)
    {
        StartCoroutine(ForcePositionOnClient(playerId, correctPosition, characterType));
    }
    
    [ClientRpc]
    private void ForceResetPositionClientRpc(ulong playerId, Vector3 correctPosition, string characterType)
    {
        Debug.Log($"[CLIENT] Force reset position for {characterType} (Client {playerId}) to {correctPosition}");
        StartCoroutine(ForcePositionOnClient(playerId, correctPosition, characterType));
    }
    
    private IEnumerator ForcePositionOnClient(ulong playerId, Vector3 correctPosition, string characterType)
    {
        // Find the player object for this client ID
        GameObject playerObject = GetPlayerByClientId(playerId);
        if (playerObject == null)
        {
            // Try finding by character type name if client ID lookup fails
            playerObject = GameObject.Find($"{characterType}(Clone)");
            if (playerObject == null)
            {
                Debug.LogWarning($"[CLIENT] Could not find player object for {characterType} (Client {playerId})");
                yield break;
            }
        }
        
        Debug.Log($"[CLIENT] Found player object: {playerObject.name} for {characterType} (Client {playerId})");
        
        // Disable movement components
        CharacterController cc = playerObject.GetComponent<CharacterController>();
        Rigidbody rb = playerObject.GetComponent<Rigidbody>();
        
        if (cc != null) cc.enabled = false;
        if (rb != null) rb.isKinematic = true;
        
        yield return null;
        
        // Set position
        playerObject.transform.position = correctPosition;
        
        yield return null;
        
        // Re-enable components
        if (cc != null) cc.enabled = true;
        if (rb != null) rb.isKinematic = false;
        
        Debug.Log($"[CLIENT] Updated position for {characterType} (Client {playerId}) to {correctPosition}");
    }
    
    [ClientRpc]
    private void NotifyPlayerSpawnedClientRpc(ulong clientId, string characterType, Vector3 spawnPosition)
    {
        Debug.Log($"[CLIENT] Player spawned notification: {characterType} for client {clientId} at {spawnPosition}");
    }
    
    [ClientRpc]
    private void ForcePositionUpdateClientRpc(ulong playerId, Vector3 correctPosition)
    {
        // Find the player object for this client ID
        GameObject playerObject = GetPlayerByClientId(playerId);
        if (playerObject != null)
        {
            CharacterController cc = playerObject.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }
            
            playerObject.transform.position = correctPosition;
            
            if (cc != null)
            {
                cc.enabled = true;
            }
            
            Debug.Log($"[CLIENT] Force updated position for player {playerId} to {correctPosition}");
        }
    }
    
    // Public methods for manual spawning
    public void SpawnAsHost()
    {
        if (!IsServer) return;
        
        ulong hostClientId = NetworkManager.Singleton.LocalClientId;
        SpawnCharacterForPlayer(hostClientId, "Dew", defaultDewPrefab);
    }
    
    public void SpawnAsClient()
    {
        if (!IsServer) return;
        
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            if (client.Key != NetworkManager.Singleton.LocalClientId)
            {
                SpawnCharacterForPlayer(client.Key, "Sol", defaultSolPrefab);
                break;
            }
        }
    }
    
    // Helper methods
    public bool IsDewSpawned() => _dewSpawned.Value;
    public bool IsSolSpawned() => _solSpawned.Value;
    public bool AreAllPlayersSpawned() => _dewSpawned.Value && _solSpawned.Value;
    
    public GameObject GetDewPlayer() => _spawnedDewPlayer;
    public GameObject GetSolPlayer() => _spawnedSolPlayer;
    
    public GameObject GetPlayerByClientId(ulong clientId)
    {
        // Check all NetworkObjects to find the one owned by this client
        foreach (var networkObject in FindObjectsOfType<NetworkObject>())
        {
            if (networkObject.OwnerClientId == clientId)
            {
                // Check if it's a player object (has expected components)
                if (networkObject.gameObject.GetComponent<CharacterController>() != null ||
                    networkObject.gameObject.name.Contains("Dew") ||
                    networkObject.gameObject.name.Contains("Sol"))
                {
                    return networkObject.gameObject;
                }
            }
        }
        
        return null;
    }
    
    // Context Menu Debug Methods
    [ContextMenu("Debug: Show Spawn Points")]
    public void DebugSpawnPoints()
    {
        Debug.Log("=== SPAWN POINTS DEBUG ===");
        
        if (dewSpawnPoint != null)
        {
            Debug.Log($"Dew Spawn Point: {dewSpawnPoint.name}");
            Debug.Log($"  Position: {dewSpawnPoint.position}");
            Debug.Log($"  Network Position: {_dewSpawnPosition.Value}");
            Debug.Log($"  Rotation: {dewSpawnPoint.rotation}");
            Debug.Log($"  Active: {dewSpawnPoint.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("DEW SPAWN POINT IS NULL!");
        }
        
        if (solSpawnPoint != null)
        {
            Debug.Log($"Sol Spawn Point: {solSpawnPoint.name}");
            Debug.Log($"  Position: {solSpawnPoint.position}");
            Debug.Log($"  Network Position: {_solSpawnPosition.Value}");
            Debug.Log($"  Rotation: {solSpawnPoint.rotation}");
            Debug.Log($"  Active: {solSpawnPoint.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("SOL SPAWN POINT IS NULL!");
        }
        
        Debug.Log($"GameManager Position: {transform.position}");
    }
    
    [ContextMenu("Debug: Show Spawn State")]
    public void DebugSpawnState()
    {
        Debug.Log("=== Game Manager Spawn State ===");
        Debug.Log($"Is Server: {IsServer}");
        Debug.Log($"Dew Spawned: {_dewSpawned.Value}");
        Debug.Log($"Sol Spawned: {_solSpawned.Value}");
        Debug.Log($"Dew Player Object: {(_spawnedDewPlayer != null ? _spawnedDewPlayer.name : "null")}");
        Debug.Log($"Sol Player Object: {(_spawnedSolPlayer != null ? _spawnedSolPlayer.name : "null")}");
        
        CharacterSelectionData selectionData = CharacterSelectionManager.SelectionData;
        if (selectionData != null)
        {
            Debug.Log($"Selection Data: {selectionData}");
            Debug.Log($"Has Valid Selections: {CharacterSelectionManager.HasValidSelections()}");
        }
        else
        {
            Debug.Log("No selection data available");
        }
        
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"Connected Clients: {NetworkManager.Singleton.ConnectedClients.Count}");
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"  Client {client.Key}");
            }
        }
    }
    
    [ContextMenu("Debug: Force Validate Spawn Points")]
    public void DebugForceValidateSpawnPoints()
    {
        ValidateSpawnPoints();
    }
    
    [ContextMenu("Debug: Force Spawn All Players")]
    public void DebugForceSpawnAllPlayers()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Can only force spawn on server!");
            return;
        }
        
        Debug.Log("Force spawning all players...");
        ValidateSpawnPoints();
        SpawnPlayersFromSelection();
    }
    
    [ContextMenu("Debug: Clear All Spawned Players")]
    public void DebugClearAllSpawnedPlayers()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Can only clear spawned players on server!");
            return;
        }
        
        if (_spawnedDewPlayer != null)
        {
            NetworkObject dewNetObj = _spawnedDewPlayer.GetComponent<NetworkObject>();
            if (dewNetObj != null && dewNetObj.IsSpawned)
            {
                dewNetObj.Despawn();
            }
            _spawnedDewPlayer = null;
            _dewSpawned.Value = false;
        }
        
        if (_spawnedSolPlayer != null)
        {
            NetworkObject solNetObj = _spawnedSolPlayer.GetComponent<NetworkObject>();
            if (solNetObj != null && solNetObj.IsSpawned)
            {
                solNetObj.Despawn();
            }
            _spawnedSolPlayer = null;
            _solSpawned.Value = false;
        }
        
        Debug.Log("All spawned players cleared");
    }
    
    public override void OnDestroy()
    {
        // Clean up event subscriptions
        if (_dewSpawnPosition != null)
        {
            _dewSpawnPosition.OnValueChanged -= OnDewSpawnPositionChanged;
        }
        if (_solSpawnPosition != null)
        {
            _solSpawnPosition.OnValueChanged -= OnSolSpawnPositionChanged;
        }
    }
}