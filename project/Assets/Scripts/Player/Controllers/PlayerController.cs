using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private Rigidbody _rb;
    private Animator _animator;
    private AudioSource _audioSource;

    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    public float acceleration = 10f;

    [Header("Run Threshold")]
    public float runHoldTime = 2f;
    private float holdTimer = 0f;
    private bool isRunning = false;

    [Header("Animation Blend")]
    public float animationSmoothTime = 0.1f;
    private float currentAnimSpeed;
    private float animVelocity;

    [Header("Look Settings")]
    public float mouseSensitivity = 0.1f;

    [Header("Jump Settings")]
    public float jumpForce = 7f;
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayer = -1;
    [SerializeField] private bool isGrounded = false;
    
    [Header("Ground Check Debug")]
    public bool showGroundCheckGizmos = true;
    private Vector3 groundCheckOrigin;
    private Vector3 sphereCheckPosition;
    [SerializeField] private float capsuleRadius = 0.5f;

    [Header("Respawn Settings")]
    [Tooltip("Reference to the spawn point object where player will respawn")]
    public Transform spawnPoint;
    
    [Tooltip("Name of the spawn point GameObject to find automatically")]
    public string spawnPointName = "SpawnPoint";
    
    [Tooltip("Tags that trigger respawn when collided with")]
    public string[] respawnTriggerTags = { "Lake", "FallZone" };
    
    [Tooltip("Height offset above spawn point when respawning")]
    public float respawnHeightOffset = 0.5f;
    
    [Tooltip("Clear velocity when respawning")]
    public bool clearVelocityOnRespawn = true;

    [Header("Voice Effects")]
    [Tooltip("Jump voice clips (will play randomly)")]
    public AudioClip[] jumpVoiceClips;
    
    [Tooltip("Movement voice clips for walking (will play randomly during movement)")]
    public AudioClip[] walkVoiceClips;
    
    [Tooltip("Movement voice clips for running (will play randomly during running)")]
    public AudioClip[] runVoiceClips;
    
    [Tooltip("Landing voice clips (when landing from jump)")]
    public AudioClip[] landingVoiceClips;
    
    [Header("Voice Settings")]
    [Range(0f, 1f)]
    public float voiceVolume = 0.7f;
    
    [Tooltip("Chance to play walk voice per movement check")]
    [Range(0f, 1f)]
    public float walkVoiceChance = 0.1f;
    
    [Tooltip("Chance to play run voice per movement check (higher for more frequent)")]
    [Range(0f, 1f)]
    public float runVoiceChance = 0.3f;
    
    [Tooltip("Cooldown between walk voice clips")]
    public float walkVoiceCooldown = 3f;
    
    [Tooltip("Cooldown between run voice clips (shorter for more frequent)")]
    public float runVoiceCooldown = 1f;
    
    [Tooltip("Cooldown between jump voice clips")]
    public float jumpVoiceCooldown = 1f;
    
    [Tooltip("Minimum fall velocity to trigger landing sound")]
    public float minLandingVelocity = -3f;

    // Anti-flicker input caching
    private Vector3 lastInput;
    private float lastInputTime;
    private float inputStabilityTime = 0.05f;

    // Voice effect tracking
    private float lastWalkVoiceTime;
    private float lastRunVoiceTime;
    private float lastJumpVoiceTime;
    private bool wasGroundedLastFrame = true;
    private bool isCurrentlyMoving = false;
    private bool wasMovingLastFrame = false;
    private float lastVerticalVelocity = 0f;
    private bool wasFalling = false;

    void Start()
    {
        // Get capsule radius FIRST for all clients
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsuleRadius = capsule.radius;
        }

        // Get or add AudioSource for voice effects
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource for voice effects
        _audioSource.volume = voiceVolume;
        _audioSource.pitch = 1f;
        _audioSource.spatialBlend = 1f; // 3D sound
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 20f;

        // Find spawn point if not assigned
        if (spawnPoint == null)
        {
            // First try to find by name
            if (!string.IsNullOrEmpty(spawnPointName))
            {
                GameObject spawnPointObj = GameObject.Find(spawnPointName);
                if (spawnPointObj != null)
                {
                    spawnPoint = spawnPointObj.transform;
                    Debug.Log($"Found spawn point by name: {spawnPointName}");
                }
                else
                {
                    Debug.LogWarning($"No GameObject found with name '{spawnPointName}' for {gameObject.name}");
                    
                    // Fallback: try to find by tag
                    spawnPointObj = GameObject.FindGameObjectWithTag("SpawnPoint");
                    if (spawnPointObj != null)
                    {
                        spawnPoint = spawnPointObj.transform;
                        Debug.Log($"Found spawn point by tag 'SpawnPoint' as fallback");
                    }
                    else
                    {
                        Debug.LogError($"No spawn point found by name '{spawnPointName}' or tag 'SpawnPoint' for {gameObject.name}. Respawn will use current position.");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Spawn point name is empty. Please assign a spawn point or set the spawn point name for {gameObject.name}");
            }
        }

        // Only setup for owner
        if (!IsOwner)
        {
            return; 
        }

        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Cursor settings only for owner
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
        // Ground check runs for ALL clients
        GroundCheck();

        // Movement only for owner
        if (!IsOwner) return;

        if (Time.fixedTime - lastInputTime < inputStabilityTime)
        {
            ApplyMovement(lastInput);
        }

        // Handle voice effects
        HandleVoiceEffects();
    }

    #region Collision and Respawn

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        // Check if the collided object has a respawn trigger tag
        foreach (string triggerTag in respawnTriggerTags)
        {
            if (other.CompareTag(triggerTag))
            {
                Debug.Log($"Player {gameObject.name} touched {triggerTag}, respawning...");
                RespawnToSpawnPoint();
                return;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner) return;

        // Check if the collided object has a respawn trigger tag
        foreach (string triggerTag in respawnTriggerTags)
        {
            if (collision.gameObject.CompareTag(triggerTag))
            {
                Debug.Log($"Player {gameObject.name} collided with {triggerTag}, respawning...");
                RespawnToSpawnPoint();
                return;
            }
        }
    }

    private void RespawnToSpawnPoint()
    {
        if (spawnPoint != null)
        {
            Vector3 respawnPosition = spawnPoint.position + Vector3.up * respawnHeightOffset;
            
            // Network the respawn to all clients
            RespawnPlayerServerRpc(respawnPosition, spawnPoint.rotation);
        }
        else
        {
            Debug.LogError($"No spawn point available for {gameObject.name}! Cannot respawn.");
        }
    }

    [ServerRpc]
    private void RespawnPlayerServerRpc(Vector3 position, Quaternion rotation)
    {
        // Move the player on the server
        transform.position = position;
        transform.rotation = rotation;

        if (clearVelocityOnRespawn && _rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Update all clients
        RespawnPlayerClientRpc(position, rotation);
    }

    [ClientRpc]
    private void RespawnPlayerClientRpc(Vector3 position, Quaternion rotation)
    {
        // Update position on all clients
        transform.position = position;
        transform.rotation = rotation;

        if (clearVelocityOnRespawn && _rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Reset movement state
        if (IsOwner)
        {
            holdTimer = 0f;
            isRunning = false;
            currentAnimSpeed = 0f;
            
            if (_animator != null)
            {
                _animator.SetFloat("speed", 0f);
            }
        }
    }

    [ContextMenu("Debug Voice Settings")]
    public void DebugVoiceSettings()
    {
        Debug.Log($"=== Voice Settings Debug for {gameObject.name} ===");
        Debug.Log($"IsOwner: {IsOwner}");
        Debug.Log($"AudioSource: {(_audioSource != null ? "Found" : "NULL")}");
        Debug.Log($"Jump clips: {(jumpVoiceClips?.Length ?? 0)}");
        Debug.Log($"Walk clips: {(walkVoiceClips?.Length ?? 0)}");
        Debug.Log($"Run clips: {(runVoiceClips?.Length ?? 0)}");
        Debug.Log($"Landing clips: {(landingVoiceClips?.Length ?? 0)}");
        Debug.Log($"Voice volume: {voiceVolume}");
    }

    #endregion

    private void HandleVoiceEffects()
    {
        if (!IsOwner) return;

        // Track falling state for better landing detection
        float currentVerticalVelocity = _rb.linearVelocity.y;
        bool isFalling = currentVerticalVelocity < minLandingVelocity && !isGrounded;
        
        // Check for landing (was falling with significant velocity, now grounded)
        if (wasFalling && isGrounded && lastVerticalVelocity < minLandingVelocity)
        {
            PlayLandingVoice();
        }

        // Check for movement state changes
        if (isCurrentlyMoving && !wasMovingLastFrame)
        {
            // Started moving
            PlayMovementVoice();
        }

        // Update tracking variables
        wasGroundedLastFrame = isGrounded;
        wasMovingLastFrame = isCurrentlyMoving;
        wasFalling = isFalling;
        lastVerticalVelocity = currentVerticalVelocity;
    }

    private void GroundCheck()
    {
        groundCheckOrigin = transform.position;
        
        float capsuleBottom = capsuleRadius;
        sphereCheckPosition = groundCheckOrigin - Vector3.up * capsuleBottom - Vector3.up * groundCheckDistance;
        
        bool previousGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(sphereCheckPosition, capsuleRadius, groundLayer, QueryTriggerInteraction.Ignore);

        if (showGroundCheckGizmos)
        {
            Debug.DrawLine(groundCheckOrigin, sphereCheckPosition, isGrounded ? Color.green : Color.red);
        }
    }

    public void Move(Vector3 input)
    {
        if (!IsOwner) return;
        lastInput = input;
        lastInputTime = Time.time;
    }

    public void Jump()
    {
        if (!IsOwner) return;
        if (isGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (_animator != null)
                _animator.SetTrigger("jump");

            // Play jump voice effect
            PlayJumpVoice();
        }
    }

    private void ApplyMovement(Vector3 input)
    {
        float inputMagnitude = input.magnitude;
        bool isHolding = inputMagnitude > 0.1f;
        
        // Update current movement state
        isCurrentlyMoving = isHolding;

        if (isHolding)
            holdTimer += Time.fixedDeltaTime;
        else
        {
            holdTimer = 0f;
            isRunning = false;
        }

        if (holdTimer >= runHoldTime)
            isRunning = true;

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        float smoothedMagnitude = Mathf.Clamp01(inputMagnitude);

        Vector3 moveDir = (transform.forward * input.z + transform.right * input.x).normalized;
        Vector3 desiredVelocity = moveDir * targetSpeed * smoothedMagnitude;

        Vector3 currentHorizontalVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 velocityChange = desiredVelocity - currentHorizontalVelocity;
        velocityChange = Vector3.ClampMagnitude(velocityChange, acceleration * Time.fixedDeltaTime);

        _rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // Animate
        float targetAnimSpeed = isHolding ? (isRunning ? 1f : 0.3f) : 0f;
        currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, targetAnimSpeed, ref animVelocity, animationSmoothTime);

        if (_animator != null)
            _animator.SetFloat("speed", currentAnimSpeed);

        // Play movement voice effects periodically while moving
        if (isHolding && isGrounded)
        {
            if (isRunning && CanPlayRunVoice())
            {
                if (Random.value < runVoiceChance)
                {
                    PlayRunVoice();
                }
            }
            else if (!isRunning && CanPlayWalkVoice())
            {
                if (Random.value < walkVoiceChance)
                {
                    PlayWalkVoice();
                }
            }
        }
    }

    public void Look(Vector3 lookInput)
    {
        if (!IsOwner) return;
        float yawDelta = lookInput.x * mouseSensitivity;
        Quaternion deltaRotation = Quaternion.Euler(0f, yawDelta, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    #region Voice Effects Methods

    private void PlayJumpVoice()
    {
        if (!CanPlayJumpVoice() || jumpVoiceClips == null || jumpVoiceClips.Length == 0)
            return;

        AudioClip randomClip = GetRandomClip(jumpVoiceClips);
        if (randomClip != null)
        {
            PlayVoiceEffectClientRpc(VoiceEffectType.Jump, GetClipIndex(jumpVoiceClips, randomClip));
            lastJumpVoiceTime = Time.time;
        }
    }

    private void PlayMovementVoice()
    {
        if (isRunning)
        {
            PlayRunVoice();
        }
        else
        {
            PlayWalkVoice();
        }
    }

    private void PlayWalkVoice()
    {
        if (!CanPlayWalkVoice() || walkVoiceClips == null || walkVoiceClips.Length == 0)
            return;

        AudioClip randomClip = GetRandomClip(walkVoiceClips);
        if (randomClip != null)
        {
            PlayVoiceEffectClientRpc(VoiceEffectType.Walk, GetClipIndex(walkVoiceClips, randomClip));
            lastWalkVoiceTime = Time.time;
        }
    }

    private void PlayRunVoice()
    {
        if (!CanPlayRunVoice() || runVoiceClips == null || runVoiceClips.Length == 0)
            return;

        AudioClip randomClip = GetRandomClip(runVoiceClips);
        if (randomClip != null)
        {
            PlayVoiceEffectClientRpc(VoiceEffectType.Run, GetClipIndex(runVoiceClips, randomClip));
            lastRunVoiceTime = Time.time;
        }
    }

    private void PlayLandingVoice()
    {
        if (landingVoiceClips == null || landingVoiceClips.Length == 0)
            return;

        AudioClip randomClip = GetRandomClip(landingVoiceClips);
        if (randomClip != null)
        {
            PlayVoiceEffectClientRpc(VoiceEffectType.Landing, GetClipIndex(landingVoiceClips, randomClip));
        }
    }

    [ClientRpc]
    private void PlayVoiceEffectClientRpc(VoiceEffectType effectType, int clipIndex)
    {
        if (_audioSource == null) return;

        AudioClip clipToPlay = null;
        
        switch (effectType)
        {
            case VoiceEffectType.Jump:
                if (jumpVoiceClips != null && clipIndex < jumpVoiceClips.Length)
                    clipToPlay = jumpVoiceClips[clipIndex];
                break;
            case VoiceEffectType.Walk:
                if (walkVoiceClips != null && clipIndex < walkVoiceClips.Length)
                    clipToPlay = walkVoiceClips[clipIndex];
                break;
            case VoiceEffectType.Run:
                if (runVoiceClips != null && clipIndex < runVoiceClips.Length)
                    clipToPlay = runVoiceClips[clipIndex];
                break;
            case VoiceEffectType.Landing:
                if (landingVoiceClips != null && clipIndex < landingVoiceClips.Length)
                    clipToPlay = landingVoiceClips[clipIndex];
                break;
        }

        if (clipToPlay != null)
        {
            _audioSource.pitch = Random.Range(0.9f, 1.1f); // Slight pitch variation
            _audioSource.PlayOneShot(clipToPlay, voiceVolume);
        }
    }

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;
        
        return clips[Random.Range(0, clips.Length)];
    }

    private int GetClipIndex(AudioClip[] clips, AudioClip targetClip)
    {
        if (clips == null || targetClip == null)
            return 0;
        
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == targetClip)
                return i;
        }
        return 0;
    }

    private bool CanPlayJumpVoice()
    {
        return Time.time - lastJumpVoiceTime >= jumpVoiceCooldown;
    }

    private bool CanPlayWalkVoice()
    {
        return Time.time - lastWalkVoiceTime >= walkVoiceCooldown;
    }

    private bool CanPlayRunVoice()
    {
        return Time.time - lastRunVoiceTime >= runVoiceCooldown;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Jump Voice")]
    public void TestJumpVoice()
    {
        if (IsOwner)
        {
            PlayJumpVoice();
        }
        else
        {
            Debug.LogWarning("Only owner can test voice effects!");
        }
    }

    [ContextMenu("Test Walk Voice")]
    public void TestWalkVoice()
    {
        if (IsOwner)
        {
            isRunning = false;
            PlayMovementVoice();
        }
        else
        {
            Debug.LogWarning("Only owner can test voice effects!");
        }
    }

    [ContextMenu("Test Run Voice")]
    public void TestRunVoice()
    {
        if (IsOwner)
        {
            isRunning = true;
            PlayMovementVoice();
        }
        else
        {
            Debug.LogWarning("Only owner can test voice effects!");
        }
    }

    [ContextMenu("Test Landing Voice")]
    public void TestLandingVoice()
    {
        if (IsOwner)
        {
            PlayLandingVoice();
        }
        else
        {
            Debug.LogWarning("Only owner can test voice effects!");
        }
    }

    [ContextMenu("Test Respawn")]
    public void TestRespawn()
    {
        if (IsOwner)
        {
            RespawnToSpawnPoint();
        }
        else
        {
            Debug.LogWarning("Only owner can test respawn!");
        }
    }

    [ContextMenu("Debug Respawn Settings")]
    public void DebugRespawnSettings()
    {
        Debug.Log($"=== Respawn Settings Debug for {gameObject.name} ===");
        Debug.Log($"Spawn Point Reference: {(spawnPoint != null ? spawnPoint.name : "NULL")}");
        Debug.Log($"Spawn Point Name to Find: '{spawnPointName}'");
        Debug.Log($"Respawn Trigger Tags: [{string.Join(", ", respawnTriggerTags)}]");
        Debug.Log($"Respawn Height Offset: {respawnHeightOffset}");
        Debug.Log($"Clear Velocity on Respawn: {clearVelocityOnRespawn}");
        
        // Test finding spawn point
        if (spawnPoint == null && !string.IsNullOrEmpty(spawnPointName))
        {
            GameObject testFind = GameObject.Find(spawnPointName);
            Debug.Log($"Test Find Result: {(testFind != null ? $"Found '{testFind.name}'" : "Not Found")}");
        }
    }

    #endregion

    void OnDrawGizmos()
    {
        if (showGroundCheckGizmos && Application.isPlaying)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(sphereCheckPosition, capsuleRadius * 0.9f);
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(groundCheckOrigin, 0.1f);
            
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheckOrigin, sphereCheckPosition);
        }
    }
}

public enum VoiceEffectType
{
    Jump,
    Walk,
    Run,
    Landing
}