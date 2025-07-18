using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class ButtonActions : MonoBehaviour
{
    private NetworkManager NetworkManager;
    private GameManager gameManager;
    
    [Header("UI References")]
    [Tooltip("Canvas containing the host/client buttons - will be disabled when button is clicked")]
    public Canvas menuCanvas;
    
    void Start()
    {
        NetworkManager = GetComponentInParent<NetworkManager>();
        gameManager = FindObjectOfType<GameManager>();
        
        // Auto-find canvas if not assigned - look for canvas named "Buttons" or find the correct one
        if (menuCanvas == null)
        {
            // First try to find a canvas named "Buttons"
            GameObject ServerUI = GameObject.Find("ServerUI");
            if (ServerUI != null)
            {
                menuCanvas = ServerUI.GetComponent<Canvas>();
            }
            
            // If still not found, try to find parent canvas
            if (menuCanvas == null)
            {
                menuCanvas = GetComponentInParent<Canvas>();
            }
            
            // Last resort: find any canvas with buttons
            if (menuCanvas == null)
            {
                Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in allCanvases)
                {
                    if (canvas.name.ToLower().Contains("button") || canvas.name.ToLower().Contains("menu"))
                    {
                        menuCanvas = canvas;
                        break;
                    }
                }
            }
        }
        
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in scene!");
        }
        
        if (menuCanvas == null)
        {
            Debug.LogError("Menu Canvas not found! Please assign the 'Buttons' Canvas in the inspector.");
        }
        else
        {
            Debug.Log($"ButtonActions: Found menu canvas: {menuCanvas.name}");
        }
    }

    public void StartHost()
    {
        Debug.Log("Start Host button clicked - disabling menu");
        
        // Disable the canvas immediately when button is clicked
        DisableCanvas();
        
        if (NetworkManager.StartHost())
        {
            Debug.Log("Host started successfully");
            // Use coroutine for more reliable timing
            StartCoroutine(WaitAndSpawnHost());
        }
        else
        {
            Debug.LogError("Failed to start host");
            // Re-enable canvas if failed to start
            EnableCanvas();
        }
    }

    public void StartClient()
    {
        Debug.Log("Start Client button clicked - disabling menu");
        
        // Disable the canvas immediately when button is clicked
        DisableCanvas();
        
        if (NetworkManager.StartClient())
        {
            Debug.Log("Client connection started");
            // Subscribe to connection events for clients
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogError("Failed to start client");
            // Re-enable canvas if failed to start
            EnableCanvas();
        }
    }
    
    private void DisableCanvas()
    {
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(false);
            Debug.Log("Menu canvas disabled");
        }
    }
    
    private void EnableCanvas()
    {
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(true);
            Debug.Log("Menu canvas enabled");
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
            // Re-enable canvas if spawn failed
            EnableCanvas();
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
            // Re-enable canvas if spawn failed
            EnableCanvas();
        }
    }
    
    // Public method to manually re-enable canvas (useful for disconnect/back to menu functionality)
    public void ShowMenu()
    {
        EnableCanvas();
    }
    
    // Public method to manually disable canvas
    public void HideMenu()
    {
        DisableCanvas();
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