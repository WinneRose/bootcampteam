using UnityEngine;

public class AbilityZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public string requiredTag = "Player";
    
    [Header("Visual Feedback")]
    public GameObject zoneEffect;
    public Color zoneColor = Color.cyan;
    public bool showZoneOutline = true;
    
    private void Start()
    {
        // Ensure we have a collider set as trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Setup visual feedback
        if (zoneEffect != null)
        {
            zoneEffect.SetActive(false);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            AbilitySystem abilitySystem = other.GetComponent<AbilitySystem>();
            if (abilitySystem != null)
            {
                abilitySystem.EnterAbilityZone();
                
                // Show visual feedback
                if (zoneEffect != null)
                {
                    zoneEffect.SetActive(true);
                }
                
                Debug.Log($"Player entered ability zone: {gameObject.name}");
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(requiredTag))
        {
            AbilitySystem abilitySystem = other.GetComponent<AbilitySystem>();
            if (abilitySystem != null)
            {
                abilitySystem.ExitAbilityZone();
                
                // Hide visual feedback
                if (zoneEffect != null)
                {
                    zoneEffect.SetActive(false);
                }
                
                Debug.Log($"Player exited ability zone: {gameObject.name}");
            }
        }
    }
    
    // Visual debug in scene view
    private void OnDrawGizmos()
    {
        if (showZoneOutline)
        {
            Gizmos.color = zoneColor;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (showZoneOutline)
        {
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
            Gizmos.DrawCube(transform.position, transform.localScale);
        }
    }
}