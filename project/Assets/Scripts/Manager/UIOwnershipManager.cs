using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple component to ensure UI elements only show for the owning player.
/// Attach this to any Canvas or UI GameObject that should only be visible to the local player.
/// </summary>
public class UIOwnershipManager : NetworkBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, this UI will only be active for the owner of the NetworkObject")]
    public bool ownerOnly = true;
    
    [Tooltip("If true, will also disable all child Canvas components for non-owners")]
    public bool disableChildCanvases = true;
    
    [Tooltip("If true, will also disable all child UI components for non-owners")]
    public bool disableChildUI = true;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        SetupUIOwnership();
    }
    
    private void SetupUIOwnership()
    {
        if (!ownerOnly)
        {
            // If not owner-only, ensure UI is active for everyone
            gameObject.SetActive(true);
            if (showDebugLogs)
                Debug.Log($"UI {gameObject.name} set to show for all players");
            return;
        }
        
        if (IsOwner)
        {
            // Enable UI for owner
            EnableUIForOwner();
        }
        else
        {
            // Disable UI for non-owners
            DisableUIForNonOwner();
        }
    }
    
    private void EnableUIForOwner()
    {
        gameObject.SetActive(true);
        
        // Setup Canvas if this is one
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }
        
        if (showDebugLogs)
            Debug.Log($"Enabled UI {gameObject.name} for owner (local player)");
    }
    
    private void DisableUIForNonOwner()
    {
        gameObject.SetActive(false);
        
        // Optionally disable child canvases
        if (disableChildCanvases)
        {
            Canvas[] childCanvases = GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in childCanvases)
            {
                canvas.gameObject.SetActive(false);
            }
        }
        
        // Optionally disable child UI components
        if (disableChildUI)
        {
            // Disable common UI components
            UnityEngine.UI.Graphic[] graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var graphic in graphics)
            {
                graphic.gameObject.SetActive(false);
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"Disabled UI {gameObject.name} for non-owner (other player)");
    }
    
    // Public method to manually refresh ownership
    [ContextMenu("Refresh UI Ownership")]
    public void RefreshOwnership()
    {
        if (IsSpawned)
        {
            SetupUIOwnership();
        }
    }
    
    // Called when ownership changes (if using ownership transfer)
    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        if (ownerOnly)
        {
            EnableUIForOwner();
        }
    }
    
    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        if (ownerOnly)
        {
            DisableUIForNonOwner();
        }
    }
}