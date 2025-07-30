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
    public GameObject particleSystemAfterDepletion;
    
    [Header("Destruction Settings")]
    [Tooltip("Delay before destroying non-regenerable water sources (in seconds)")]
    public float destructionDelay = 2f;
    
    [Header("Water Level Colors")]
    [Tooltip("Color when water is at 100%")]
    public Color color100Percent = new Color(0f, 0.8f, 1f, 1f); // Bright cyan
    [Tooltip("Color when water is at 75%")]
    public Color color75Percent = new Color(0.2f, 0.6f, 0.9f, 1f); // Blue-cyan
    [Tooltip("Color when water is at 50%")]
    public Color color50Percent = new Color(0.4f, 0.4f, 0.8f, 1f); // Blue
    [Tooltip("Color when water is at 25%")]
    public Color color25Percent = new Color(0.8f, 0.3f, 0.2f, 1f); // Orange-red
    [Tooltip("Color when water is at 0%")]
    public Color color0Percent = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray
    
    [Header("Runtime Visual Settings")]
    [Tooltip("Enable runtime color changes on the object's material")]
    public bool enableRuntimeColorChange = true;
    [Tooltip("Target object for visual changes (leave empty to use this object)")]
    public GameObject visualTarget;
    [Tooltip("Use emission for glowing effects")]
    public bool useEmission = false;
    [Tooltip("Emission intensity multiplier")]
    public float emissionIntensity = 1.0f;
    
    private bool isPlayerInZone = false;
    private bool isWaterDepleted = false;
    private bool isRegenerating = false;
    private float regenerationTimer = 0f;
    private DewAbilitySystem currentDewSystem = null;
    
    // Destruction handling
    private bool destructionInitiated = false;
    
    // Visual components
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Material runtimeMaterial;
    private Color originalColor;
    
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
        
        // Setup visual components
        SetupVisualComponents();
        
        // Update initial visual state
        UpdateVisualState();
    }
    
    private void SetupVisualComponents()
    {
        if (!enableRuntimeColorChange) return;
        
        // Determine which object to get the renderer from
        GameObject targetObject = visualTarget != null ? visualTarget : gameObject;
        
        // If no specific target is set, try to find "Sphere" child
        if (visualTarget == null)
        {
            Transform sphereChild = transform.Find("Sphere");
            if (sphereChild != null)
            {
                targetObject = sphereChild.gameObject;
                Debug.Log($"Auto-found Sphere child for visual effects on {gameObject.name}");
            }
        }
        
        objectRenderer = targetObject.GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogWarning($"No Renderer found on {targetObject.name}. Runtime color changes disabled.");
            enableRuntimeColorChange = false;
            return;
        }
        
        // Store original material and color
        originalMaterial = objectRenderer.material;
        
        // Create a runtime instance of the material to avoid modifying the asset
        runtimeMaterial = new Material(originalMaterial);
        objectRenderer.material = runtimeMaterial;
        
        // Store original color (try different properties)
        if (runtimeMaterial.HasProperty("_Color"))
        {
            originalColor = runtimeMaterial.color;
        }
        else if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            originalColor = runtimeMaterial.GetColor("_BaseColor");
        }
        else
        {
            originalColor = Color.white;
        }
    }
    
    private void UpdateVisualState()
    {
        if (!enableRuntimeColorChange || runtimeMaterial == null) return;
        
        Color targetColor = GetCurrentStateColor();
        
        // Apply color to material
        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.color = targetColor;
        }
        else if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", targetColor);
        }
        
        // Handle emission if enabled
        if (useEmission && runtimeMaterial.HasProperty("_EmissionColor"))
        {
            if (isRegenerating)
            {
                // Pulsing emission during regeneration
                float pulse = Mathf.Sin(Time.time * 2f) * 0.5f + 0.5f;
                Color emissionColor = regeneratingZoneColor * emissionIntensity * pulse;
                runtimeMaterial.SetColor("_EmissionColor", emissionColor);
                runtimeMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                // Emission based on water level
                float waterPercent = GetWaterPercentage() / 100f;
                Color baseEmissionColor = GetWaterLevelColor(GetWaterPercentage());
                Color emissionColor = baseEmissionColor * emissionIntensity * waterPercent * 0.3f;
                runtimeMaterial.SetColor("_EmissionColor", emissionColor);
                
                if (waterPercent > 0f)
                {
                    runtimeMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    runtimeMaterial.DisableKeyword("_EMISSION");
                }
            }
        }
    }
    
    private Color GetCurrentStateColor()
    {
        if (isRegenerating)
        {
            // During regeneration, interpolate between the water level color and regenerating color
            float progress = GetRegenerationProgress();
            Color waterLevelColor = GetWaterLevelColor(GetWaterPercentage());
            return Color.Lerp(waterLevelColor, regeneratingZoneColor, 0.5f + Mathf.Sin(Time.time * 3f) * 0.3f);
        }
        else
        {
            // Normal state - use water level color
            return GetWaterLevelColor(GetWaterPercentage());
        }
    }
    
    private Color GetWaterLevelColor(float waterPercentage)
    {
        // Clamp percentage to 0-100 range
        waterPercentage = Mathf.Clamp(waterPercentage, 0f, 100f);
        
        // Define the color stops and their percentages
        if (waterPercentage >= 100f)
        {
            return color100Percent;
        }
        else if (waterPercentage >= 75f)
        {
            // Interpolate between 75% and 100%
            float t = (waterPercentage - 75f) / 25f;
            return Color.Lerp(color75Percent, color100Percent, t);
        }
        else if (waterPercentage >= 50f)
        {
            // Interpolate between 50% and 75%
            float t = (waterPercentage - 50f) / 25f;
            return Color.Lerp(color50Percent, color75Percent, t);
        }
        else if (waterPercentage >= 25f)
        {
            // Interpolate between 25% and 50%
            float t = (waterPercentage - 25f) / 25f;
            return Color.Lerp(color25Percent, color50Percent, t);
        }
        else if (waterPercentage > 0f)
        {
            // Interpolate between 0% and 25%
            float t = waterPercentage / 25f;
            return Color.Lerp(color0Percent, color25Percent, t);
        }
        else
        {
            // Completely depleted
            return color0Percent;
        }
    }
    
    private void Update()
    {
        bool previouslyRegenerating = isRegenerating;
        bool previouslyDepleted = isWaterDepleted;
        float previousWaterAmount = currentWaterRemaining;
        
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
        
        // Handle destruction of non-regenerable sources when depleted
        else if (isWaterDepleted && !isCanRegenerable && !destructionInitiated)
        {
            destructionInitiated = true;
            Debug.Log($"Initiating destruction of non-regenerable water source: {gameObject.name}");
            
            // Spawn particle effect
            if (particleSystemAfterDepletion != null)
            {
                Vector3 particlePosition = new Vector3(transform.position.x, transform.position.y , transform.position.z);
                GameObject particle = Instantiate(particleSystemAfterDepletion, particlePosition, Quaternion.identity);
                Destroy(particle, 5f); // Destroy particle after 5 seconds
            }
            
            // Start destruction coroutine
            StartCoroutine(DestroyAfterDelay());
        }
        
        // Update visual state if anything changed
        if (previouslyRegenerating != isRegenerating || 
            previouslyDepleted != isWaterDepleted || 
            Mathf.Abs(previousWaterAmount - currentWaterRemaining) > 0.1f ||
            (useEmission && isRegenerating)) // Always update during regeneration for pulsing effect
        {
            UpdateVisualState();
        }
    }
    
    private System.Collections.IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destructionDelay);
        Debug.Log($"Destroying water source: {gameObject.name}");
        Destroy(gameObject);
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
        
        // Update visual immediately after consumption
        UpdateVisualState();
        
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
    
    // Utility methods for testing water levels
    public void RefillWater()
    {
        currentWaterRemaining = maxWaterCapacity;
        isWaterDepleted = false;
        isRegenerating = false;
        destructionInitiated = false; // Reset destruction flag
        UpdateVisualState();
    }
    
    public void DepleteWater()
    {
        currentWaterRemaining = 0f;
        isWaterDepleted = true;
        UpdateVisualState();
    }
    
    // Methods to test specific water levels
    [ContextMenu("Set Water to 100%")]
    public void SetWaterTo100() { SetWaterLevel(100f); }
    
    [ContextMenu("Set Water to 75%")]
    public void SetWaterTo75() { SetWaterLevel(75f); }
    
    [ContextMenu("Set Water to 50%")]
    public void SetWaterTo50() { SetWaterLevel(50f); }
    
    [ContextMenu("Set Water to 25%")]
    public void SetWaterTo25() { SetWaterLevel(25f); }
    
    [ContextMenu("Set Water to 0%")]
    public void SetWaterTo0() { SetWaterLevel(0f); }
    
    public void SetWaterLevel(float percentage)
    {
        percentage = Mathf.Clamp(percentage, 0f, 100f);
        currentWaterRemaining = (percentage / 100f) * maxWaterCapacity;
        isWaterDepleted = currentWaterRemaining <= 0f;
        isRegenerating = false;
        destructionInitiated = false; // Reset destruction flag when manually setting water level
        UpdateVisualState();
        Debug.Log($"Water set to {percentage}% ({currentWaterRemaining:F1}/{maxWaterCapacity})");
    }
    
    // Clean up runtime material when destroyed
    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            DestroyImmediate(runtimeMaterial);
        }
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
        if (destructionInitiated)
        {
            statusLabel = "DESTROYING...";
        }
        else if (isRegenerating)
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
        else
        {
            rateInfo += $" | Destroy Delay: {destructionDelay}s";
        }
        UnityEditor.Handles.Label(labelPos + Vector3.up * 1f, rateInfo);
#endif
    }
    #endregion
}