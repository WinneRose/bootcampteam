using UnityEngine;


[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Structure")]
public class QuestStructure : ScriptableObject
{
    [Header("Basic Info")]
    public string questName = "Collect Apples Quickly";
    public string questDescription = "Collect 5 apples before time runs out!";

    [Header("Quest Type")]
    public bool isCollection = true;        // ✅ Enable collection
    public bool isTimeBased = true;         // ✅ Enable time limit

    [Header("Collection Settings")]
    public string collectionNameTag = "Coin";  // Item to collect
    public int collectionCount = 5;             // How many to collect

    [Header("Time Settings")]
    public float timeInMinute = 2.0f;           // 2 minutes to complete
}

/*
This creates a quest where players must:
- Collect 5 apples (collection goal)
- Within 2 minutes (time limit)

Possible outcomes:
1. Collect 5 apples before time runs out → COMPLETED ✅
2. Time runs out before collecting 5 apples → FAILED ❌
*/