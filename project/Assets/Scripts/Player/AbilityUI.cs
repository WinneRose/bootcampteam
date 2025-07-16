using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider abilityBar;
    public TextMeshProUGUI abilityText;
    public Image abilityBarFill;
    public TextMeshProUGUI zoneInfoText;
    public GameObject chargingIndicator;
    
    [Header("Visual Settings")]
    public Color normalColor = Color.blue;
    public Color chargingColor = Color.yellow;
    public Color readyColor = Color.green;
    public Color insufficientColor = Color.red;
    public Color maxedOutColor = Color.orange;
    
    [Header("Animation")]
    public float animationSpeed = 2f;
    public float pulseIntensity = 0.3f;
    
    [Header("Auto Hide Settings")]
    public bool autoHideUI = true;
    public float hideDelay = 3f;
    
    private AbilitySystem abilitySystem;
    private bool isInitialized = false;
    private bool isCurrentlyCharging = false;
    private float lastActivityTime = 0f;
    private CanvasGroup canvasGroup;
    
    private void Awake()
    {
        // Get or add CanvasGroup for fading
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
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
        lastActivityTime = Time.time;
    }
    
    public void UpdateAbilityBar(float currentPoints, float maxPoints)
    {
        if (!isInitialized) return;
        
        lastActivityTime = Time.time;
        
        // Update slider value smoothly
        if (abilityBar != null)
        {
            abilityBar.value = Mathf.Lerp(abilityBar.value, currentPoints, Time.deltaTime * animationSpeed);
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
        isCurrentlyCharging = isCharging;
        
        if (isCharging)
            lastActivityTime = Time.time;
        
        if (chargingIndicator != null)
        {
            chargingIndicator.SetActive(isCharging);
        }
    }
    
    public void UpdateZoneInfo(float currentZonePoints, float maxZonePoints, bool inZone)
    {
        if (zoneInfoText != null)
        {
            if (inZone)
            {
                if (currentZonePoints >= maxZonePoints)
                {
                    zoneInfoText.text = $"Zone: MAXED ({currentZonePoints:F0}/{maxZonePoints:F0})";
                    zoneInfoText.color = maxedOutColor;
                }
                else
                {
                    zoneInfoText.text = $"Zone: {currentZonePoints:F0}/{maxZonePoints:F0}";
                    zoneInfoText.color = chargingColor;
                }
                lastActivityTime = Time.time;
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
        if (abilityBarFill == null || abilitySystem == null) return;
        
        Color targetColor = GetTargetColor(currentPoints);
        
        // Apply pulsing effect when charging
        if (isCurrentlyCharging)
        {
            float pulse = Mathf.Sin(Time.time * animationSpeed) * pulseIntensity + (1f - pulseIntensity);
            Color pulsedColor = targetColor * pulse;
            pulsedColor.a = targetColor.a; // Preserve alpha
            abilityBarFill.color = pulsedColor;
        }
        else
        {
            // Smooth color transition when not charging
            abilityBarFill.color = Color.Lerp(abilityBarFill.color, targetColor, Time.deltaTime * animationSpeed);
        }
    }
    
    private Color GetTargetColor(float currentPoints)
    {
        if (abilitySystem == null) return normalColor;
        
        // Priority order for color determination
        if (isCurrentlyCharging && abilitySystem.IsInAbilityZone())
        {
            return chargingColor;
        }
        else if (abilitySystem.IsInAbilityZone())
        {
            if (abilitySystem.GetCurrentZonePoints() >= abilitySystem.GetMaxPointsPerZone())
            {
                return maxedOutColor; // Zone maxed out
            }
            else
            {
                return normalColor; // Can charge
            }
        }
        else if (abilitySystem.CanUseAbility())
        {
            return readyColor; // Ready to use
        }
        else if (currentPoints > 0)
        {
            return insufficientColor; // Has some points but not enough
        }
        else
        {
            return normalColor; // Empty
        }
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        HandleVisibility();
    }
    
    private void HandleVisibility()
    {
        if (abilitySystem == null || canvasGroup == null) return;
        
        bool shouldShow = ShouldShowUI();
        float targetAlpha = shouldShow ? 1f : 0f;
        
        // Smooth fade in/out
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * animationSpeed);
        
        // Disable interaction when fully transparent
        canvasGroup.interactable = canvasGroup.alpha > 0.1f;
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.1f;
    }
    
    private bool ShouldShowUI()
    {
        if (!autoHideUI) return true;
        
        // Always show when in ability zone or charging
        if (abilitySystem.IsInAbilityZone() || abilitySystem.IsCharging())
            return true;
        
        // Show if has ability points
        if (abilitySystem.GetCurrentAbilityPoints() > 0)
            return true;
        
        // Show for a short time after activity
        if (Time.time - lastActivityTime < hideDelay)
            return true;
        
        return false;
    }
    
    // Public methods for external control
    public void ForceShow()
    {
        lastActivityTime = Time.time;
    }
    
    public void SetAutoHide(bool enabled)
    {
        autoHideUI = enabled;
        if (!enabled)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}