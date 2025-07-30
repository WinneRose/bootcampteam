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
        if (applyOnStart)
        {
            ApplyPropSettings();
        }
    }
    
    void Start()
    {
        // Fallback if not using NetworkBehaviour
        if (!IsSpawned && applyOnStart)
        {
            // Delay to ensure NetworkManager is ready
            Invoke(nameof(ApplyPropSettings), 0.1f);
        }
    }
    
    /// <summary>
    /// Apply the prop and component settings based on host/client status
    /// </summary>
    public void ApplyPropSettings()
    {
        bool isHost = IsHost();
        
        // Handle GameObjects for Host
        foreach (GameObject prop in hostOnlyProps)
        {
            if (prop != null)
            {
                prop.SetActive(isHost);
            }
        }
        
        // Handle GameObjects for Client
        foreach (GameObject prop in clientOnlyProps)
        {
            if (prop != null)
            {
                prop.SetActive(!isHost);
            }
        }
        
        // Handle Components for Host
        foreach (MonoBehaviour component in hostOnlyComponents)
        {
            if (component != null)
            {
                component.enabled = isHost;
            }
        }
        
        // Handle Components for Client
        foreach (MonoBehaviour component in clientOnlyComponents)
        {
            if (component != null)
            {
                component.enabled = !isHost;
            }
        }
        
        if (logStatus)
        {
            Debug.Log($"[NetcodePropManager] Props configured for: {(isHost ? "HOST" : "CLIENT")}");
            Debug.Log($"Host props active: {hostOnlyProps.Count}, Client props active: {clientOnlyProps.Count}");
        }
    }
    
    /// <summary>
    /// Check if current player is host
    /// </summary>
    private bool IsHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetcodePropManager] NetworkManager not found!");
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
            return false;
        }
        
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
            prop.SetActive(IsClient());
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
        if (IsHost())
        {
            ApplyPropSettings();
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        // Refresh settings when clients disconnect
        if (IsHost())
        {
            ApplyPropSettings();
        }
    }
}