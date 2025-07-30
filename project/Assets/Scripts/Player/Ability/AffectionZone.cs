using System;
using System.Collections.Generic;
using UnityEngine;

public class AffectionZone : MonoBehaviour
{
    [Header("Affection Zone Settings")]
    public float zoneSize = 0.2f;
    public bool affectionZone = true;
    public LayerMask affectedLayers = -1; // What layers to affect
    public float detectionRate = 0.1f; // How often to check for objects (seconds)
    
    [Header("Tag Filtering")]
    public List<string> allowedTags = new List<string>(); // Only these tags will be affected

    
    [Header("Timed Activation")]
    public bool useTimedActivation = false; // Enable automatic on/off cycles
    public float activationInterval = 30f; // How often to activate (seconds)
    public float activationDuration = 5f; // How long to stay active (seconds)
    public bool startActive = true; // Should start in active state
    
    [Header("Effects")]
    public ParticleSystem smellPS;
    public AudioClip detectionSound;
    public float audioVolume = 0.5f;
    public Color matColorAffect = Color.red; // Color to apply when affected
    
    [Header("Debug Options")]
    [SerializeField] private Color debugColor = Color.yellow;
    [SerializeField] private bool debug = false;
    [SerializeField] private bool showDetectedObjects = false; // Changed default to false
    
    // Private variables
    private List<GameObject> objectsInZone = new List<GameObject>();
    private List<GameObject> previousObjects = new List<GameObject>();
    private AudioSource audioSource;
    private float lastDetectionTime = 0f;
    
    // Timed activation variables
    private float timedActivationTimer = 0f;
    private bool isTimedActive = true;
    private float nextActivationTime = 0f;
    
    // Events
    public System.Action<GameObject> OnObjectEnterZone;
    public System.Action<GameObject> OnObjectExitZone;
    public System.Action<List<GameObject>> OnObjectsDetected;
    
    private void Start()
    {
        // Setup audio source if needed
        if (detectionSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.volume = audioVolume;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.playOnAwake = false;
        }
        
        // Initialize timed activation
        if (useTimedActivation)
        {
            isTimedActive = startActive;
            if (startActive)
            {
                timedActivationTimer = activationDuration;
                nextActivationTime = 0f;
            }
            else
            {
                timedActivationTimer = 0f;
                nextActivationTime = activationInterval;
            }
        }
        
        // Setup particle system
        UpdateParticleSystem();
    }
    
    private void Update()
    {
        // Handle timed activation
        if (useTimedActivation)
        {
            UpdateTimedActivation();
        }
        
        // Only process detection if zone is active
        if (!IsZoneCurrentlyActive()) return;
        
        // Check for objects at specified rate
        if (Time.time >= lastDetectionTime + detectionRate)
        {
            GetAffectedObjects();
            lastDetectionTime = Time.time;
        }
        
        // Update particle system based on zone state
        UpdateParticleSystem();
    }
    
    public void GetAffectedObjects()
    {
        // Use half extents (zoneSize * 0.5f)
        Vector3 halfExtents = new Vector3(zoneSize, zoneSize, zoneSize) * 0.5f;
        
        Collider[] overlappedColliders = Physics.OverlapBox(
            transform.position, 
            halfExtents,
            transform.rotation,
            affectedLayers,
            QueryTriggerInteraction.UseGlobal
        );
        
        // Store previous objects for comparison
        previousObjects.Clear();
        previousObjects.AddRange(objectsInZone);
        
        // Clear current list
        objectsInZone.Clear();
        
        // Process detected objects
        foreach (Collider collider in overlappedColliders)
        {
            // Skip self
            if (collider.gameObject == gameObject) continue;

            GameObject obj = collider.gameObject;

            // Check if the object's tag is in the allowed list
            if (allowedTags.Count > 0 && !allowedTags.Contains(obj.tag)) continue;

            objectsInZone.Add(obj);

            if (showDetectedObjects)
            {
                Debug.Log($"[AffectionZone] Found object: {obj.name} (Tag: {obj.tag}, Layer: {obj.layer})");
            }

            // Check if this is a new object
            if (!previousObjects.Contains(obj))
            {
                OnObjectEntered(obj);
            }
        }
        
        // Check for objects that left the zone
        foreach (GameObject prevObj in previousObjects)
        {
            if (prevObj != null && !objectsInZone.Contains(prevObj))
            {
                OnObjectExited(prevObj);
            }
        }
        
        // Fire detection event
        OnObjectsDetected?.Invoke(new List<GameObject>(objectsInZone));
    }
    
    private void OnObjectEntered(GameObject obj)
    {
        // Play sound effect
        if (audioSource != null && detectionSound != null)
        {
            audioSource.PlayOneShot(detectionSound);
        }
        
        // Fire event
        OnObjectEnterZone?.Invoke(obj);
        
        // Apply affection effects
        ApplyAffectionEffects(obj);
    }
    
    private void OnObjectExited(GameObject obj)
    {
        // Fire event
        OnObjectExitZone?.Invoke(obj);
        
        // Remove affection effects
        RemoveAffectionEffects(obj);
    }
    
    private void ApplyAffectionEffects(GameObject obj)
    {
        // Change object color (using material instance to avoid affecting prefab)
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Check if we already have an affection effect component
            AffectionEffect effect = obj.GetComponent<AffectionEffect>();
            if (effect == null)
            {
                effect = obj.AddComponent<AffectionEffect>();
                
                // Store original material (not color, the whole material)
                effect.originalMaterial = renderer.material;
                
                // Create a new material instance so we don't affect the prefab
                Material newMaterial = new Material(renderer.material);
                newMaterial.color = matColorAffect; // Use the configurable color
                
                // Apply the new material instance
                renderer.material = newMaterial;
            }
        }
        
        // Apply force to rigidbodies
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = (transform.position - obj.transform.position).normalized;
            rb.AddForce(direction * 2f, ForceMode.Acceleration);
        }
        
        // Add custom behavior
        // obj.SendMessage("OnAffectionZoneEnter", this, SendMessageOptions.DontRequireReceiver);
    }
    
    private void RemoveAffectionEffects(GameObject obj)
    {
        if (obj == null) return;
        
        // Remove material effect and restore original
        AffectionEffect effect = obj.GetComponent<AffectionEffect>();
        if (effect != null)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null && effect.originalMaterial != null)
            {
                // Destroy the temporary material instance to prevent memory leaks
                if (renderer.material != effect.originalMaterial)
                {
                    Material tempMaterial = renderer.material;
                    renderer.material = effect.originalMaterial;
                    
                    // Destroy the temporary material instance
                    if (Application.isPlaying)
                    {
                        Destroy(tempMaterial);
                    }
                    else
                    {
                        DestroyImmediate(tempMaterial);
                    }
                }
            }
            
            // Remove the effect component
            if (Application.isPlaying)
            {
                Destroy(effect);
            }
            else
            {
                DestroyImmediate(effect);
            }
        }
        
        // Remove custom behavior
        // obj.SendMessage("OnAffectionZoneExit", this, SendMessageOptions.DontRequireReceiver);
    }
    
    private void UpdateParticleSystem()
    {
        if (smellPS == null) return;
        
        // Control particle system based on zone state and objects detected
        bool shouldPlay = IsZoneCurrentlyActive() && objectsInZone.Count > 0;
        
        // Special case: if using timed activation, show particles when zone is active even without objects
        if (useTimedActivation && isTimedActive)
        {
            shouldPlay = true; // Always show particles when timed zone is active
        }
        
        if (shouldPlay && !smellPS.isPlaying)
        {
            smellPS.Play();
        }
        else if (!shouldPlay && smellPS.isPlaying)
        {
            smellPS.Stop();
        }
        
        // Adjust particle intensity based on object count (only when objects are detected)
        if (smellPS.isPlaying && objectsInZone.Count > 0)
        {
            var emission = smellPS.emission;
            float baseRate = 10f;
            float rateMultiplier = Mathf.Clamp(objectsInZone.Count, 1, 5);
            emission.rateOverTime = baseRate * rateMultiplier;
        }
        else if (smellPS.isPlaying && useTimedActivation)
        {
            // Default particle rate when timed zone is active but no objects
            var emission = smellPS.emission;
            emission.rateOverTime = 5f; // Lower rate when no objects detected
        }
    }
    
    // Timed activation system
    private void UpdateTimedActivation()
    {
        if (isTimedActive)
        {
            // Zone is currently active - count down activation duration
            timedActivationTimer -= Time.deltaTime;
            
            if (timedActivationTimer <= 0f)
            {
                // Activation period ended - deactivate zone
                DeactivateTimedZone();
            }
        }
        else
        {
            // Zone is currently inactive - count up to next activation
            timedActivationTimer += Time.deltaTime;
            
            if (timedActivationTimer >= nextActivationTime)
            {
                // Time to activate zone
                ActivateTimedZone();
            }
        }
    }
    
    private void ActivateTimedZone()
    {
        isTimedActive = true;
        timedActivationTimer = activationDuration; // Set duration timer
        
        // Play activation sound
        if (audioSource != null && detectionSound != null)
        {
            audioSource.PlayOneShot(detectionSound);
        }
        
        // Immediately update particle system
        UpdateParticleSystem();
    }
    
    private void DeactivateTimedZone()
    {
        isTimedActive = false;
        timedActivationTimer = 0f;
        nextActivationTime = activationInterval; // Set when to activate next
        
        // Remove effects from all current objects
        foreach (GameObject obj in objectsInZone)
        {
            RemoveAffectionEffects(obj);
        }
        objectsInZone.Clear();
        
        // Update particle system (will stop it)
        UpdateParticleSystem();
    }
    
    private bool IsZoneCurrentlyActive()
    {
        if (!affectionZone) return false; // Main switch is off
        
        if (useTimedActivation)
        {
            return isTimedActive; // Use timed activation state
        }
        
        return true; // Always active if not using timed activation
    }
    
    // Public methods
    public void SetZoneActive(bool active)
    {
        affectionZone = active;
        
        if (!active)
        {
            // Clear all objects and remove effects
            foreach (GameObject obj in objectsInZone)
            {
                RemoveAffectionEffects(obj);
            }
            objectsInZone.Clear();
            
            // Stop particles
            if (smellPS != null && smellPS.isPlaying)
            {
                smellPS.Stop();
            }
        }
    }
    
    public void SetTimedActivation(bool enabled)
    {
        useTimedActivation = enabled;
        
        if (enabled)
        {
            // Reset timed activation
            isTimedActive = startActive;
            timedActivationTimer = startActive ? activationDuration : 0f;
            nextActivationTime = startActive ? 0f : activationInterval;
        }
    }
    
    public void SetActivationInterval(float interval)
    {
        activationInterval = interval;
    }
    
    public void SetActivationDuration(float duration)
    {
        activationDuration = duration;
    }
    
    public void ForceActivateNow()
    {
        if (useTimedActivation)
        {
            ActivateTimedZone();
        }
    }
    
    public void ForceDeactivateNow()
    {
        if (useTimedActivation)
        {
            DeactivateTimedZone();
        }
    }
    
    public void SetZoneSize(float newSize)
    {
        zoneSize = newSize;
    }
    
    public List<GameObject> GetObjectsInZone()
    {
        return new List<GameObject>(objectsInZone);
    }
    
    public int GetObjectCount()
    {
        return objectsInZone.Count;
    }
    
    public bool HasObjectsInZone()
    {
        return objectsInZone.Count > 0;
    }
    
    // Timed activation getters
    public bool IsCurrentlyActive()
    {
        return IsZoneCurrentlyActive();
    }
    
    public float GetTimeUntilNextActivation()
    {
        if (!useTimedActivation) return -1f;
        
        if (isTimedActive)
        {
            return timedActivationTimer; // Time remaining in current activation
        }
        else
        {
            return nextActivationTime - timedActivationTimer; // Time until next activation
        }
    }
    
    public string GetTimedActivationStatus()
    {
        if (!useTimedActivation) return "Timed activation disabled";
        
        if (isTimedActive)
        {
            return $"ACTIVE - {timedActivationTimer:F1}s remaining";
        }
        else
        {
            float timeUntilNext = nextActivationTime - timedActivationTimer;
            return $"INACTIVE - {timeUntilNext:F1}s until next activation";
        }
    }
    
    // Context Menu Debug Methods
    [ContextMenu("Debug: Show Zone Status")]
    public void DebugShowZoneStatus()
    {
        Debug.Log("=== AFFECTION ZONE STATUS ===");
        Debug.Log($"Zone Active: {affectionZone}");
        Debug.Log($"Currently Active: {IsZoneCurrentlyActive()}");
        Debug.Log($"Zone Size: {zoneSize}");
        Debug.Log($"Detection Rate: {detectionRate}s");
        Debug.Log($"Objects in Zone: {objectsInZone.Count}");
        Debug.Log($"Allowed Tags: {string.Join(", ", allowedTags)}");
        Debug.Log($"Affected Layers: {affectedLayers.value}");
    }
    
    [ContextMenu("Debug: Show Detected Objects")]
    public void DebugShowDetectedObjects()
    {
        Debug.Log("=== DETECTED OBJECTS ===");
        Debug.Log($"Total Objects: {objectsInZone.Count}");
        
        for (int i = 0; i < objectsInZone.Count; i++)
        {
            GameObject obj = objectsInZone[i];
            if (obj != null)
            {
                Debug.Log($"  {i + 1}. {obj.name} (Tag: {obj.tag}, Layer: {obj.layer})");
                Debug.Log($"     Position: {obj.transform.position}");
                Debug.Log($"     Distance: {Vector3.Distance(transform.position, obj.transform.position):F2}");
            }
            else
            {
                Debug.Log($"  {i + 1}. NULL OBJECT");
            }
        }
    }
    
    [ContextMenu("Debug: Show Timed Activation Status")]
    public void DebugShowTimedActivationStatus()
    {
        Debug.Log("=== TIMED ACTIVATION STATUS ===");
        Debug.Log($"Use Timed Activation: {useTimedActivation}");
        Debug.Log($"Start Active: {startActive}");
        Debug.Log($"Activation Interval: {activationInterval}s");
        Debug.Log($"Activation Duration: {activationDuration}s");
        Debug.Log($"Current Status: {GetTimedActivationStatus()}");
        Debug.Log($"Is Timed Active: {isTimedActive}");
        Debug.Log($"Timer Value: {timedActivationTimer:F2}s");
        Debug.Log($"Next Activation Time: {nextActivationTime:F2}s");
    }
    
    [ContextMenu("Debug: Show Effect Settings")]
    public void DebugShowEffectSettings()
    {
        Debug.Log("=== EFFECT SETTINGS ===");
        Debug.Log($"Particle System: {(smellPS != null ? smellPS.name : "NULL")}");
        Debug.Log($"Detection Sound: {(detectionSound != null ? detectionSound.name : "NULL")}");
        Debug.Log($"Audio Volume: {audioVolume}");
        Debug.Log($"Material Color Effect: {matColorAffect}");
        Debug.Log($"Audio Source: {(audioSource != null ? "Found" : "NULL")}");
        
        if (smellPS != null)
        {
            Debug.Log($"Particle System Playing: {smellPS.isPlaying}");
            var emission = smellPS.emission;
            Debug.Log($"Particle Rate: {emission.rateOverTime.constant}");
        }
    }
    
    [ContextMenu("Debug: Force Detection Now")]
    public void DebugForceDetection()
    {
        Debug.Log("Forcing object detection...");
        GetAffectedObjects();
        Debug.Log($"Detection complete. Found {objectsInZone.Count} objects.");
    }
    
    [ContextMenu("Debug: Force Activate Zone")]
    public void DebugForceActivate()
    {
        if (useTimedActivation)
        {
            ForceActivateNow();
            Debug.Log("Zone force activated!");
        }
        else
        {
            Debug.Log("Timed activation is disabled. Enable it to use force activation.");
        }
    }
    
    [ContextMenu("Debug: Force Deactivate Zone")]
    public void DebugForceDeactivate()
    {
        if (useTimedActivation)
        {
            ForceDeactivateNow();
            Debug.Log("Zone force deactivated!");
        }
        else
        {
            Debug.Log("Timed activation is disabled. Enable it to use force deactivation.");
        }
    }
    
    [ContextMenu("Debug: Reset Timed Activation")]
    public void DebugResetTimedActivation()
    {
        if (useTimedActivation)
        {
            SetTimedActivation(true);
            Debug.Log("Timed activation reset to initial state.");
        }
        else
        {
            Debug.Log("Timed activation is disabled.");
        }
    }
    
    [ContextMenu("Debug: Toggle Zone Active")]
    public void DebugToggleZoneActive()
    {
        SetZoneActive(!affectionZone);
        Debug.Log($"Zone toggled to: {(affectionZone ? "ACTIVE" : "INACTIVE")}");
    }
    
    [ContextMenu("Debug: Clear All Effects")]
    public void DebugClearAllEffects()
    {
        Debug.Log("Clearing all affection effects...");
        
        foreach (GameObject obj in objectsInZone)
        {
            RemoveAffectionEffects(obj);
        }
        
        objectsInZone.Clear();
        previousObjects.Clear();
        
        if (smellPS != null && smellPS.isPlaying)
        {
            smellPS.Stop();
        }
        
        Debug.Log("All effects cleared.");
    }
    
    [ContextMenu("Debug: Test Particle System")]
    public void DebugTestParticleSystem()
    {
        if (smellPS != null)
        {
            if (smellPS.isPlaying)
            {
                smellPS.Stop();
                Debug.Log("Particle system stopped.");
            }
            else
            {
                smellPS.Play();
                Debug.Log("Particle system started.");
            }
        }
        else
        {
            Debug.Log("No particle system assigned!");
        }
    }
    
    [ContextMenu("Debug: Test Detection Sound")]
    public void DebugTestDetectionSound()
    {
        if (audioSource != null && detectionSound != null)
        {
            audioSource.PlayOneShot(detectionSound);
            Debug.Log("Detection sound played.");
        }
        else
        {
            Debug.Log($"Cannot play sound - AudioSource: {(audioSource != null ? "OK" : "NULL")}, Sound: {(detectionSound != null ? "OK" : "NULL")}");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!debug) return;
        
        // Change gizmo color based on current state
        Color currentGizmoColor = debugColor;
        if (useTimedActivation && Application.isPlaying)
        {
            currentGizmoColor = isTimedActive ? Color.green : Color.red;
        }
        else if (!IsZoneCurrentlyActive() && Application.isPlaying)
        {
            currentGizmoColor = Color.gray;
        }
        
        // Draw zone bounds
        Gizmos.color = currentGizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(zoneSize, zoneSize, zoneSize));
        
        // Draw detected objects
        if (Application.isPlaying && objectsInZone.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (GameObject obj in objectsInZone)
            {
                if (obj != null)
                {
                    Gizmos.DrawLine(transform.position, obj.transform.position);
                    Gizmos.DrawWireSphere(obj.transform.position, 0.2f);
                }
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!debug) return;
        
        // Draw filled cube when selected
        Color fillColor = debugColor;
        if (useTimedActivation && Application.isPlaying)
        {
            fillColor = isTimedActive ? Color.green : Color.red;
        }
        fillColor.a = 0.2f;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(transform.position, new Vector3(zoneSize, zoneSize, zoneSize));
        
        // Draw info text
        #if UNITY_EDITOR
        if (Application.isPlaying)
        {
            string statusText = $"Objects: {objectsInZone.Count}";
            if (useTimedActivation)
            {
                statusText += $"\n{GetTimedActivationStatus()}";
            }
            
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (zoneSize * 0.6f), 
                statusText
            );
        }
        #endif
    }
    
    private void OnDestroy()
    {
        // Clean up effects on all objects
        foreach (GameObject obj in objectsInZone)
        {
            RemoveAffectionEffects(obj);
        }
    }
}

// Helper component to store original object state
public class AffectionEffect : MonoBehaviour
{
    [System.Obsolete("Use originalMaterial instead")]
    public Color originalColor; // Keep for backward compatibility
    
    public Material originalMaterial; // Store the complete original material
    
    // Add other original states here if needed
    public Vector3 originalScale;
    public float originalSpeed;
}