using Unity.Netcode;
using UnityEngine;

public class QuestMasterMultiplayer : NetworkBehaviour
{
    [Header("Quest to Give")]
    [SerializeField] private QuestStructure questToGive;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            // Check if quest is already active before giving
            if (!IsQuestAlreadyActive())
            {
                GiveQuest();
            }
            else
            {
                Debug.Log($"Quest '{questToGive.questName}' is already active!");
            }
        }
    }

    private bool IsQuestAlreadyActive()
    {
        return NetworkedQuestManager.Instance.GetQuestInstance(questToGive) != null;
    }

    private void GiveQuest()
    {
        NetworkedQuestManager.Instance.StartQuest(questToGive);
        Debug.Log($"Quest given: {questToGive.questName}");
    }
}
