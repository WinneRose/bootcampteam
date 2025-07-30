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
    public float spawnDelay = 1f;
    public float clientWaitTime = 2f; // Additional wait for clients
    
    // Network variables to track spawned players
    private NetworkVariable<bool> _dewSpawned = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> _solSpawned = new NetworkVariable<bool>(false);
    
    // References to spawned player objects
    private GameObject _spawnedDewPlayer;
    private GameObject _spawnedSolPlayer;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Wait for all clients to connect before spawning
            if (autoSpawnOnSceneLoad)
            {
                StartCoroutine(WaitForClientsAndSpawn());
            }
        }
    }
    
    private IEnumerator WaitForClientsAndSpawn()
    {
        // Wait for spawn delay
        yield return new WaitForSeconds(spawnDelay);
        
        // Additional wait for clients to fully load
        yield return new WaitForSeconds(clientWaitTime);
        
        // Ensure spawn points are valid before spawning
        ValidateSpawnPoints();
        
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
        
        // Spawn Dew character
        if (selectionData.isDewSelected && selectionData.dewPlayerID != 999999)
        {
            if (IsClientConnected(selectionData.dewPlayerID))
            {
                Debug.Log($"Spawning Dew for player {selectionData.dewPlayerID}");
                SpawnCharacterForPlayer(selectionData.dewPlayerID, "Dew", selectionData.dewPrefab ?? defaultDewPrefab);
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
        
        int clientIndex = 0;
        foreach (var client in connectedClients)
        {
            ulong clientId = client.Key;
            Debug.Log($"Processing client {clientId} (index {clientIndex})");
            
            if (clientIndex == 0)
            {
                SpawnCharacterForPlayer(clientId, "Dew", defaultDewPrefab);
            }
            else if (clientIndex == 1)
            {
                SpawnCharacterForPlayer(clientId, "Sol", defaultSolPrefab);
            }
            
            clientIndex++;
            if (clientIndex >= 2) break;
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
        
        // Determine spawn point with better error handling
        Transform spawnPoint = characterType == "Dew" ? dewSpawnPoint : solSpawnPoint;
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        if (spawnPoint == null)
        {
            Debug.LogError($"NO SPAWN POINT ASSIGNED FOR {characterType}! Using GameManager position as fallback.");
            spawnPosition = transform.position + (characterType == "Dew" ? Vector3.left * 2f : Vector3.right * 2f);
            spawnRotation = transform.rotation;
        }
        else
        {
            spawnPosition = spawnPoint.position;
            spawnRotation = spawnPoint.rotation;
            Debug.Log($"Spawning {characterType} at {spawnPoint.name}: {spawnPosition}");
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
            
            // Store references
            if (characterType == "Dew")
            {
                _spawnedDewPlayer = playerInstance;
                _dewSpawned.Value = true;
                Debug.Log($"Dew spawned successfully for client {clientId} at {spawnPosition}");
            }
            else if (characterType == "Sol")
            {
                _spawnedSolPlayer = playerInstance;
                _solSpawned.Value = true;
                Debug.Log($"Sol spawned successfully for client {clientId} at {spawnPosition}");
            }
            
            // Notify all clients about the spawn
            NotifyPlayerSpawnedClientRpc(clientId, characterType, spawnPosition);
            
            // Force position update to ensure it's correct
            StartCoroutine(ForcePositionUpdate(playerInstance, spawnPosition));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to spawn {characterType} character: {e.Message}");
        }
    }
    
    private IEnumerator ForcePositionUpdate(GameObject playerInstance, Vector3 correctPosition)
    {
        // Wait a frame then force the position
        yield return null;
        
        if (playerInstance != null)
        {
            playerInstance.transform.position = correctPosition;
            
            // If it has a CharacterController, disable and re-enable it
            CharacterController cc = playerInstance.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                yield return null;
                cc.enabled = true;
            }
            
            Debug.Log($"Forced position update to {correctPosition} for {playerInstance.name}");
        }
    }
    
    [ClientRpc]
    private void NotifyPlayerSpawnedClientRpc(ulong clientId, string characterType, Vector3 spawnPosition)
    {
        Debug.Log($"[CLIENT] Player spawned notification: {characterType} for client {clientId} at {spawnPosition}");
        
        // Find the spawned player on the client
        StartCoroutine(ValidateClientSpawnPosition(clientId, characterType, spawnPosition));
    }
    
    private IEnumerator ValidateClientSpawnPosition(ulong clientId, string characterType, Vector3 expectedPosition)
    {
        // Wait a moment for the object to be fully spawned
        yield return new WaitForSeconds(0.5f);
        
        GameObject spawnedPlayer = GetPlayerByClientId(clientId);
        if (spawnedPlayer != null)
        {
            Vector3 actualPosition = spawnedPlayer.transform.position;
            float distance = Vector3.Distance(actualPosition, expectedPosition);
            
            if (distance > 1f) // If position is significantly off
            {
                Debug.LogWarning($"[CLIENT] {characterType} position mismatch! Expected: {expectedPosition}, Actual: {actualPosition}, Distance: {distance}");
                
                // If this is our local player, try to correct the position
                if (NetworkManager.Singleton.LocalClientId == clientId)
                {
                    spawnedPlayer.transform.position = expectedPosition;
                    Debug.Log($"[CLIENT] Corrected local player position to {expectedPosition}");
                }
            }
            else
            {
                Debug.Log($"[CLIENT] {characterType} position validated: {actualPosition}");
            }
        }
        else
        {
            Debug.LogWarning($"[CLIENT] Could not find spawned player for client {clientId}");
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
    
    [ContextMenu("Debug: Show All Spawned NetworkObjects")]
    public void DebugShowAllNetworkObjects()
    {
        Debug.Log("=== ALL NETWORK OBJECTS ===");
        var networkObjects = FindObjectsOfType<NetworkObject>();
        
        foreach (var netObj in networkObjects)
        {
            Debug.Log($"NetworkObject: {netObj.name}");
            Debug.Log($"  Owner: {netObj.OwnerClientId}");
            Debug.Log($"  Position: {netObj.transform.position}");
            Debug.Log($"  IsSpawned: {netObj.IsSpawned}");
            Debug.Log($"  Has CharacterController: {netObj.GetComponent<CharacterController>() != null}");
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
    
    [ContextMenu("Debug: Force Fallback Spawn")]
    public void DebugForceFallbackSpawn()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Can only force spawn on server!");
            return;
        }
        
        Debug.Log("Force fallback spawning...");
        ValidateSpawnPoints();
        SpawnPlayersFallback();
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
    
    [ContextMenu("Debug: Clear Character Selection Data")]
    public void DebugClearCharacterSelectionData()
    {
        CharacterSelectionManager.ClearSelections();
        Debug.Log("Character selection data cleared");
    }
}