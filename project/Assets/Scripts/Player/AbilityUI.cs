using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider abilityBar;
    public TextMeshProUGUI abilityText;
    public Image abilityBarFill;
    public TextMeshProUGUI zoneInfoText; // Shows current zone progress
    public GameObject chargingIndicator; // Shows when charging
    
    [Header("Visual Settings")]
    public Color normalColor = Color.blue;
    public Color chargingColor = Color.yellow;
    public Color readyColor = Color.green;
    public Color insufficientColor = Color.red;
    
    [Header("Animation")]
    public float animationSpeed = 2f;
    
    private AbilitySystem abilitySystem;
    private bool isInitialized = false;
    
    public void Initialize(AbilitySystem system)
    {
        abilitySystem = system;
        isInitialized = true;
        
        if (abilityBar != null)
        {
            abilityBar.minValue = 0f;
            abilityBar.maxValue = system.GetMaxAbilityPoints();
            abilityBar.value = 0f;
        }
        
        if (chargingIndicator != null)
            chargingIndicator.SetActive(false);
        
        UpdateAbilityBar(0f, system.GetMaxAbilityPoints());
    }
    
    public void UpdateAbilityBar(float currentPoints, float maxPoints)
    {
        if (!isInitialized) return;
        
        // Update slider value
        if (abilityBar != null)
        {
            abilityBar.value = currentPoints;
        }
        
        // Update text
        if (abilityText != null)
        {
            abilityText.text = $"{currentPoints:F0}/{maxPoints:F0}";
        }
        
        // Update color based on state
        UpdateBarColor(currentPoints, maxPoints);
    }
    
    public void UpdateChargingState(bool isCharging)
    {
        if (chargingIndicator != null)
        {
            chargingIndicator.SetActive(isCharging);
        }
        
        // Add pulsing effect when charging
        if (isCharging && abilityBarFill != null)
        {
            float pulse = Mathf.Sin(Time.time * animationSpeed) * 0.3f + 0.7f;
            Color chargingColorPulsed = chargingColor;
            chargingColorPulsed.a = pulse;
            abilityBarFill.color = chargingColorPulsed;
        }
    }
    
    public void UpdateZoneInfo(float currentZonePoints, float maxZonePoints, bool inZone)
    {
        if (zoneInfoText != null)
        {
            if (inZone)
            {
                zoneInfoText.text = $"Zone: {currentZonePoints:F0}/{maxZonePoints:F0}";
                zoneInfoText.color = currentZonePoints >= maxZonePoints ? insufficientColor : chargingColor;
            }
            else
            {
                zoneInfoText.text = "Find Ability Zone";
                zoneInfoText.color = Color.gray;
            }
        }
    }
    
    private void UpdateBarColor(float currentPoints, float maxPoints)
    {
        if (abilityBarFill == null) return;
        
        Color targetColor;
        
        if (abilitySystem != null && abilitySystem.IsInAbilityZone() && Input.GetMouseButton(1))
        {
            // Charging with right click hold
            targetColor = chargingColor;
        }
        else if (abilitySystem != null && abilitySystem.IsInAbilityZone())
        {
            // In zone but not charging
            if (abilitySystem.GetCurrentZonePoints() >= abilitySystem.GetMaxPointsPerZone())
            {
                targetColor = insufficientColor; // Zone maxed out
            }
            else
            {
                targetColor = normalColor; // Can charge
            }
        }
        else if (currentPoints >= abilitySystem.consumeAmount)
        {
            // Ready to use
            targetColor = readyColor;
        }
        else if (currentPoints > 0)
        {
            // Has some points but not enough
            targetColor = insufficientColor;
        }
        else
        {
            // Empty
            targetColor = normalColor;
        }
        
        abilityBarFill.color = Color.Lerp(abilityBarFill.color, targetColor, Time.deltaTime * animationSpeed);
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Show/hide UI based on ability zone
        if (abilitySystem != null)
        {
            bool shouldShow = abilitySystem.IsInAbilityZone() || abilitySystem.GetCurrentAbilityPoints() > 0;
            gameObject.SetActive(shouldShow);
        }
    }
}