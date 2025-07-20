using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Unity.Netcode.Transports.UTP;

public class ButtonActions : MonoBehaviour
{
    private NetworkManager networkManager;
    private UIManager uiManager;
    
    [Header("Network Settings")]
    [Tooltip("IP address for client to connect to")]
    public string serverAddress = "127.0.0.1";
    [Tooltip("Port for connection")]
    public ushort serverPort = 7777;
    
    // Remove gameSceneName - character selection will handle scene loading
    
    void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
        }
        
        uiManager = FindObjectOfType<UIManager>();
        
        SetupTransport();
        
        if (uiManager == null)
        {
            Debug.LogError("UIManager not found in scene!");
        }
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found! Make sure there's a NetworkManager in the scene.");
        }
        
        Debug.Log("ButtonActions initialized");
    }
    
    private void SetupTransport()
    {
        if (networkManager == null) return;
        
        if (networkManager.NetworkConfig.NetworkTransport == null)
        {
            Debug.LogError("No transport assigned to NetworkManager! Adding Unity Transport...");
            
            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
                Debug.Log("Added Unity Transport component to NetworkManager");
            }
            
            networkManager.NetworkConfig.NetworkTransport = transport;
            Debug.Log("Assigned Unity Transport to NetworkManager");
        }
        
        UnityTransport unityTransport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (unityTransport != null)
        {
            unityTransport.ConnectionData.Address = serverAddress;
            unityTransport.ConnectionData.Port = serverPort;
            unityTransport.ConnectionData.ServerListenAddress = "0.0.0.0";
            
            Debug.Log($"Transport configured: {serverAddress}:{serverPort}");
        }
    }

    public void StartHost()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null! Cannot start host.");
            return;
        }
        
        Debug.Log("Starting Host...");
        
        SetupTransport();
        
        try
        {
            if (networkManager.StartHost())
            {
                Debug.Log("Host started successfully - staying in lobby for character selection");
                // Don't load game scene here - let character selection handle it
            }
            else
            {
                Debug.LogError("Failed to start host");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception when starting host: {e.Message}");
        }
    }

    public void StartClient()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null! Cannot start client.");
            return;
        }
        
        Debug.Log("Starting Client...");
        
        SetupTransport();
        
        try
        {
            if (networkManager.StartClient())
            {
                Debug.Log("Client connection started");
                
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                
                StartCoroutine(ClientConnectionTimeout());
            }
            else
            {
                Debug.LogError("Failed to start client");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception when starting client: {e.Message}");
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
            networkManager.Shutdown();
            
            if (uiManager != null)
            {
                uiManager.OnDisconnected();
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected. Local client ID: {networkManager.LocalClientId}");
        
        if (clientId == networkManager.LocalClientId && !networkManager.IsHost)
        {
            Debug.Log($"Local client {clientId} connected successfully - ready for character selection");
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        
        if (clientId == networkManager.LocalClientId)
        {
            Debug.Log("Local client disconnected");
            
            if (uiManager != null)
            {
                uiManager.OnDisconnected();
            }
        }
    }
    
    public void Disconnect()
    {
        if (networkManager != null && networkManager.IsConnectedClient)
        {
            Debug.Log("Disconnecting from network...");
            networkManager.Shutdown();
        }
        
        if (uiManager != null)
        {
            uiManager.OnDisconnected();
        }
    }
    
    [ContextMenu("Debug Network Status")]
    public void DebugNetworkStatus()
    {
        if (networkManager == null)
        {
            Debug.Log("NetworkManager: NULL");
            return;
        }
        
        Debug.Log("=== Network Status ===");
        Debug.Log($"Is Host: {networkManager.IsHost}");
        Debug.Log($"Is Client: {networkManager.IsClient}");
        Debug.Log($"Is Server: {networkManager.IsServer}");
        Debug.Log($"Is Connected Client: {networkManager.IsConnectedClient}");
        Debug.Log($"Local Client ID: {networkManager.LocalClientId}");
        Debug.Log($"Connected Clients Count: {networkManager.ConnectedClients.Count}");
        
        if (networkManager.NetworkConfig.NetworkTransport != null)
        {
            Debug.Log($"Transport: {networkManager.NetworkConfig.NetworkTransport.GetType().Name}");
            
            if (networkManager.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                Debug.Log($"Transport Address: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}");
            }
        }
        else
        {
            Debug.Log("Transport: NULL - This is the problem!");
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}