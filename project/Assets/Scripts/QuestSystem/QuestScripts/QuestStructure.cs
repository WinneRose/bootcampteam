using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "QuestMaster/Quest Template")]

public class QuestStructure : ScriptableObject
{
    [Header("Quest Template")]
    public string questName;
    public string questDescription;
    
    public string questType; // time, collection
    
    [Header("Time Based Quest")]
    public bool isTimeBased;
    public float timeInMinute;
    
    [Header("Collection Based Quest")]
    public bool isCollection;
    public int collectionCount; // Amount of Object
    public string collectionNameTag; //Which is Collected
}
