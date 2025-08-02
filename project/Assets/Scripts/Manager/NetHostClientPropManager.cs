using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetHostClientPropManager : NetworkBehaviour
{
    [Header("Host Only Props")]
    [Tooltip("These objects will only be active when player is host")]
    [SerializeField] private List<GameObject> hostOnlyProps = new List<GameObject>();
    
    [Header("Client Only Props")]
    [Tooltip("These objects will only be active when player is client")]
    [SerializeField] private List<GameObject> clientOnlyProps = new List<GameObject>();
    
    [Header("Host Only Components")]
    [Tooltip("These components will only be enabled when player is host")]
    [SerializeField] private List<MonoBehaviour> hostOnlyComponents = new List<MonoBehaviour>();
    
    [Header("Client Only Components")]
    [Tooltip("These components will only be enabled when player is client")]
    [SerializeField] private List<MonoBehaviour> clientOnlyComponents = new List<MonoBehaviour>();
    
    [Header("Settings")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool logStatus = true;
    
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[NetHostClientPropManager] OnNetworkSpawn - IsHost: {IsHost()}, IsClient: {IsClient()}");
        
        if (applyOnStart)
        {
            // Small delay to ensure network state is fully established
            Invoke(nameof(ApplyPropSettings), 0.1f);
        }
    }
    
    void Start()
    {
        // Fallback if not using NetworkBehaviour
        if (!IsSpawned && applyOnStart)
        {
            Debug.Log("[NetHostClientPropManager] Not spawned, applying fallback settings");
            // Delay to ensure NetworkManager is ready
            Invoke(nameof(ApplyPropSettings), 0.2f);
        }
    }
    
    /// <summary>
    /// Apply the prop and component settings based on host/client status
    /// </summary>
    public void ApplyPropSettings()
    {
        bool isHost = IsHost();
        bool isClient = IsClient();
        
        if (logStatus)
        {
            Debug.Log($"[NetHostClientPropManager] Applying settings - IsHost: {isHost}, IsClient: {isClient}");
        }
        
        // Handle GameObjects for Host
        foreach (GameObject prop in hostOnlyProps)
        {
            if (prop != null)
            {
                bool shouldBeActive = isHost;
                prop.SetActive(shouldBeActive);
                
                if (logStatus)
                {
                    Debug.Log($"[NetHostClientPropManager] Host prop '{prop.name}' set to: {shouldBeActive}");
                }
            }
        }
        
        // Handle GameObjects for Client  
        foreach (GameObject prop in clientOnlyProps)
        {
            if (prop != null)
            {
                bool shouldBeActive = isClient; // Changed from !isHost to isClient
                prop.SetActive(shouldBeActive);
                
                if (logStatus)
                {
                    Debug.Log($"[NetHostClientPropManager] Client prop '{prop.name}' set to: {shouldBeActive}");
                }
            }
        }
        
        // Handle Components for Host
        foreach (MonoBehaviour component in hostOnlyComponents)
        {
            if (component != null)
            {
                component.enabled = isHost;
                
                if (logStatus)
                {
                    Debug.Log($"[NetHostClientPropManager] Host component '{component.GetType().Name}' enabled: {isHost}");
                }
            }
        }
        
        // Handle Components for Client
        foreach (MonoBehaviour component in clientOnlyComponents)
        {
            if (component != null)
            {
                component.enabled = isClient; // Changed from !isHost to isClient
                
                if (logStatus)
                {
                    Debug.Log($"[NetHostClientPropManager] Client component '{component.GetType().Name}' enabled: {isClient}");
                }
            }
        }
        
        if (logStatus)
        {
            Debug.Log($"[NetHostClientPropManager] Props configured for: {GetPlayerTypeString()}");
            Debug.Log($"Host props count: {hostOnlyProps.Count}, Client props count: {clientOnlyProps.Count}");
        }
    }
    
    /// <summary>
    /// Check if current player is host
    /// </summary>
    private bool IsHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetHostClientPropManager] NetworkManager not found!");
            return false;
        }
        
        return NetworkManager.Singleton.IsHost;
    }
    
    /// <summary>
    /// Check if current player is client (not host)
    /// </summary>
    private bool IsClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetHostClientPropManager] NetworkManager not found for IsClient check!");
            return false;
        }
        
        // Client is someone who is connected but NOT the host
        return NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost;
    }
    
    /// <summary>
    /// Public method to manually refresh prop settings
    /// </summary>
    public void RefreshPropSettings()
    {
        ApplyPropSettings();
    }
    
    /// <summary>
    /// Add a prop to host-only list at runtime
    /// </summary>
    public void AddHostOnlyProp(GameObject prop)
    {
        if (prop != null && !hostOnlyProps.Contains(prop))
        {
            hostOnlyProps.Add(prop);
            prop.SetActive(IsHost());
        }
    }
    
    /// <summary>
    /// Add a prop to client-only list at runtime
    /// </summary>
    public void AddClientOnlyProp(GameObject prop)
    {
        if (prop != null && !clientOnlyProps.Contains(prop))
        {
            clientOnlyProps.Add(prop);
            prop.SetActive(IsClient()); // Fixed to use IsClient() instead of IsClient()
        }
    }
    
    /// <summary>
    /// Remove a prop from all lists
    /// </summary>
    public void RemoveProp(GameObject prop)
    {
        hostOnlyProps.Remove(prop);
        clientOnlyProps.Remove(prop);
    }
    
    /// <summary>
    /// Get current player type as string
    /// </summary>
    public string GetPlayerTypeString()
    {
        if (NetworkManager.Singleton == null)
            return "OFFLINE";
            
        if (NetworkManager.Singleton.IsHost)
            return "HOST";
        else if (NetworkManager.Singleton.IsClient)
            return "CLIENT";
        else
            return "DISCONNECTED";
    }
    
    /// <summary>
    /// Enable/disable all host props
    /// </summary>
    public void SetHostPropsActive(bool active)
    {
        foreach (GameObject prop in hostOnlyProps)
        {
            if (prop != null)
            {
                prop.SetActive(active);
            }
        }
    }
    
    /// <summary>
    /// Enable/disable all client props
    /// </summary>
    public void SetClientPropsActive(bool active)
    {
        foreach (GameObject prop in clientOnlyProps)
        {
            if (prop != null)
            {
                prop.SetActive(active);
            }
        }
    }
    
    // Event callbacks for network state changes
    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    
    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        // Refresh settings when clients connect
        Debug.Log($"[NetHostClientPropManager] Client {clientId} connected, refreshing settings");
        Invoke(nameof(ApplyPropSettings), 0.1f); // Small delay for network state to settle
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        // Refresh settings when clients disconnect  
        Debug.Log($"[NetHostClientPropManager] Client {clientId} disconnected, refreshing settings");
        Invoke(nameof(ApplyPropSettings), 0.1f); // Small delay for network state to settle
    }
}