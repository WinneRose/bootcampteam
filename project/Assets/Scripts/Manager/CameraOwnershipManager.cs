using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple component to ensure Camera elements only show for the owning player.
/// Attach this to any Canvas or Camera GameObject that should only be visible to the local player.
/// </summary>
public class CameraOwnershipManager : NetworkBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, this Camera will only be active for the owner of the NetworkObject")]
    public bool ownerOnly = true;
    
    [Tooltip("If true, will also disable all child Canvas components for non-owners")]
    public bool disableChildCanvases = true;
    
    [Tooltip("If true, will also disable all child Camera components for non-owners")]
    public bool disableChildCamera = true;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        SetupCameraOwnership();
    }
    
    private void SetupCameraOwnership()
    {
        if (!ownerOnly)
        {
            // If not owner-only, ensure Camera is active for everyone
            gameObject.SetActive(true);
            if (showDebugLogs)
                Debug.Log($"Camera {gameObject.name} set to show for all players");
            return;
        }
        
        if (IsOwner)
        {
            // Enable Camera for owner
            EnableCameraForOwner();
        }
        else
        {
            // Disable Camera for non-owners
            DisableCameraForNonOwner();
        }
    }
    
    private void EnableCameraForOwner()
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
            Debug.Log($"Enabled Camera {gameObject.name} for owner (local player)");
    }
    
    private void DisableCameraForNonOwner()
    {
        gameObject.SetActive(false);
        
        // Optionally disable child canvases
        if (disableChildCamera)
        {
            Camera[] childCamera = GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in childCamera)
            {
                camera.gameObject.SetActive(false);
            }
        }
        
      
        if (showDebugLogs)
            Debug.Log($"Disabled Camera {gameObject.name} for non-owner (other player)");
    }
    
    // Public method to manually refresh ownership
    [ContextMenu("Refresh Camera Ownership")]
    public void RefreshOwnership()
    {
        if (IsSpawned)
        {
            SetupCameraOwnership();
        }
    }
    
    // Called when ownership changes (if using ownership transfer)
    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        if (ownerOnly)
        {
            EnableCameraForOwner();
        }
    }
    
    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        if (ownerOnly)
        {
            DisableCameraForNonOwner();
        }
    }
}