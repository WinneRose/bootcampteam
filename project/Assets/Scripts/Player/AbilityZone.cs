using UnityEngine;

public class AbilityZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("The tag required on the object entering the zone, e.g., 'Player'.")]
    public string requiredTag = "Player";
    
    [Header("Visual Feedback")]
    [Tooltip("An optional particle effect or light to activate when a player is in the zone.")]
    public GameObject zoneEffect;
    public Color zoneColor = Color.cyan;
    public bool showZoneOutline = true;
    
    private void Start()
    {
        // Ensure we have a collider and it's set as a trigger.
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("AbilityZone requires a Collider component on the same GameObject.", this);
            return;
        }
        col.isTrigger = true;
        
        // Setup visual feedback, ensuring it's off by default.
        if (zoneEffect != null)
        {
            zoneEffect.SetActive(false);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // First, check if the entering object has the required tag.
        if (other.CompareTag(requiredTag))
        {
            // Next, try to get the DewAbilitySystem component from the object.
            DewAbilitySystem dewAbility = other.GetComponent<DewAbilitySystem>();

            // <--- FIX: Check if the component was actually found before trying to use it.
            if (dewAbility != null)
            {
                // If it was found, call the function.
                //dewAbility.EnterAbilityZone();

                // Activate the visual effect
                if (zoneEffect != null)
                {
                    zoneEffect.SetActive(true);
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // First, check if the exiting object has the required tag.
        if (other.CompareTag(requiredTag))
        {
            // Next, try to get the DewAbilitySystem component from the object.
            DewAbilitySystem dewAbility =  other.GetComponent<DewAbilitySystem>();

            // <--- FIX: Also add a check here for safety.
            if (dewAbility != null)
            {
                // If it was found, call the function.
                //dewAbility.ExitAbilityZone();

                // Deactivate the visual effect
                if (zoneEffect != null)
                {
                    zoneEffect.SetActive(false);
                }
            }
        }
    }
    
    // The rest of your Gizmo code is fine and helpful for debugging!
    #region Gizmos
    private void OnDrawGizmos()
    {
        if (showZoneOutline)
        {
            Gizmos.color = zoneColor;
            // Use the collider's bounds for a more accurate gizmo
            if(GetComponent<Collider>() != null)
            {
                Gizmos.DrawWireCube(GetComponent<Collider>().bounds.center, GetComponent<Collider>().bounds.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, transform.localScale);
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (showZoneOutline)
        {
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
            if(GetComponent<Collider>() != null)
            {
                Gizmos.DrawCube(GetComponent<Collider>().bounds.center, GetComponent<Collider>().bounds.size);
            }
            else
            {
                Gizmos.DrawCube(transform.position, transform.localScale);
            }
        }
    }
    #endregion
}