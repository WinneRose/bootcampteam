using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private Rigidbody _rb;
    private Animator _animator;

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
    
    // Network synchronized states
    private NetworkVariable<bool> networkIsGrounded = new NetworkVariable<bool>(false, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(Vector3.zero,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>(Vector3.zero,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    [SerializeField] private bool localIsGrounded = false;
    
    [Header("Ground Check Debug")]
    public bool showGroundCheckGizmos = true;
    private Vector3 groundCheckOrigin;
    private Vector3 sphereCheckPosition;
    [SerializeField] private float capsuleRadius = 0.5f;

    // Anti-flicker input caching
    private Vector3 lastInput;
    private float lastInputTime;
    private float inputStabilityTime = 0.05f;

    // Client prediction and reconciliation
    private Vector3 predictedPosition;
    private bool usePrediction = true;
    private float reconciliationThreshold = 0.1f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkIsGrounded.OnValueChanged += OnGroundedStateChanged;
        networkPosition.OnValueChanged += OnNetworkPositionChanged;
        
        if (IsOwner)
        {
            networkPosition.Value = transform.position;
            predictedPosition = transform.position;
        }
    }

    void Start()
    {
        // Get capsule radius for all clients
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsuleRadius = capsule.radius;
        }

        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();

        if (IsOwner)
        {
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Cursor settings only for local player
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Non-owners: disable physics but keep collider for ground checking
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
    }

    void FixedUpdate()
    {
        if (IsOwner)
        {
            // Owner: Authoritative physics and ground check
            PerformOwnerUpdate();
        }
        else
        {
            // Non-owners: Use network position for ground checking
            PerformClientUpdate();
        }
    }

    private void PerformOwnerUpdate()
    {
        // Standard ground check at current position
        PerformGroundCheckAtPosition(transform.position);
        
        // Update network variables
        if (Vector3.Distance(networkPosition.Value, transform.position) > 0.01f)
        {
            networkPosition.Value = transform.position;
        }
        
        if (Vector3.Distance(networkVelocity.Value, _rb.linearVelocity) > 0.01f)
        {
            networkVelocity.Value = _rb.linearVelocity;
        }
        
        if (networkIsGrounded.Value != localIsGrounded)
        {
            networkIsGrounded.Value = localIsGrounded;
        }

        // Handle movement input
        if (Time.fixedTime - lastInputTime < inputStabilityTime)
        {
            ApplyMovement(lastInput);
        }
    }

    private void PerformClientUpdate()
    {
        // Use network position for ground checking to match server state
        Vector3 checkPosition = networkPosition.Value;
        
        // If we have velocity info, predict slightly ahead for better responsiveness
        if (usePrediction && networkVelocity.Value.magnitude > 0.1f)
        {
            float predictionTime = Time.fixedDeltaTime * 2f; // Predict 2 frames ahead
            checkPosition += networkVelocity.Value * predictionTime;
        }
        
        // Perform ground check at the network/predicted position
        PerformGroundCheckAtPosition(checkPosition);
        
        // Smoothly interpolate visual position
        InterpolateToNetworkPosition();
    }

    private void PerformGroundCheckAtPosition(Vector3 position)
    {
        groundCheckOrigin = position;
        float capsuleBottom = capsuleRadius;
        sphereCheckPosition = groundCheckOrigin - Vector3.up * capsuleBottom - Vector3.up * groundCheckDistance;

        bool previousGrounded = localIsGrounded;
        localIsGrounded = Physics.CheckSphere(sphereCheckPosition, capsuleRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // Debug logging for state changes
        if (previousGrounded != localIsGrounded)
        {
            string clientType = IsOwner ? "OWNER" : "CLIENT";
            Debug.Log($"[{clientType}] Ground state changed: {previousGrounded} -> {localIsGrounded} at position {position}");
            
            // Additional debug info for clients
            if (!IsOwner)
            {
                Debug.Log($"[CLIENT] Network position: {networkPosition.Value}, Check position: {position}");
                Debug.Log($"[CLIENT] Network grounded: {networkIsGrounded.Value}, Local grounded: {localIsGrounded}");
            }
        }

        // Enhanced debugging - check what we're hitting
        if (!localIsGrounded)
        {
            // Check if there's something there but on wrong layer
            Collider[] hits = Physics.OverlapSphere(sphereCheckPosition, capsuleRadius, -1, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0)
            {
                string clientType = IsOwner ? "OWNER" : "CLIENT";
                Debug.Log($"[{clientType}] Found {hits.Length} colliders but not grounded:");
                foreach (Collider hit in hits)
                {
                    if (hit.gameObject != gameObject) // Don't count self
                    {
                        bool inGroundLayer = ((1 << hit.gameObject.layer) & groundLayer) != 0;
                        Debug.Log($"  - {hit.name} on layer {hit.gameObject.layer} ({LayerMask.LayerToName(hit.gameObject.layer)}) - In ground layer: {inGroundLayer}");
                    }
                }
            }
        }
    }

    private void InterpolateToNetworkPosition()
    {
        Vector3 targetPosition = networkPosition.Value;
        float distance = Vector3.Distance(transform.position, targetPosition);
        
        // Only interpolate if the distance is reasonable (not a teleport)
        if (distance < reconciliationThreshold)
        {
            float lerpRate = 15f * Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPosition, lerpRate);
        }
        else
        {
            // Large distance - snap to network position
            transform.position = targetPosition;
            Debug.Log($"[CLIENT] Snapped to network position. Distance was: {distance}");
        }
    }

    private void OnNetworkPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsOwner)
        {
            // Check for prediction accuracy
            if (usePrediction)
            {
                float predictionError = Vector3.Distance(predictedPosition, newValue);
                if (predictionError > reconciliationThreshold)
                {
                    Debug.Log($"[CLIENT] Prediction error: {predictionError}. Reconciling.");
                }
            }
            predictedPosition = newValue;
        }
    }

    private void OnGroundedStateChanged(bool previousValue, bool newValue)
    {
        if (!IsOwner)
        {
            Debug.Log($"[CLIENT] Network ground state changed: {previousValue} -> {newValue}");
            
            // Force a ground check update when network state changes
            PerformGroundCheckAtPosition(networkPosition.Value);
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
        
        // Use local ground check for immediate response
        bool canJumpLocal = localIsGrounded;
        bool canJumpNetwork = networkIsGrounded.Value;
        
        Debug.Log($"[JUMP] Local grounded: {canJumpLocal}, Network grounded: {canJumpNetwork}");
        
        // Allow jump if locally grounded (for responsiveness)
        if (canJumpLocal)
        {
            PerformJump();
        }
        else
        {
            Debug.Log($"Cannot jump - not grounded locally. Position: {transform.position}");
            
            // Force immediate ground check for debugging
            PerformGroundCheckAtPosition(transform.position);
            Debug.Log($"Immediate ground check result: {localIsGrounded}");
        }
    }

    private void PerformJump()
    {
        if (!IsOwner) return;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        
        if (_animator != null)
            _animator.SetTrigger("jump");
            
        // Update states immediately
        localIsGrounded = false;
        networkIsGrounded.Value = false;
        
        Debug.Log($"[JUMP] Performed jump at position: {transform.position}");
        
        // Notify other clients
        PlayJumpAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayJumpAnimationClientRpc()
    {
        if (!IsOwner && _animator != null)
        {
            _animator.SetTrigger("jump");
        }
    }

    private void ApplyMovement(Vector3 input)
    {
        if (!IsOwner) return;

        float inputMagnitude = input.magnitude;
        bool isHolding = inputMagnitude > 0.1f;

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
    }

    public void Look(Vector3 lookInput)
    {
        if (!IsOwner) return;
        float yawDelta = lookInput.x * mouseSensitivity;
        Quaternion deltaRotation = Quaternion.Euler(0f, yawDelta, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    void OnDrawGizmos()
    {
        if (showGroundCheckGizmos && Application.isPlaying)
        {
            // Different colors for different client types and states
            Color gizmoColor;
            if (IsOwner)
            {
                gizmoColor = localIsGrounded ? Color.green : Color.red;
            }
            else
            {
                // Client colors: green if both local and network agree, yellow if disagree, red if both false
                if (localIsGrounded && networkIsGrounded.Value)
                    gizmoColor = Color.green;
                else if (localIsGrounded != networkIsGrounded.Value)
                    gizmoColor = Color.yellow;
                else
                    gizmoColor = Color.red;
            }
            
            // Draw ground check sphere
            Gizmos.color = gizmoColor;
            if (sphereCheckPosition != Vector3.zero)
            {
                Gizmos.DrawWireSphere(sphereCheckPosition, capsuleRadius * 0.9f);
            }
            
            // Draw origin point
            Gizmos.color = Color.white;
            if (groundCheckOrigin != Vector3.zero)
            {
                Gizmos.DrawWireSphere(groundCheckOrigin, 0.1f);
            }
            
            // Draw connection line
            Gizmos.color = gizmoColor;
            if (groundCheckOrigin != Vector3.zero && sphereCheckPosition != Vector3.zero)
            {
                Gizmos.DrawLine(groundCheckOrigin, sphereCheckPosition);
            }
            
            // Show client type indicator
            if (!IsOwner)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.2f);
                
                // Show network position if different from current position
                if (Vector3.Distance(transform.position, networkPosition.Value) > 0.05f)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(networkPosition.Value, 0.3f);
                    Gizmos.DrawLine(transform.position, networkPosition.Value);
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (networkIsGrounded != null)
            networkIsGrounded.OnValueChanged -= OnGroundedStateChanged;
        if (networkPosition != null)
            networkPosition.OnValueChanged -= OnNetworkPositionChanged;
        base.OnNetworkDespawn();
    }
}