using UnityEngine;

public class WaterCollectionZone : MonoBehaviour
{
    [Header("Water Source Settings")]
    public float waterProvisionRate = 10f;
    public float maxWaterCapacity = 100f;
    public float currentWaterRemaining = 0f;
    public bool isCanRegenerable = false;
    public float regenerationTime = 30f;
    
    [Header("Visual Feedback")]
    public Color waterZoneColor = Color.cyan;
    public Color depletedZoneColor = Color.gray;
    public Color regeneratingZoneColor = Color.yellow;
    public bool showZoneOutline = true;
    
    private bool isPlayerInZone = false;
    private bool isWaterDepleted = false;
    private bool isRegenerating = false;
    private float regenerationTimer = 0f;
    private DewAbilitySystem currentDewSystem = null;
    
    private void Start()
    {
        // Setup collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("WaterCollectionZone requires a Collider component!");
            return;
        }
        col.isTrigger = true;
        
        // Ensure Water tag
        if (!gameObject.CompareTag("Water"))
        {
            gameObject.tag = "Water";
        }
        
        // Initialize water
        if (currentWaterRemaining <= 0f)
        {
            currentWaterRemaining = maxWaterCapacity;
        }
    }
    
    private void Update()
    {
        // Handle regeneration ONLY when depleted
        if (isWaterDepleted && isCanRegenerable && regenerationTime > 0f)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                regenerationTimer = 0f;
                Debug.Log($"{gameObject.name} started regenerating (depleted)");
            }
            
            regenerationTimer += Time.deltaTime;
            float progress = regenerationTimer / regenerationTime;
            
            if (progress >= 1f)
            {
                // Fully regenerated
                isWaterDepleted = false;
                isRegenerating = false;
                currentWaterRemaining = maxWaterCapacity;
                regenerationTimer = 0f;
                Debug.Log($"{gameObject.name} fully regenerated!");
            }
            else
            {
                // Gradually fill water during regeneration
                currentWaterRemaining = maxWaterCapacity * progress;
                
                // Once we have some water, we're no longer depleted
                if (currentWaterRemaining > 0f)
                {
                    isWaterDepleted = false;
                }
            }
        }
        
        // Continue regeneration even after no longer "depleted" until full
        else if (isRegenerating && isCanRegenerable && regenerationTime > 0f)
        {
            regenerationTimer += Time.deltaTime;
            float progress = regenerationTimer / regenerationTime;
            
            if (progress >= 1f)
            {
                // Fully regenerated
                isRegenerating = false;
                currentWaterRemaining = maxWaterCapacity;
                regenerationTimer = 0f;
                Debug.Log($"{gameObject.name} fully regenerated!");
            }
            else
            {
                // Continue gradually filling water
                currentWaterRemaining = maxWaterCapacity * progress;
            }
        }
        
        // Destroy non-regenerable sources when depleted
        else if (isWaterDepleted && !isCanRegenerable)
        {
            Debug.Log($"Destroying non-regenerable water source: {gameObject.name}");
            Destroy(gameObject, 2f); // 2 second delay
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            DewAbilitySystem dewSystem = other.GetComponent<DewAbilitySystem>();
            if (dewSystem != null)
            {
                isPlayerInZone = true;
                currentDewSystem = dewSystem;
                Debug.Log($"Player entered water zone: {gameObject.name} - Water: {currentWaterRemaining:F0}/{maxWaterCapacity:F0}");
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            DewAbilitySystem dewSystem = other.GetComponent<DewAbilitySystem>();
            if (dewSystem == currentDewSystem)
            {
                isPlayerInZone = false;
                currentDewSystem = null;
                Debug.Log($"Player exited water zone: {gameObject.name}");
            }
        }
    }
    
    public bool CanProvideWater()
    {
        // Cannot provide water while regenerating - must wait until fully recharged
        if (isRegenerating)
        {
            return false;
        }
        
        return currentWaterRemaining > 0f;
    }
    
    public float GetWaterProvisionRate()
    {
        return CanProvideWater() ? waterProvisionRate : 0f;
    }
    
    public float ConsumeWater(float requestedAmount)
    {
        if (!CanProvideWater()) return 0f;
        
        float actualAmount = Mathf.Min(requestedAmount, currentWaterRemaining);
        currentWaterRemaining -= actualAmount;
        
        if (currentWaterRemaining <= 0f)
        {
            currentWaterRemaining = 0f;
            isWaterDepleted = true;
            Debug.Log($"{gameObject.name} depleted! Regenerable: {isCanRegenerable}");
        }
        
        return actualAmount;
    }
    
    // Public getters
    public bool IsPlayerInZone() => isPlayerInZone;
    public bool IsWaterDepleted() => isWaterDepleted;
    public bool IsRegenerating() => isRegenerating;
    public float GetCurrentWaterRemaining() => currentWaterRemaining;
    public float GetMaxWaterCapacity() => maxWaterCapacity;
    public float GetRegenerationProgress() => regenerationTime > 0f ? regenerationTimer / regenerationTime : 0f;
    public float GetWaterPercentage() => maxWaterCapacity > 0f ? (currentWaterRemaining / maxWaterCapacity) * 100f : 0f;
    public bool IsCanRegenerable() => isCanRegenerable;
    
    // Utility methods
    public void RefillWater()
    {
        currentWaterRemaining = maxWaterCapacity;
        isWaterDepleted = false;
        isRegenerating = false;
    }
    
    public void DepleteWater()
    {
        currentWaterRemaining = 0f;
        isWaterDepleted = true;
    }
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        if (!showZoneOutline) return;
        
        Color gizmoColor = waterZoneColor;
        if (isRegenerating)
            gizmoColor = regeneratingZoneColor;
        else if (isWaterDepleted)
            gizmoColor = depletedZoneColor;
        
        Gizmos.color = gizmoColor;
        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showZoneOutline) return;
        
        Color gizmoColor = waterZoneColor;
        if (isRegenerating)
            gizmoColor = regeneratingZoneColor;
        else if (isWaterDepleted)
            gizmoColor = depletedZoneColor;
        
        gizmoColor.a = 0.3f;
        Gizmos.color = gizmoColor;
        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            Gizmos.DrawCube(transform.position, transform.localScale);
        }
        
        // Visual feedback labels
#if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        
        // Water info
        string waterInfo = $"Water: {currentWaterRemaining:F0}/{maxWaterCapacity:F0} ({GetWaterPercentage():F0}%)";
        UnityEditor.Handles.Label(labelPos, waterInfo);
        
        // Status label
        string statusLabel = "";
        if (isRegenerating)
        {
            float progress = GetRegenerationProgress();
            statusLabel = $"REGENERATING: {progress * 100f:F0}% - COLLECTION DISABLED";
        }
        else if (isWaterDepleted)
        {
            statusLabel = isCanRegenerable ? "DEPLETED - WILL REGENERATE" : "DEPLETED - WILL DESTROY";
        }
        else if (isPlayerInZone)
        {
            statusLabel = "PLAYER IN ZONE - COLLECTION AVAILABLE";
        }
        else
        {
            statusLabel = "AVAILABLE";
        }
        
        UnityEditor.Handles.Label(labelPos + Vector3.up * 0.5f, statusLabel);
        
        // Rate and regeneration info
        string rateInfo = $"Rate: {waterProvisionRate}/sec";
        if (isCanRegenerable)
        {
            rateInfo += $" | Regen: {regenerationTime}s";
        }
        UnityEditor.Handles.Label(labelPos + Vector3.up * 1f, rateInfo);
#endif
    }
    #endregion
}