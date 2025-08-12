using UnityEngine;
using UnityEngine.AI;

public class PlayerFollow : MonoBehaviour
{
    [SerializeField] private GameObject player;
    private NavMeshAgent _agent;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        FindPlayerByName();
    }

    void FindPlayerByName()
    {
        // Sol(Clone) ismindeki objeyi bul
        GameObject foundPlayer = GameObject.Find("Sol(Clone)");
        
        if (foundPlayer != null)
        {
            player = foundPlayer;
            Debug.Log("Sol(Clone) bulundu ve hedef olarak ayarlandı!");
        }
        else
        {
            Debug.LogWarning("Sol(Clone) isimli obje bulunamadı!");
        }
    }

    void Update()
    {
        if (player != null)
        {
            _agent.SetDestination(player.transform.position);
        }
        else
        {
            // Player hala bulunamadıysa tekrar ara
            FindPlayerByName();
        }
    }
}