using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class ButtonActions : MonoBehaviour
{
    private NetworkManager NetworkManager;
    private GameManager gameManager;

    void Start()
    {
        NetworkManager = GetComponentInParent<NetworkManager>();
        gameManager = FindObjectOfType<GameManager>();
        
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in scene!");
        }
    }

    public void StartHost()
    {
        if (NetworkManager.StartHost())
        {
            Debug.Log("Host started successfully");
            // Use coroutine for more reliable timing
            StartCoroutine(WaitAndSpawnHost());
        }
        else
        {
            Debug.LogError("Failed to start host");
        }
    }

    public void StartClient()
    {
        if (NetworkManager.StartClient())
        {
            Debug.Log("Client connection started");
            // Subscribe to connection events for clients
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogError("Failed to start client");
        }
    }

    private IEnumerator WaitAndSpawnHost()
    {
        // Wait for network to be fully ready
        yield return new WaitUntil(() => NetworkManager.IsHost && NetworkManager.IsConnectedClient);
        
        // Additional small delay to ensure everything is initialized
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("Network is ready, spawning host player");
        
        if (gameManager != null)
        {
            gameManager.SpawnAsHost();
        }
        else
        {
            Debug.LogError("GameManager is null when trying to spawn host!");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected. Local client ID: {NetworkManager.LocalClientId}");
        
        // Only handle our own client connection (and not if we're the host)
        if (clientId == NetworkManager.LocalClientId && !NetworkManager.IsHost)
        {
            Debug.Log($"Local client {clientId} connected - spawning client player");
            
            // Small delay to ensure network is fully ready
            StartCoroutine(WaitAndSpawnClient());
        }
    }

    private IEnumerator WaitAndSpawnClient()
    {
        // Wait a bit to ensure network is stable
        yield return new WaitForSeconds(0.1f);
        
        if (gameManager != null)
        {
            Debug.Log("Spawning client player");
            gameManager.SpawnAsClient();
        }
        else
        {
            Debug.LogError("GameManager is null when trying to spawn client!");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}