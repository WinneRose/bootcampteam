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
        Debug.Log($"Set Dew selection: Player {playerId}");
    }
    
    public static void SetSolSelection(ulong playerId, GameObject prefab)
    {
        SelectionData.isSolSelected = true;
        SelectionData.solPlayerID = playerId;
        SelectionData.solPrefab = prefab;
        CreatePersistentObject();
        Debug.Log($"Set Sol selection: Player {playerId}");
    }
    
    private static void CreatePersistentObject()
    {
        if (_persistentObject == null)
        {
            _persistentObject = new GameObject("CharacterSelectionPersistence");
            Object.DontDestroyOnLoad(_persistentObject);
            Debug.Log("Created persistent object for character selection data");
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
        Debug.Log("Cleared all character selections");
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
    
    // Network variables to track spawned players
    private NetworkVariable<bool> _dewSpawned = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> _solSpawned = new NetworkVariable<bool>(false);
    
    // References to spawned player objects
    private GameObject _spawnedDewPlayer;
    private GameObject _spawnedSolPlayer;
    
    public override void OnNetworkSpawn()
    {
        Debug.Log("🎮 GameManager: OnNetworkSpawn called");
        
        if (IsServer && autoSpawnOnSceneLoad)
        {
            Debug.Log($"🎮 GameManager: Will spawn players in {spawnDelay} seconds");
            // Delay to ensure everything is ready
            StartCoroutine(DelayedSpawn());
        }
    }
    
    private IEnumerator DelayedSpawn()
    {
        yield return new WaitForSeconds(spawnDelay);
        
        Debug.Log("🎮 GameManager: Starting delayed spawn process");
        SpawnPlayersFromSelection();
    }
    
    private void SpawnPlayersFromSelection()
    {
        Debug.Log("🎮 GameManager: === SPAWNING PLAYERS FROM SELECTION ===");
        
        // Get character selection data
        CharacterSelectionData selectionData = CharacterSelectionManager.SelectionData;
        
        if (selectionData == null)
        {
            Debug.LogWarning("🎮 No character selection data found! Using fallback spawning...");
            SpawnPlayersFallback();
            return;
        }
        
        Debug.Log($"🎮 Selection data: {selectionData}");
        
        if (!CharacterSelectionManager.HasValidSelections())
        {
            Debug.LogWarning("🎮 Invalid selection data! Using fallback spawning...");
            SpawnPlayersFallback();
            return;
        }
        
        // Spawn Dew character
        if (selectionData.isDewSelected && selectionData.dewPlayerID != 999999)
        {
            if (IsClientConnected(selectionData.dewPlayerID))
            {
                SpawnCharacterForPlayer(selectionData.dewPlayerID, "Dew", selectionData.dewPrefab ?? defaultDewPrefab);
            }
            else
            {
                Debug.LogWarning($"🎮 Dew player {selectionData.dewPlayerID} is not connected!");
            }
        }
        
        // Spawn Sol character
        if (selectionData.isSolSelected && selectionData.solPlayerID != 999999)
        {
            if (IsClientConnected(selectionData.solPlayerID))
            {
                SpawnCharacterForPlayer(selectionData.solPlayerID, "Sol", selectionData.solPrefab ?? defaultSolPrefab);
            }
            else
            {
                Debug.LogWarning($"🎮 Sol player {selectionData.solPlayerID} is not connected!");
            }
        }
    }
    
    private bool IsClientConnected(ulong clientId)
    {
        bool isConnected = NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId);
        Debug.Log($"🎮 Client {clientId} connected: {isConnected}");
        return isConnected;
    }
    
    private void SpawnPlayersFallback()
    {
        Debug.Log("🎮 GameManager: === USING FALLBACK SPAWNING ===");
        
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        Debug.Log($"🎮 Connected clients count: {connectedClients.Count}");
        
        int clientIndex = 0;
        foreach (var client in connectedClients)
        {
            ulong clientId = client.Key;
            
            if (clientIndex == 0)
            {
                // First client (usually host) becomes Dew
                SpawnCharacterForPlayer(clientId, "Dew", defaultDewPrefab);
            }
            else if (clientIndex == 1)
            {
                // Second client becomes Sol
                SpawnCharacterForPlayer(clientId, "Sol", defaultSolPrefab);
            }
            
            clientIndex++;
            
            // Only spawn for first 2 clients
            if (clientIndex >= 2) break;
        }
    }
    
    private void SpawnCharacterForPlayer(ulong clientId, string characterType, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"🎮 No prefab assigned for {characterType} character!");
            return;
        }
        
        // Check if already spawned
        if (characterType == "Dew" && _dewSpawned.Value)
        {
            Debug.LogWarning($"🎮 Dew character already spawned!");
            return;
        }
        
        if (characterType == "Sol" && _solSpawned.Value)
        {
            Debug.LogWarning($"🎮 Sol character already spawned!");
            return;
        }
        
        // Determine spawn point with detailed debugging
        Transform spawnPoint = null;
        if (characterType == "Dew")
        {
            spawnPoint = dewSpawnPoint;
            Debug.Log($"🎮 Dew spawn point: {(dewSpawnPoint != null ? dewSpawnPoint.name + " at " + dewSpawnPoint.position : "NULL")}");
        }
        else if (characterType == "Sol")
        {
            spawnPoint = solSpawnPoint;
            Debug.Log($"🎮 Sol spawn point: {(solSpawnPoint != null ? solSpawnPoint.name + " at " + solSpawnPoint.position : "NULL")}");
        }
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        if (spawnPoint == null)
        {
            Debug.LogError($"🎮 ❌ NO SPAWN POINT ASSIGNED FOR {characterType}!");
            Debug.LogError($"🎮 Please assign {characterType}SpawnPoint in GameManager inspector!");
            
            // Use GameManager position as emergency fallback
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            Debug.LogWarning($"🎮 Using GameManager fallback position: {spawnPosition}");
        }
        else
        {
            spawnPosition = spawnPoint.position;
            spawnRotation = spawnPoint.rotation;
            Debug.Log($"🎮 ✅ Using {characterType} spawn point: {spawnPosition}");
        }
        
        Debug.Log($"🎮 === SPAWNING {characterType} CHARACTER ===");
        Debug.Log($"🎮 Client ID: {clientId}");
        Debug.Log($"🎮 Prefab: {prefab.name}");
        Debug.Log($"🎮 Spawn Point Object: {(spawnPoint != null ? spawnPoint.name : "NULL")}");
        Debug.Log($"🎮 Final Position: {spawnPosition}");
        Debug.Log($"🎮 Final Rotation: {spawnRotation}");
        
        try
        {
            GameObject playerInstance = Instantiate(prefab, spawnPosition, spawnRotation);
            
            // Verify the spawned position
            Debug.Log($"🎮 Player instantiated at: {playerInstance.transform.position}");
            
            // Get or add NetworkObject component
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"🎮 Character prefab {prefab.name} does not have NetworkObject component!");
                Destroy(playerInstance);
                return;
            }
            
            // Spawn with ownership to the specific client
            networkObject.SpawnWithOwnership(clientId);
            
            // Verify the position after network spawn
            Debug.Log($"🎮 Player position after network spawn: {playerInstance.transform.position}");
            
            // Store references
            if (characterType == "Dew")
            {
                _spawnedDewPlayer = playerInstance;
                _dewSpawned.Value = true;
                Debug.Log($"✅ Dew character spawned for client {clientId} at {playerInstance.transform.position}");
            }
            else if (characterType == "Sol")
            {
                _spawnedSolPlayer = playerInstance;
                _solSpawned.Value = true;
                Debug.Log($"✅ Sol character spawned for client {clientId} at {playerInstance.transform.position}");
            }
            
            // Notify clients about the spawn
            NotifyPlayerSpawnedClientRpc(clientId, characterType, spawnPosition);
            
            // Check for PlayerNetworkManager
            var playerNetworkManager = playerInstance.GetComponent<PlayerNetworkManager>();
            if (playerNetworkManager != null)
            {
                Debug.Log($"🎮 PlayerNetworkManager found on {characterType} character");
            }
            else
            {
                Debug.LogWarning($"🎮 No PlayerNetworkManager found on {characterType} character");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"🎮 Failed to spawn {characterType} character: {e.Message}");
        }
    }
    
    [ClientRpc]
    private void NotifyPlayerSpawnedClientRpc(ulong clientId, string characterType, Vector3 spawnPosition)
    {
        Debug.Log($"🎮 Player spawned notification: Client {clientId} as {characterType} at {spawnPosition}");
    }
    
    // Public methods for manual spawning (useful for testing or special cases)
    public void SpawnAsHost()
    {
        if (!IsServer)
        {
            Debug.LogWarning("🎮 SpawnAsHost called on non-server!");
            return;
        }
        
        ulong hostClientId = NetworkManager.Singleton.LocalClientId;
        SpawnCharacterForPlayer(hostClientId, "Dew", defaultDewPrefab);
    }
    
    public void SpawnAsClient()
    {
        if (!IsServer)
        {
            Debug.LogWarning("🎮 SpawnAsClient called on non-server!");
            return;
        }
        
        // Find the first non-host client
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
    
    // Find player by client ID
    public GameObject GetPlayerByClientId(ulong clientId)
    {
        // Check if it's the Dew player
        if (_spawnedDewPlayer != null)
        {
            NetworkObject dewNetObj = _spawnedDewPlayer.GetComponent<NetworkObject>();
            if (dewNetObj != null && dewNetObj.OwnerClientId == clientId)
            {
                return _spawnedDewPlayer;
            }
        }
        
        // Check if it's the Sol player
        if (_spawnedSolPlayer != null)
        {
            NetworkObject solNetObj = _spawnedSolPlayer.GetComponent<NetworkObject>();
            if (solNetObj != null && solNetObj.OwnerClientId == clientId)
            {
                return _spawnedSolPlayer;
            }
        }
        
        return null;
    }
    
    // Debug methods
    [ContextMenu("Debug Spawn Points")]
    public void DebugSpawnPoints()
    {
        Debug.Log("🎮 === SPAWN POINTS DEBUG ===");
        
        if (dewSpawnPoint != null)
        {
            Debug.Log($"🎮 Dew Spawn Point: {dewSpawnPoint.name}");
            Debug.Log($"🎮   Position: {dewSpawnPoint.position}");
            Debug.Log($"🎮   Rotation: {dewSpawnPoint.rotation}");
            Debug.Log($"🎮   Active: {dewSpawnPoint.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("🎮 ❌ DEW SPAWN POINT IS NULL!");
        }
        
        if (solSpawnPoint != null)
        {
            Debug.Log($"🎮 Sol Spawn Point: {solSpawnPoint.name}");
            Debug.Log($"🎮   Position: {solSpawnPoint.position}");
            Debug.Log($"🎮   Rotation: {solSpawnPoint.rotation}");
            Debug.Log($"🎮   Active: {solSpawnPoint.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("🎮 ❌ SOL SPAWN POINT IS NULL!");
        }
        
        Debug.Log($"🎮 GameManager Position: {transform.position}");
    }
    
    [ContextMenu("Debug Spawn State")]
    public void DebugSpawnState()
    {
        Debug.Log("🎮 === Game Manager Spawn State ===");
        Debug.Log($"🎮 Is Server: {IsServer}");
        Debug.Log($"🎮 Dew Spawned: {_dewSpawned.Value}");
        Debug.Log($"🎮 Sol Spawned: {_solSpawned.Value}");
        Debug.Log($"🎮 Dew Player Object: {(_spawnedDewPlayer != null ? _spawnedDewPlayer.name : "null")}");
        Debug.Log($"🎮 Sol Player Object: {(_spawnedSolPlayer != null ? _spawnedSolPlayer.name : "null")}");
        
        // Debug selection data
        CharacterSelectionData selectionData = CharacterSelectionManager.SelectionData;
        if (selectionData != null)
        {
            Debug.Log($"🎮 Selection Data: {selectionData}");
            Debug.Log($"🎮 Has Valid Selections: {CharacterSelectionManager.HasValidSelections()}");
        }
        else
        {
            Debug.Log("🎮 No selection data available");
        }
        
        // Debug connected clients
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"🎮 Connected Clients: {NetworkManager.Singleton.ConnectedClients.Count}");
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"🎮   Client {client.Key}");
            }
        }
    }
    
    [ContextMenu("Force Spawn All Players")]
    public void ForceSpawnAllPlayers()
    {
        if (!IsServer)
        {
            Debug.LogWarning("🎮 Can only force spawn on server!");
            return;
        }
        
        SpawnPlayersFromSelection();
    }
    
    [ContextMenu("Force Fallback Spawn")]
    public void ForceFallbackSpawn()
    {
        if (!IsServer)
        {
            Debug.LogWarning("🎮 Can only force spawn on server!");
            return;
        }
        
        SpawnPlayersFallback();
    }
    
    [ContextMenu("Clear All Spawned Players")]
    public void ClearAllSpawnedPlayers()
    {
        if (!IsServer)
        {
            Debug.LogWarning("🎮 Can only clear spawned players on server!");
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
        
        Debug.Log("🎮 All spawned players cleared");
    }
}