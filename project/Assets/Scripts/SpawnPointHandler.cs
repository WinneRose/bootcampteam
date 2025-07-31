using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class SpawnPointHandler : NetworkBehaviour
{
    public Transform Dew_SpawnPoint;
    public Transform Sol_SpawnPoint;

    public GameObject Dew_Player;
    public GameObject Sol_Player;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Wait longer to ensure all clients have spawned their players
            StartCoroutine(PositionPlayersAfterDelay());
        }
    }

    private System.Collections.IEnumerator PositionPlayersAfterDelay()
    {
        // Wait longer for all clients to be ready
        yield return new WaitForSeconds(0.5f);
        
        Dew_Player = GameObject.Find("Dew(Clone)");
        Sol_Player = GameObject.Find("Sol(Clone)");
        
        if (Dew_Player != null)
        {
            Vector3 dewPos = Dew_SpawnPoint.position;
            ForcePlayerPosition(Dew_Player, dewPos);
            
            // Get NetworkObject ID for more reliable identification
            NetworkObject dewNetObj = Dew_Player.GetComponent<NetworkObject>();
            if (dewNetObj != null)
            {
                ForcePlayerPositionClientRpc(dewNetObj.NetworkObjectId, dewPos);
            }
        }
            
        if (Sol_Player != null)
        {
            Vector3 solPos = Sol_SpawnPoint.position;
            ForcePlayerPosition(Sol_Player, solPos);
            
            // Get NetworkObject ID for more reliable identification
            NetworkObject solNetObj = Sol_Player.GetComponent<NetworkObject>();
            if (solNetObj != null)
            {
                ForcePlayerPositionClientRpc(solNetObj.NetworkObjectId, solPos);
            }
        }
    }

    private void ForcePlayerPosition(GameObject player, Vector3 position)
    {
        // Disable NetworkTransform temporarily
        NetworkTransform netTransform = player.GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.enabled = false;
        }
        
        // Force position
        player.transform.position = position;
        
        // Re-enable NetworkTransform after a short delay
        if (netTransform != null)
        {
            StartCoroutine(ReEnableNetworkTransform(netTransform));
        }
        
        Debug.Log($"Server: Forced {player.name} to position {position}");
    }

    private System.Collections.IEnumerator ReEnableNetworkTransform(NetworkTransform netTransform)
    {
        yield return new WaitForSeconds(0.1f);
        if (netTransform != null)
        {
            netTransform.enabled = true;
        }
    }

    [ClientRpc]
    private void ForcePlayerPositionClientRpc(ulong networkObjectId, Vector3 position)
    {
        StartCoroutine(ForcePositionWithRetry(networkObjectId, position));
    }

    private System.Collections.IEnumerator ForcePositionWithRetry(ulong networkObjectId, Vector3 position)
    {
        int attempts = 0;
        int maxAttempts = 20;
        
        while (attempts < maxAttempts)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
            {
                // Multiple force attempts
                ForceNetworkObjectPosition(networkObject, position);
                Debug.Log($"Client: Forced player position via NetworkObjectId: {networkObjectId} to {position} on attempt {attempts + 1}");
                
                // Wait a bit and try again to make sure it sticks
                yield return new WaitForSeconds(0.05f);
                
                // Check if position actually changed
                if (Vector3.Distance(networkObject.transform.position, position) < 0.1f)
                {
                    Debug.Log($"Client: Position successfully set for NetworkObjectId: {networkObjectId}");
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.LogWarning($"Client: Failed to set position for NetworkObjectId: {networkObjectId} after {maxAttempts} attempts");
    }

    private void ForceNetworkObjectPosition(NetworkObject networkObject, Vector3 position)
    {
        // Method 1: Disable NetworkTransform temporarily
        NetworkTransform netTransform = networkObject.GetComponent<NetworkTransform>();
        bool wasEnabled = false;
        
        if (netTransform != null)
        {
            wasEnabled = netTransform.enabled;
            netTransform.enabled = false;
        }
        
        // Method 2: Force transform position
        networkObject.transform.position = position;
        
        // Method 3: If there's a CharacterController, disable and re-enable it
        CharacterController charController = networkObject.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
            networkObject.transform.position = position;
            charController.enabled = true;
        }
        
        // Method 4: If there's a Rigidbody, use MovePosition
        Rigidbody rb = networkObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.MovePosition(position);
            rb.linearVelocity = Vector3.zero; // Stop any movement
        }
        
        // Re-enable NetworkTransform
        if (netTransform != null && wasEnabled)
        {
            StartCoroutine(ReEnableNetworkTransformDelayed(netTransform));
        }
    }

    private System.Collections.IEnumerator ReEnableNetworkTransformDelayed(NetworkTransform netTransform)
    {
        yield return new WaitForSeconds(0.2f);
        if (netTransform != null)
        {
            netTransform.enabled = true;
        }
    }

    // Manual methods for testing
    [ContextMenu("Force Reposition Players")]
    public void ForceRepositionPlayers()
    {
        if (IsServer)
        {
            StartCoroutine(PositionPlayersAfterDelay());
        }
    }

    // Server RPC for manual repositioning
    [ServerRpc(RequireOwnership = false)]
    public void ForceRepositionPlayersServerRpc()
    {
        ForceRepositionPlayers();
    }
}