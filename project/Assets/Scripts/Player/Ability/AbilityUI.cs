using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityUI : MonoBehaviour
{
    [Header("Dew UI")]
    public Slider waterBar;
    public Image waterBarFill;
    public TextMeshProUGUI waterText;
    public TextMeshProUGUI waterDistanceText;
    public TextMeshProUGUI waterChargingText;
    public GameObject waterChargingIndicator;
    
    [Header("Sol UI")]
    public Slider solarEnergyBar;
    public Image solarEnergyBarFill;
    public TextMeshProUGUI solarEnergyText;
    public TextMeshProUGUI timeOfDayText;
    public GameObject sunlightIndicator;
    
    [Header("Colors")]
    public Color waterColor = Color.blue;
    public Color solarColor = Color.orange;
    public Color chargingColor = Color.yellow;
    public Color readyColor = Color.green;
    public Color lowWaterColor = Color.red;
    public Color waterInRangeColor = Color.cyan;
    public Color waterOutOfRangeColor = Color.red;
    
    [Header("Update Settings")]
    public float waterDistanceUpdateRate = 0.1f;
    
    private DewAbilitySystem dewSystem;
    private SolAbilitySystem solSystem;
    private bool isInitialized = false;
    private float lastWaterDistanceUpdate = 0f;
    
    // Initialize with specific ability system
    public void Initialize(MonoBehaviour abilitySystem)
    {
        // Cache specific system references
        dewSystem = abilitySystem as DewAbilitySystem;
        solSystem = abilitySystem as SolAbilitySystem;
        
        isInitialized = true;
        
        // Setup character-specific UI
        SetupCharacterUI();
        
        Debug.Log($"AbilityUI initialized for {abilitySystem.GetType().Name}");
    }
    
    private void SetupCharacterUI()
    {
        bool isDew = dewSystem != null;
        bool isSol = solSystem != null;
        
        // Show/hide Dew UI
        SetUIElementActive(waterBar?.gameObject, isDew);
        SetUIElementActive(waterText?.gameObject, isDew);
        SetUIElementActive(waterDistanceText?.gameObject, isDew);
        SetUIElementActive(waterChargingText?.gameObject, isDew);
        SetUIElementActive(waterChargingIndicator, isDew);
        
        // Show/hide Sol UI
        SetUIElementActive(solarEnergyBar?.gameObject, isSol);
        SetUIElementActive(solarEnergyText?.gameObject, isSol);
        SetUIElementActive(timeOfDayText?.gameObject, isSol);
        SetUIElementActive(sunlightIndicator, isSol);
        
        // Setup specific UI
        if (isDew)
        {
            SetupDewUI();
        }
        else if (isSol)
        {
            SetupSolUI();
        }
    }
    
    private void SetupDewUI()
    {
        if (dewSystem == null) return;
        
        // Setup water bar using DewAbilitySystem variables
        if (waterBar != null)
        {
            waterBar.minValue = 0f;
            waterBar.maxValue = dewSystem.maxWaterCapacity;
            waterBar.value = dewSystem.currentWaterCapacity;
        }
        
        // Set initial water bar fill color
        if (waterBarFill != null)
        {
            waterBarFill.color = waterColor;
        }
        
        // Initialize charging indicator
        if (waterChargingIndicator != null)
        {
            waterChargingIndicator.SetActive(false);
        }
        
        // Initialize charging text
        if (waterChargingText != null)
        {
            waterChargingText.gameObject.SetActive(false);
        }
    }
    
    private void SetupSolUI()
    {
        if (solSystem == null) return;
        
        // Setup solar energy bar
        if (solarEnergyBar != null)
        {
            solarEnergyBar.minValue = 0f;
            solarEnergyBar.maxValue = solSystem.GetMaxSolarEnergy();
            solarEnergyBar.value = solSystem.GetCurrentSolarEnergy();
        }
        
        // Set initial solar bar fill color
        if (solarEnergyBarFill != null)
        {
            solarEnergyBarFill.color = solarColor;
        }
        
        // Initialize sunlight indicator
        if (sunlightIndicator != null)
        {
            sunlightIndicator.SetActive(false);
        }
    }
    
    private void SetUIElementActive(GameObject element, bool active)
    {
        if (element != null)
            element.SetActive(active);
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Update Dew UI automatically
        if (dewSystem != null && Time.time >= lastWaterDistanceUpdate + waterDistanceUpdateRate)
        {
            UpdateDewUI();
            lastWaterDistanceUpdate = Time.time;
        }
        
        // Update Sol UI automatically
        if (solSystem != null)
        {
            UpdateSolUI();
        }
    }
    
    private void UpdateDewUI()
    {
        if (dewSystem == null) return;
        
        // Update water capacity
        UpdateWaterInfo(dewSystem.currentWaterCapacity, dewSystem.maxWaterCapacity);
        
        // Update charging state
        UpdateWaterChargingState(dewSystem.IsCharging());
        
        // Update water distance
        float nearestDistance = dewSystem.GetNearestWaterDistance();
        bool isInWaterZone = dewSystem.IsInWaterZone();
        UpdateWaterDistance(nearestDistance, isInWaterZone);
        
        // Update charging text
        UpdateWaterChargingText(nearestDistance, isInWaterZone);
    }
    
    private void UpdateSolUI()
    {
        if (solSystem == null) return;
        
        // Update solar energy
        UpdateSolarInfo(
            solSystem.GetCurrentSolarEnergy(),
            solSystem.GetMaxSolarEnergy(),
            solSystem.GetIsInSunlight(),
            solSystem.GetTimeOfDay()
        );
    }
    
    // Dew-specific UI updates
    public void UpdateWaterInfo(float currentWater, float maxWater)
    {
        // Update water bar
        if (waterBar != null)
        {
            waterBar.value = currentWater;
            waterBar.maxValue = maxWater;
        }
        
        // Update water text
        if (waterText != null)
        {
            waterText.text = $"Water: {currentWater:F0}/{maxWater:F0}";
            
            // Color the text based on water level
            if (currentWater >= maxWater)
            {
                waterText.color = readyColor;
            }
            else if (dewSystem != null && currentWater >= dewSystem.waterCostPerShot)
            {
                waterText.color = waterColor;
            }
            else
            {
                waterText.color = lowWaterColor;
            }
        }
        
        // Update water bar fill color
        if (waterBarFill != null)
        {
            if (currentWater <= 0f)
                waterBarFill.color = lowWaterColor;
            else if (currentWater >= maxWater)
                waterBarFill.color = readyColor;
            else if (dewSystem != null && dewSystem.IsCharging())
                waterBarFill.color = chargingColor;
            else
                waterBarFill.color = waterColor;
        }
    }
    
    public void UpdateWaterChargingState(bool isCharging)
    {
        if (waterChargingIndicator != null)
            waterChargingIndicator.SetActive(isCharging);
    }
    
    public void UpdateWaterDistance(float distance, bool inRange)
    {
        if (waterDistanceText == null) return;
        
        if (distance == -1f)
        {
            waterDistanceText.text = "No water sources found";
            waterDistanceText.color = waterOutOfRangeColor;
        }
        else
        {
            waterDistanceText.text = $"Water: {distance:F1}m";
            waterDistanceText.color = inRange ? waterInRangeColor : waterOutOfRangeColor;
        }
    }
    
    public void UpdateWaterDistance(string distanceText)
    {
        if (waterDistanceText != null)
            waterDistanceText.text = distanceText;
    }
    
    private void UpdateWaterChargingText(float distance, bool inZone)
    {
        if (dewSystem == null || waterChargingText == null) return;
        
        if (dewSystem.IsCharging())
        {
            waterChargingText.text = "Charging Water...";
            waterChargingText.color = chargingColor;
            waterChargingText.gameObject.SetActive(true);
        }
        else if (inZone && distance != -1f)
        {
            if (dewSystem.currentWaterCapacity < dewSystem.maxWaterCapacity)
            {
                waterChargingText.text = "Hold to charge water";
                waterChargingText.color = waterInRangeColor;
            }
            else
            {
                waterChargingText.text = "Water tank full";
                waterChargingText.color = readyColor;
            }
            waterChargingText.gameObject.SetActive(true);
        }
        else
        {
            waterChargingText.gameObject.SetActive(false);
        }
    }
    
    // Sol-specific UI updates
    public void UpdateSolarInfo(float currentSolar, float maxSolar, bool inSunlight, float timeOfDay)
    {
        // Update solar energy bar
        if (solarEnergyBar != null)
        {
            solarEnergyBar.value = currentSolar;
            solarEnergyBar.maxValue = maxSolar;
        }
        
        // Update solar energy text
        if (solarEnergyText != null)
        {
            solarEnergyText.text = $"Solar: {currentSolar:F0}/{maxSolar:F0}";
            solarEnergyText.color = inSunlight ? solarColor : Color.gray;
        }
        
        // Update solar bar fill color
        if (solarEnergyBarFill != null)
        {
            if (currentSolar <= 0f)
                solarEnergyBarFill.color = Color.gray;
            else if (currentSolar >= maxSolar)
                solarEnergyBarFill.color = readyColor;
            else if (inSunlight)
                solarEnergyBarFill.color = chargingColor;
            else
                solarEnergyBarFill.color = solarColor;
        }
        
        // Update sunlight indicator
        if (sunlightIndicator != null)
            sunlightIndicator.SetActive(inSunlight);
        
        // Update time of day
        if (timeOfDayText != null)
        {
            int hours = Mathf.FloorToInt(timeOfDay);
            int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
            timeOfDayText.text = $"{hours:D2}:{minutes:D2}";
            timeOfDayText.color = inSunlight ? Color.yellow : Color.blue;
        }
    }
    
    // Public utility methods
    public void RefreshWaterDistance()
    {
        if (dewSystem != null)
        {
            UpdateDewUI();
        }
    }
    
    public float GetCurrentWaterDistance()
    {
        return dewSystem?.GetNearestWaterDistance() ?? -1f;
    }
    
    public void ShowAbilityUsed(string abilityName)
    {
        if (dewSystem != null && waterText != null)
        {
            StartCoroutine(ShowTemporaryWaterText($"{abilityName} Used!", 1f));
        }
        else if (solSystem != null && solarEnergyText != null)
        {
            StartCoroutine(ShowTemporarySolarText($"{abilityName} Used!", 1f));
        }
    }
    
    private System.Collections.IEnumerator ShowTemporaryWaterText(string text, float duration)
    {
        if (waterText == null) yield break;
        
        string originalText = waterText.text;
        Color originalColor = waterText.color;
        
        waterText.text = text;
        waterText.color = chargingColor;
        
        yield return new WaitForSeconds(duration);
        
        waterText.text = originalText;
        waterText.color = originalColor;
    }
    
    private System.Collections.IEnumerator ShowTemporarySolarText(string text, float duration)
    {
        if (solarEnergyText == null) yield break;
        
        string originalText = solarEnergyText.text;
        Color originalColor = solarEnergyText.color;
        
        solarEnergyText.text = text;
        solarEnergyText.color = chargingColor;
        
        yield return new WaitForSeconds(duration);
        
        solarEnergyText.text = originalText;
        solarEnergyText.color = originalColor;
    }
    
    // Public getters
    public bool IsInitialized() => isInitialized;
    public bool IsDewCharacter() => dewSystem != null;
    public bool IsSolCharacter() => solSystem != null;
    public DewAbilitySystem GetDewSystem() => dewSystem;
    public SolAbilitySystem GetSolSystem() => solSystem;
    
    // Method to be called from DewAbilitySystem.UpdateUI()
    public void UpdateChargingState(bool isCharging)
    {
        UpdateWaterChargingState(isCharging);
    }
    
    // Debug method
    public void DebugDisplayUIState()
    {
        if (!isInitialized)
        {
            Debug.Log("AbilityUI: Not initialized");
            return;
        }
        
        if (dewSystem != null)
        {
            Debug.Log($"AbilityUI (Dew): Water {dewSystem.currentWaterCapacity:F1}/{dewSystem.maxWaterCapacity}, " +
                     $"Distance: {dewSystem.GetNearestWaterDistance():F1}m, " +
                     $"Charging: {dewSystem.IsCharging()}, " +
                     $"In Zone: {dewSystem.IsInWaterZone()}, " +
                     $"Can Use: {dewSystem.CanUseAbility()}");
        }
        else if (solSystem != null)
        {
            Debug.Log($"AbilityUI (Sol): Solar {solSystem.GetCurrentSolarEnergy()}/{solSystem.GetMaxSolarEnergy()}, " +
                     $"In Sunlight: {solSystem.GetIsInSunlight()}");
        }
    }
}