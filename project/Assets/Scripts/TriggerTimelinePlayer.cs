using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class CinemachineCameraSequencePlayer : MonoBehaviour
{
    [Header("Camera Setup")]
    [Tooltip("The main user-controlled camera to disable")]
    public Camera userCamera;
    
    [Tooltip("First Cinemachine Camera GameObject")]
    public GameObject firstCinemachineCamera;
    
    [Tooltip("Second Cinemachine Camera GameObject")]
    public GameObject secondCinemachineCamera;
    
    [Header("Timeline Settings")]
    [Tooltip("PlayableDirector for the first camera timeline")]
    public PlayableDirector firstPlayableDirector;
    
    [Tooltip("PlayableDirector for the second camera timeline")]
    public PlayableDirector secondPlayableDirector;
    
    [Tooltip("First timeline asset")]
    public TimelineAsset firstTimelineAsset;
    
    [Tooltip("Second timeline asset")]
    public TimelineAsset secondTimelineAsset;
    
    [Header("Trigger Settings")]
    [Tooltip("Tag of the object that can trigger this sequence")]
    public string triggerTag = "Player";
    
    [Tooltip("Start sequence when object enters trigger")]
    public bool playOnEnter = true;
    
    [Tooltip("Only trigger once")]
    public bool triggerOnce = false;
    
    [Header("Sequence Control")]
    [Tooltip("Start time for the first timeline (in seconds)")]
    public double firstStartTime = 0.0;
    
    [Tooltip("Start time for the second timeline (in seconds)")]
    public double secondStartTime = 0.0;
    
    [Tooltip("Delay between first and second camera (seconds)")]
    public float delayBetweenCameras = 0f;
    
    [Tooltip("Delay before re-enabling user camera (seconds)")]
    public float reEnableDelay = 0f;
    
    // Private variables
    private bool hasTriggered = false;
    private bool sequenceInProgress = false;
    private Coroutine sequenceCoroutine;

    void Start()
    {
        // Auto-find user camera if not assigned
        if (userCamera == null)
        {
            userCamera = Camera.main;
            if (userCamera == null)
            {
                userCamera = FindObjectOfType<Camera>();
            }
        }
        
        // Auto-find PlayableDirectors if not assigned
        if (firstPlayableDirector == null || secondPlayableDirector == null)
        {
            PlayableDirector[] directors = FindObjectsOfType<PlayableDirector>();
            
            if (firstPlayableDirector == null && directors.Length > 0)
            {
                firstPlayableDirector = directors[0];
                Debug.Log($"Auto-assigned first PlayableDirector: {firstPlayableDirector.name}", this);
            }
            
            if (secondPlayableDirector == null && directors.Length > 1)
            {
                secondPlayableDirector = directors[1];
                Debug.Log($"Auto-assigned second PlayableDirector: {secondPlayableDirector.name}", this);
            }
        }
        
        // Set timeline assets if provided
        if (firstTimelineAsset != null && firstPlayableDirector != null)
        {
            firstPlayableDirector.playableAsset = firstTimelineAsset;
        }
        
        if (secondTimelineAsset != null && secondPlayableDirector != null)
        {
            secondPlayableDirector.playableAsset = secondTimelineAsset;
        }
        
        // Ensure cameras start in correct state
        InitializeCameraStates();
        
        // Setup trigger collider
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogWarning("No Collider found! This script requires a Collider set as trigger.", this);
        }
    }

    void InitializeCameraStates()
    {
        // Enable user camera by default
        if (userCamera != null)
            userCamera.enabled = true;
        
        // Disable Cinemachine Camera GameObjects by default
        if (firstCinemachineCamera != null)
            firstCinemachineCamera.SetActive(false);
        
        if (secondCinemachineCamera != null)
            secondCinemachineCamera.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if this is the correct object
        if (!IsValidTriggerObject(other))
            return;
        
        // Check if already triggered once
        if (triggerOnce && hasTriggered)
            return;
        
        // Check if sequence is already in progress
        if (sequenceInProgress)
            return;
        
        if (playOnEnter)
        {
            StartCameraSequence();
            hasTriggered = true;
        }
    }
    
    private bool IsValidTriggerObject(Collider other)
    {
        if (!string.IsNullOrEmpty(triggerTag))
        {
            return other.CompareTag(triggerTag);
        }
        return true;
    }
    
    public void StartCameraSequence()
    {
        if (sequenceInProgress)
        {
            Debug.LogWarning("Camera sequence already in progress!", this);
            return;
        }
        
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }
        
        sequenceCoroutine = StartCoroutine(CameraSequenceCoroutine());
    }
    
    private System.Collections.IEnumerator CameraSequenceCoroutine()
    {
        sequenceInProgress = true;
        
        Debug.Log("Starting Cinemachine camera sequence...", this);
        
        // Step 1: Disable user camera
        if (userCamera != null)
        {
            userCamera.enabled = false;
            Debug.Log("User camera disabled", this);
        }
        
        // Step 2: Enable first Cinemachine Camera GameObject and play timeline
        if (firstCinemachineCamera != null)
        {
            firstCinemachineCamera.SetActive(true);
            Debug.Log($"First Cinemachine Camera enabled: {firstCinemachineCamera.name}", this);
        }
        
        if (firstPlayableDirector != null)
        {
            firstPlayableDirector.time = firstStartTime;
            firstPlayableDirector.Play();
            Debug.Log($"First timeline started: {firstPlayableDirector.playableAsset?.name}", this);
            
            // Wait for first timeline to finish
            while (firstPlayableDirector.state == PlayState.Playing)
            {
                yield return null;
            }
            
            Debug.Log("First timeline finished", this);
        }
        
        // Step 3: Disable first Cinemachine Camera
        if (firstCinemachineCamera != null)
        {
            firstCinemachineCamera.SetActive(false);
            Debug.Log($"First Cinemachine Camera disabled: {firstCinemachineCamera.name}", this);
        }
        
        // Optional delay between cameras
        if (delayBetweenCameras > 0)
        {
            yield return new WaitForSeconds(delayBetweenCameras);
        }
        
        // Step 4: Enable second Cinemachine Camera GameObject and play timeline
        if (secondCinemachineCamera != null)
        {
            secondCinemachineCamera.SetActive(true);
            Debug.Log($"Second Cinemachine Camera enabled: {secondCinemachineCamera.name}", this);
        }
        
        if (secondPlayableDirector != null)
        {
            secondPlayableDirector.time = secondStartTime;
            secondPlayableDirector.Play();
            Debug.Log($"Second timeline started: {secondPlayableDirector.playableAsset?.name}", this);
            
            // Wait for second timeline to finish
            while (secondPlayableDirector.state == PlayState.Playing)
            {
                yield return null;
            }
            
            Debug.Log("Second timeline finished", this);
        }
        
        // Step 5: Disable second Cinemachine Camera
        if (secondCinemachineCamera != null)
        {
            secondCinemachineCamera.SetActive(false);
            Debug.Log($"Second Cinemachine Camera disabled: {secondCinemachineCamera.name}", this);
        }
        
        // Step 6: Re-enable user camera
        if (reEnableDelay > 0)
        {
            yield return new WaitForSeconds(reEnableDelay);
        }
        
        if (userCamera != null)
        {
            userCamera.enabled = true;
            Debug.Log("User camera re-enabled", this);
        }
        
        sequenceInProgress = false;
        Debug.Log("Cinemachine camera sequence completed!", this);
    }
    
    public void StopSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
        
        // Stop any playing timelines
        if (firstPlayableDirector != null)
        {
            firstPlayableDirector.Stop();
        }
        
        if (secondPlayableDirector != null)
        {
            secondPlayableDirector.Stop();
        }
        
        // Reset camera states
        InitializeCameraStates();
        
        sequenceInProgress = false;
        Debug.Log("Camera sequence stopped and reset", this);
    }
    
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
    
    // Manual control methods
    public void PlayFirstCinemachineOnly()
    {
        if (firstPlayableDirector != null && firstCinemachineCamera != null)
        {
            // Disable user camera and second cinemachine
            if (userCamera != null) userCamera.enabled = false;
            if (secondCinemachineCamera != null) secondCinemachineCamera.SetActive(false);
            
            // Enable first cinemachine and play
            firstCinemachineCamera.SetActive(true);
            firstPlayableDirector.time = firstStartTime;
            firstPlayableDirector.Play();
            Debug.Log("Playing first Cinemachine camera only", this);
        }
    }
    
    public void PlaySecondCinemachineOnly()
    {
        if (secondPlayableDirector != null && secondCinemachineCamera != null)
        {
            // Disable user camera and first cinemachine
            if (userCamera != null) userCamera.enabled = false;
            if (firstCinemachineCamera != null) firstCinemachineCamera.SetActive(false);
            
            // Enable second cinemachine and play
            secondCinemachineCamera.SetActive(true);
            secondPlayableDirector.time = secondStartTime;
            secondPlayableDirector.Play();
            Debug.Log("Playing second Cinemachine camera only", this);
        }
    }
    
    public void EnableUserCamera()
    {
        if (userCamera != null)
        {
            userCamera.enabled = true;
            
            // Disable Cinemachine cameras
            if (firstCinemachineCamera != null) firstCinemachineCamera.SetActive(false);
            if (secondCinemachineCamera != null) secondCinemachineCamera.SetActive(false);
            
            Debug.Log("User camera enabled, Cinemachine cameras disabled", this);
        }
    }
    
    // Status check methods
    public bool IsSequenceInProgress()
    {
        return sequenceInProgress;
    }
    
    public bool IsFirstTimelinePlaying()
    {
        return firstPlayableDirector != null && firstPlayableDirector.state == PlayState.Playing;
    }
    
    public bool IsSecondTimelinePlaying()
    {
        return secondPlayableDirector != null && secondPlayableDirector.state == PlayState.Playing;
    }
    
    public bool IsUserCameraActive()
    {
        return userCamera != null && userCamera.enabled;
    }
    
    public bool IsFirstCinemachineActive()
    {
        return firstCinemachineCamera != null && firstCinemachineCamera.activeInHierarchy;
    }
    
    public bool IsSecondCinemachineActive()
    {
        return secondCinemachineCamera != null && secondCinemachineCamera.activeInHierarchy;
    }
    
    // Debug methods
    [ContextMenu("Test Start Sequence")]
    public void TestStartSequence()
    {
        StartCameraSequence();
    }
    
    [ContextMenu("Test Stop Sequence")]
    public void TestStopSequence()
    {
        StopSequence();
    }
    
    [ContextMenu("Test Enable User Camera")]
    public void TestEnableUserCamera()
    {
        EnableUserCamera();
    }
    
    [ContextMenu("Test Play First Cinemachine Only")]
    public void TestPlayFirstCinemachineOnly()
    {
        PlayFirstCinemachineOnly();
    }
    
    [ContextMenu("Test Play Second Cinemachine Only")]
    public void TestPlaySecondCinemachineOnly()
    {
        PlaySecondCinemachineOnly();
    }
    
    [ContextMenu("Debug Camera States")]
    public void DebugCameraStates()
    {
        Debug.Log($"User Camera Active: {IsUserCameraActive()}", this);
        Debug.Log($"First Cinemachine Active: {IsFirstCinemachineActive()}", this);
        Debug.Log($"Second Cinemachine Active: {IsSecondCinemachineActive()}", this);
        Debug.Log($"First Timeline Playing: {IsFirstTimelinePlaying()}", this);
        Debug.Log($"Second Timeline Playing: {IsSecondTimelinePlaying()}", this);
        Debug.Log($"Sequence In Progress: {IsSequenceInProgress()}", this);
    }
    
    void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Draw trigger area in yellow
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawWireCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawWireSphere(sphereCol.center, sphereCol.radius);
            }
            
            // Draw inner area to show sequence state
            Gizmos.color = sequenceInProgress ? Color.red : Color.green;
            
            if (col is BoxCollider boxCol2)
            {
                Gizmos.DrawWireCube(boxCol2.center, boxCol2.size * 0.8f);
            }
            else if (col is SphereCollider sphereCol2)
            {
                Gizmos.DrawWireSphere(sphereCol2.center, sphereCol2.radius * 0.8f);
            }
        }
    }
}