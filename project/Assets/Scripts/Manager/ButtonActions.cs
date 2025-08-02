using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine.UI;
using TMPro;

public class ButtonActions : MonoBehaviour
{
    private NetworkManager networkManager;
    private UIManager uiManager; // Optional - can be null
    
    [Header("Network Settings")]
    [Tooltip("IP address for client to connect to (will auto-detect local IP for host)")]
    public string serverAddress = "192.168.1.100"; // Default - will be auto-detected
    [Tooltip("Port for connection")]
    public ushort serverPort = 7777;
    [Tooltip("Auto-detect local IP address when hosting")]
    public bool autoDetectHostIP = true;
    
    [Header("UI References (Optional)")]
    [Tooltip("Input field for players to enter server IP address")]
    public TMP_InputField serverIPInput;
    [Tooltip("Text field to display current server IP")]
    public TextMeshProUGUI serverIPDisplay;
    [Tooltip("Button to copy IP to clipboard")]
    public Button copyIPButton;
    
    [Header("Debug Info")]
    [SerializeField, Tooltip("Current detected local IP")]
    private string detectedLocalIP = "";
    
    void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
        }
        
        // UIManager is optional - don't log error if not found
        uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            Debug.Log("UIManager found and connected");
        }
        else
        {
            Debug.Log("No UIManager found - running without UI callbacks");
        }
        
        // Auto-detect local IP on start
        if (autoDetectHostIP)
        {
            DetectLocalIP();
        }
        
        SetupTransport();
        SetupUI();
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found! Make sure there's a NetworkManager in the scene.");
        }
        
        Debug.Log("ButtonActions initialized");
        ShowNetworkInfo(); // Show available IPs for easy configuration
    }
    
    private void SetupUI()
    {
        // Setup IP input field
        if (serverIPInput != null)
        {
            serverIPInput.text = serverAddress;
            serverIPInput.onEndEdit.AddListener(OnIPInputChanged);
            Debug.Log("Server IP input field connected");
        }
        
        // Setup IP display
        if (serverIPDisplay != null)
        {
            UpdateIPDisplay();
            Debug.Log("Server IP display connected");
        }
        
        // Setup copy button
        if (copyIPButton != null)
        {
            copyIPButton.onClick.AddListener(CopyIPToClipboard);
            Debug.Log("Copy IP button connected");
        }
    }
    
    private void OnIPInputChanged(string newIP)
    {
        if (!string.IsNullOrEmpty(newIP))
        {
            SetServerAddress(newIP);
            Debug.Log($"Server IP updated from input field: {newIP}");
        }
    }
    
    private void UpdateIPDisplay()
    {
        if (serverIPDisplay != null)
        {
            serverIPDisplay.text = $"Local IP: {serverAddress}:{serverPort}";
        }
    }
    
    private void CopyIPToClipboard()
    {
        string ipToCopy = $"{serverAddress}:{serverPort}";
        GUIUtility.systemCopyBuffer = ipToCopy;
        Debug.Log($"Copied to clipboard: {ipToCopy}");
    }
    
    private void DetectLocalIP()
    {
        try
        {
            string localIP = GetLocalIPAddress();
            if (!string.IsNullOrEmpty(localIP))
            {
                detectedLocalIP = localIP;
                serverAddress = localIP;
                Debug.Log($"Auto-detected local IP: {localIP}");
                UpdateIPDisplay();
                
                // Update input field if it exists
                if (serverIPInput != null)
                {
                    serverIPInput.text = localIP;
                }
            }
            else
            {
                Debug.LogWarning("Could not auto-detect local IP. Using default: " + serverAddress);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error detecting local IP: {e.Message}");
        }
    }
    
    private string GetLocalIPAddress()
    {
        // Try to get the best local IP address for LAN gaming
        var host = Dns.GetHostEntry(Dns.GetHostName());
        
        // Prefer common local network ranges
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                string ipString = ip.ToString();
                
                // Prefer common local network ranges
                if (ipString.StartsWith("192.168.") || 
                    ipString.StartsWith("10.") || 
                    ipString.StartsWith("172."))
                {
                    return ipString;
                }
            }
        }
        
        // Fallback: return any IPv4 address that's not localhost
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().Equals("127.0.0.1"))
            {
                return ip.ToString();
            }
        }
        
        return null;
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
            unityTransport.ConnectionData.ServerListenAddress = "0.0.0.0"; // Listen on all interfaces
            
            Debug.Log($"Transport configured - Client will connect to: {serverAddress}:{serverPort}");
            Debug.Log($"Server will listen on: 0.0.0.0:{serverPort} (all interfaces)");
        }
    }

    public void StartHost()
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null! Cannot start host.");
            return;
        }
        
        // Update IP detection before hosting
        if (autoDetectHostIP)
        {
            DetectLocalIP();
        }
        
        Debug.Log($"Starting Host on IP: {serverAddress}:{serverPort}");
        Debug.Log("Other players should connect using this IP address!");
        
        if (uiManager == null)
        {
            gameObject.SetActive(false);
        }
        SetupTransport();
        
        try
        {
            if (networkManager.StartHost())
            {
                Debug.Log("Host started successfully - staying in lobby for character selection");
                Debug.Log($"*** HOST INFO: Tell other players to connect to {serverAddress}:{serverPort} ***");
                UpdateIPDisplay();
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
        
        // Get IP from input field if it exists and has content
        if (serverIPInput != null && !string.IsNullOrEmpty(serverIPInput.text))
        {
            SetServerAddress(serverIPInput.text);
        }
        
        Debug.Log($"Starting Client - attempting to connect to: {serverAddress}:{serverPort}");
        if (uiManager == null)
        {
            gameObject.SetActive(false);
        }
        
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
            Debug.LogError($"Client connection timed out! Could not connect to {serverAddress}:{serverPort}");
            Debug.LogError("Make sure the host is running and the IP address is correct.");
            networkManager.Shutdown();
            
            // Only call UI callback if UIManager exists
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
            Debug.Log($"Local client {clientId} connected successfully to {serverAddress}:{serverPort} - ready for character selection");
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        
        if (clientId == networkManager.LocalClientId)
        {
            Debug.Log("Local client disconnected");
            
            // Only call UI callback if UIManager exists
            if (uiManager != null)
            {
                uiManager.OnDisconnected();
            }
        }
    }
    
    public void Disconnect()
    {
        if (networkManager != null && (networkManager.IsConnectedClient || networkManager.IsHost))
        {
            Debug.Log("Disconnecting from network...");
            networkManager.Shutdown();
        }
        
        // Only call UI callback if UIManager exists
        if (uiManager != null)
        {
            uiManager.OnDisconnected();
        }
    }
    
    // Helper method to manually set server address (useful for UI)
    public void SetServerAddress(string address)
    {
        serverAddress = address.Trim();
        Debug.Log($"Server address set to: {serverAddress}");
        SetupTransport(); // Update transport with new address
        UpdateIPDisplay();
    }
    
    // Public method that can be called from UI buttons
    public void RefreshAndShowLocalIP()
    {
        DetectLocalIP();
        ShowNetworkInfo();
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
        Debug.Log($"UIManager: {(uiManager != null ? "Present" : "Not Found")}");
        Debug.Log($"Configured Server Address: {serverAddress}:{serverPort}");
        Debug.Log($"Auto-detected Local IP: {detectedLocalIP}");
        
        if (networkManager.NetworkConfig.NetworkTransport != null)
        {
            Debug.Log($"Transport: {networkManager.NetworkConfig.NetworkTransport.GetType().Name}");
            
            if (networkManager.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                Debug.Log($"Transport Client Address: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}");
                Debug.Log($"Transport Server Listen Address: {transport.ConnectionData.ServerListenAddress}:{transport.ConnectionData.Port}");
            }
        }
        else
        {
            Debug.Log("Transport: NULL - This is the problem!");
        }
    }
    
    [ContextMenu("Show Network Info")]
    public void ShowNetworkInfo()
    {
        Debug.Log("=== Available Network Interfaces ===");
        
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            Debug.Log($"Computer Name: {Dns.GetHostName()}");
            
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string ipString = ip.ToString();
                    if (!ipString.Equals("127.0.0.1"))
                    {
                        Debug.Log($"Available IP for LAN gaming: {ipString}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error getting network info: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        // Clean up UI listeners
        if (serverIPInput != null)
        {
            serverIPInput.onEndEdit.RemoveListener(OnIPInputChanged);
        }
        
        if (copyIPButton != null)
        {
            copyIPButton.onClick.RemoveListener(CopyIPToClipboard);
        }
    }
}