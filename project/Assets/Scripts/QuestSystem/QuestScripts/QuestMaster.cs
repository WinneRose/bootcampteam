using UnityEngine;

public class QuestMaster : MonoBehaviour
{
    public QuestStructure questTemplate;
    private bool playerInRange = false;
    private bool questGiven = false;

    public void TryInteract()
    {
        if (playerInRange)
        {
            QuestManager.Instance.StartQuest(questTemplate);
            Debug.Log("Quest accepted!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        QuestManager.Instance.StartQuest(questTemplate);
        Debug.Log("Quest accepted!");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }
}