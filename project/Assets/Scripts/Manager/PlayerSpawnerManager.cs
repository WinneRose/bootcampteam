using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    public GameObject hostPrefab;
    public GameObject clientPrefab;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Host = Server + Client
        bool isHost = clientId == NetworkManager.ServerClientId;

        GameObject prefabToSpawn = isHost ? hostPrefab : clientPrefab;

        GameObject playerObject = Instantiate(prefabToSpawn, GetSpawnPosition(), Quaternion.identity);

        playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    private Vector3 GetSpawnPosition()
    {
        // Example: random spawn point
        return new Vector3(Random.Range(-5, 5), 1, Random.Range(-5, 5));
    }

    private void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}