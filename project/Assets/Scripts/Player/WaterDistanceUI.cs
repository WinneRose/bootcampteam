using TMPro;
using UnityEngine;
using Unity.Netcode;

public class WaterDistanceUI : NetworkBehaviour
{
    public float searchRadius = 100f;
    public TextMeshProUGUI distanceText;

    private Transform player;

    void Start()
    {
        if (!IsOwner)
        {
            enabled = false; // Bu oyuncu bizim değilse script çalışmasın
            return;
        }

        player = transform;
    }

    void Update()
    {
        GameObject[] waters = GameObject.FindGameObjectsWithTag("Water");
        float closestDistance = Mathf.Infinity;

        foreach (GameObject water in waters)
        {
            float distance = Vector3.Distance(player.position, water.transform.position);
            if (distance < closestDistance && distance <= searchRadius)
            {
                closestDistance = distance;
            }
        }

        if (closestDistance == Mathf.Infinity)
        {
            distanceText.text = "There is no water nearby";
        }
        else
        {
            distanceText.text = "Distance: " + Mathf.RoundToInt(closestDistance) + " m";
        }
    }
}
