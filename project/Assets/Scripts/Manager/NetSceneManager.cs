using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetSceneManager : NetworkBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string targetSceneName = "";
    [Tooltip("Scene to load when trigger is activated")]
    
    [Header("Trigger Settings")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool hostOnly = false;
    [Tooltip("If true, only host can trigger scene change")]
    
    [Header("Loading Settings")]
    [SerializeField] private bool useLoadingDelay = false;
    [SerializeField] private float loadingDelay = 1f;
    [SerializeField] private bool showLoadingMessage = true;
    [SerializeField] private string loadingMessage = "Loading scene...";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private bool isLoading = false;
    
    private void Start()
    {
        // Ensure this GameObject has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[NetcodeSceneManager] No collider found! Adding BoxCollider with trigger enabled.");
            BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("[NetcodeSceneManager] Collider is not set as trigger! Scene change may not work properly.");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Prevent multiple scene loads
        if (isLoading) return;
        
        // Check if we should require player tag
        if (requirePlayerTag && !other.CompareTag(playerTag))
        {
            if (enableDebugLogs)
                Debug.Log($"[NetcodeSceneManager] Object {other.name} does not have required tag: {playerTag}");
            return;
        }
        
        // Check if only host can trigger
        if (hostOnly && !IsHost)
        {
            if (enableDebugLogs)
                Debug.Log("[NetcodeSceneManager] Scene change can only be triggered by host");
            return;
        }
        
        // Validate scene name
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[NetcodeSceneManager] Target scene name is empty!");
            return;
        }
        
        // Check if the triggering object belongs to the local player
        NetworkObject networkObject = other.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsOwner)
        {
            if (enableDebugLogs)
                Debug.Log("[NetcodeSceneManager] Trigger activated by non-owner object, ignoring");
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[NetcodeSceneManager] Scene change triggered by {other.name} to scene: {targetSceneName}");
        
        // Trigger scene change
        TriggerSceneChange();
    }
    
    /// <summary>
    /// Triggers the scene change process
    /// </summary>
    public void TriggerSceneChange()
    {
        if (isLoading) return;
        
        if (IsHost)
        {
            // Host handles the scene change
            StartCoroutine(LoadSceneCoroutine());
        }
        else
        {
            // Client requests scene change from host
            RequestSceneChangeServerRpc(targetSceneName);
        }
    }
    
    /// <summary>
    /// Client requests scene change from host
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestSceneChangeServerRpc(string sceneName)
    {
        if (enableDebugLogs)
            Debug.Log($"[NetcodeSceneManager] Host received scene change request for: {sceneName}");
        
        targetSceneName = sceneName;
        StartCoroutine(LoadSceneCoroutine());
    }
    
    /// <summary>
    /// Coroutine to handle scene loading with optional delay
    /// </summary>
    private IEnumerator LoadSceneCoroutine()
    {
        if (isLoading) yield break;
        
        isLoading = true;
        
        // Show loading message to all clients
        if (showLoadingMessage)
        {
            ShowLoadingMessageClientRpc(loadingMessage);
        }
        
        // Optional loading delay
        if (useLoadingDelay && loadingDelay > 0)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetcodeSceneManager] Waiting {loadingDelay} seconds before loading scene...");
            
            yield return new WaitForSeconds(loadingDelay);
        }
        
        // Load the scene
        LoadScene(targetSceneName);
    }
    
    /// <summary>
    /// Actually loads the scene using NetworkManager
    /// </summary>
    private void LoadScene(string sceneName)
    {
        if (!IsHost)
        {
            Debug.LogError("[NetcodeSceneManager] Only host can load scenes!");
            return;
        }
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetcodeSceneManager] NetworkManager is null!");
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[NetcodeSceneManager] Loading scene: {sceneName}");
        
        // Use NetworkManager's scene management
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
    
    /// <summary>
    /// Shows loading message to all clients
    /// </summary>
    [ClientRpc]
    private void ShowLoadingMessageClientRpc(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[NetcodeSceneManager] {message}");
        
        // You can extend this to show UI loading screen
        // For example: LoadingScreen.Instance.Show(message);
    }
    
    /// <summary>
    /// Public method to manually trigger scene change with custom scene name
    /// </summary>
    public void ChangeToScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[NetcodeSceneManager] Scene name cannot be empty!");
            return;
        }
        
        targetSceneName = sceneName;
        TriggerSceneChange();
    }
    
    /// <summary>
    /// Public method to set the target scene name
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;
        if (enableDebugLogs)
            Debug.Log($"[NetcodeSceneManager] Target scene set to: {sceneName}");
    }
    
    /// <summary>
    /// Get the current target scene name
    /// </summary>
    public string GetTargetScene()
    {
        return targetSceneName;
    }
    
    /// <summary>
    /// Check if currently loading a scene
    /// </summary>
    public bool IsLoadingScene()
    {
        return isLoading;
    }
    
    // Reset loading state when scene actually changes
    public override void OnNetworkSpawn()
    {
        isLoading = false;
    }
    
    // Optional: Reset loading state if network despawns
    public override void OnNetworkDespawn()
    {
        isLoading = false;
    }
    
    // Gizmo for easier visualization in scene view
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
}