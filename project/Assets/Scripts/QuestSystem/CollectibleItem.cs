using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    public string tagName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            QuestManager.Instance.ReportItemCollected(tagName);
            Destroy(gameObject);
        }
    }
}