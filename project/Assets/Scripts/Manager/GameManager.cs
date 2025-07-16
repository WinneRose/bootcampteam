using UnityEngine;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    [Header("Player Prefabs")]
    public GameObject hostPlayerPrefab;
    public GameObject clientPlayerPrefab;
    
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    
    [Header("Debug")]
    public bool showDebugMessages = true;

    void Start()
    {
        if (showDebugMessages)
            Debug.Log("GameManager initialized");
    }

    public void SpawnAsHost()
    {
        if (showDebugMessages)
            Debug.Log("SpawnAsHost called");
            
        if (hostPlayerPrefab == null)
        {
            Debug.LogError("Host player prefab is not assigned!");
            return;
        }

        // Host should always spawn directly since it's the server
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnPlayerOnServer(hostPlayerPrefab, NetworkManager.Singleton.LocalClientId, "HOST");
        }
        else
        {
            Debug.LogError("SpawnAsHost called but not running as server!");
        }
    }

    public void SpawnAsClient()
    {
        if (showDebugMessages)
            Debug.Log("SpawnAsClient called");
            
        if (clientPlayerPrefab == null)
        {
            Debug.LogError("Client player prefab is not assigned!");
            return;
        }

        // Client needs to request spawn from server
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            if (showDebugMessages)
                Debug.Log("Requesting spawn from server as client");
            RequestPlayerSpawnServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        else if (NetworkManager.Singleton.IsServer)
        {
            // This case shouldn't happen, but just in case
            SpawnPlayerOnServer(clientPlayerPrefab, NetworkManager.Singleton.LocalClientId, "CLIENT");
        }
        else
        {
            Debug.LogError("SpawnAsClient called but not connected to network!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ulong clientId)
    {
        if (showDebugMessages)
            Debug.Log($"Received spawn request from client {clientId}");
            
        // Determine if this client is the host or a regular client
        bool isHost = clientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost;
        GameObject prefabToSpawn = isHost ? hostPlayerPrefab : clientPlayerPrefab;
        string playerType = isHost ? "HOST" : "CLIENT";
        
        SpawnPlayerOnServer(prefabToSpawn, clientId, playerType);
    }

    private void SpawnPlayerPrefab(GameObject prefab, string playerType)
    {
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("Cannot spawn player - not connected to network!");
            return;
        }

        // Check if we already have a player spawned
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            Debug.LogWarning($"Player already spawned for this client!");
            return;
        }

        // Only the server can spawn player objects
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnPlayerOnServer(prefab, NetworkManager.Singleton.LocalClientId, playerType);
        }
        else
        {
            Debug.LogError("Only the server can spawn players! This should be called on the host.");
        }
    }

    private void SpawnPlayerOnServer(GameObject prefab, ulong clientId, string playerType)
    {
        if (showDebugMessages)
            Debug.Log($"SpawnPlayerOnServer called - Spawning {playerType} player for client {clientId}");

        // Validate we're actually the server
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("SpawnPlayerOnServer called but not running as server!");
            return;
        }

        // Check if player already exists for this client
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.LogWarning($"Player already exists for client {clientId}");
                return;
            }
        }

        // Validate prefab
        if (prefab == null)
        {
            Debug.LogError($"Cannot spawn {playerType} - prefab is null!");
            return;
        }

        // Get spawn position
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        // Add random offset to avoid overlapping
        spawnPosition += new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));

        if (showDebugMessages)
            Debug.Log($"Instantiating {playerType} prefab at position {spawnPosition}");

        // Instantiate the player
        GameObject playerInstance = Instantiate(prefab, spawnPosition, spawnRotation);
        
        if (playerInstance == null)
        {
            Debug.LogError($"Failed to instantiate {playerType} prefab!");
            return;
        }

        // Get NetworkObject component
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        
        if (networkObject == null)
        {
            Debug.LogError($"{playerType} player prefab does not have a NetworkObject component!");
            Destroy(playerInstance);
            return;
        }

        if (showDebugMessages)
            Debug.Log($"Spawning NetworkObject for {playerType} player (clientId: {clientId})");

        // Spawn as player object
        networkObject.SpawnAsPlayerObject(clientId);
        
        if (showDebugMessages)
            Debug.Log($"Successfully spawned {playerType} player for client {clientId}");
    }

    // Helper method to check network state
    public bool CanSpawnPlayer()
    {
        return NetworkManager.Singleton != null && 
               NetworkManager.Singleton.IsConnectedClient &&
               NetworkManager.Singleton.LocalClient?.PlayerObject == null;
    }

    // Method to get appropriate prefab based on role
    public GameObject GetPrefabForRole(bool isHost)
    {
        return isHost ? hostPlayerPrefab : clientPlayerPrefab;
    }

    // Method to spawn player for any client (called by server)
    public void SpawnPlayerForClient(ulong clientId, bool isHost)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("SpawnPlayerForClient can only be called on the server!");
            return;
        }

        GameObject prefabToSpawn = isHost ? hostPlayerPrefab : clientPlayerPrefab;
        string playerType = isHost ? "HOST" : "CLIENT";
        
        SpawnPlayerOnServer(prefabToSpawn, clientId, playerType);
    }
}