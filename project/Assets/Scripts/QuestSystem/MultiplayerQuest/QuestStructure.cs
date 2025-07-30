using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Structure")]
public class QuestStructure : ScriptableObject
{
    [Header("Basic Info")]
    public string questName = "Collect Apples Quickly";
    public string questDescription = "Collect 5 apples before time runs out!";

    [Header("Quest Type")]
    public bool isCollection = true;        // ✅ Enable collection
    public bool isHitBased = false;         // ✅ Enable hit-based quest
    public bool isTimeBased = true;         // ✅ Enable time limit

    [Header("Collection Settings")]
    public string collectionNameTag = "Coin";  // Item to collect
    public int collectionCount = 5;             // How many to collect

    [Header("Hit-based Settings")]
    [Tooltip("Tag of objects that can be hit (e.g., 'Target', 'Enemy', 'Barrel')")]
    public string hitTargetTag = "Target";      // What to hit
    [Tooltip("How many hits are required")]
    public int requiredHits = 3;                // How many hits needed
    [Tooltip("Tag of projectile that can score hits (e.g., 'WaterProjectile', 'Bullet')")]
    public string projectileTag = "WaterProjectile"; // What projectile counts

    [Header("Time Settings")]
    public float timeInMinute = 2.0f;           // 2 minutes to complete

    // Validation
    private void OnValidate()
    {
        // Ensure at least one quest type is selected
        if (!isCollection && !isHitBased)
        {
            isCollection = true;
        }
        
        // Ensure counts are positive
        collectionCount = Mathf.Max(1, collectionCount);
        requiredHits = Mathf.Max(1, requiredHits);
        timeInMinute = Mathf.Max(0.1f, timeInMinute);
    }
}

/*
Quest Examples:

1. COLLECTION QUEST:
   - isCollection = true, isHitBased = false
   - "Collect 5 coins before time runs out"

2. HIT-BASED QUEST:
   - isCollection = false, isHitBased = true
   - "Hit 3 targets with water projectiles"

3. COMBO QUEST:
   - isCollection = true, isHitBased = true
   - "Collect 5 coins AND hit 3 targets"

4. PURE TIME QUEST:
   - isCollection = false, isHitBased = false
   - "Survive for 2 minutes"
*/