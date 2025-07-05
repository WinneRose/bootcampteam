using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
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
    public float mouseSensitivity = 1.5f;

    [Header("Jump Settings")]
    public float jumpForce = 7f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = -1; // Default to everything
    [SerializeField] private bool isGrounded = false;
    
    // Ground check improvements
    [Header("Ground Check Debug")]
    public bool showGroundCheckGizmos = true;
    private Vector3 groundCheckOrigin;
    private float capsuleRadius = 0.5f; // Adjust based on your character's collider

    // Anti-flicker input caching
    private Vector3 lastInput;
    private float lastInputTime;
    private float inputStabilityTime = 0.05f;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Get capsule radius if available
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsuleRadius = capsule.radius;
        }
    }

    void FixedUpdate()
    {
        // Improved ground check
        GroundCheck();

        if (Time.fixedTime - lastInputTime < inputStabilityTime)
        {
            ApplyMovement(lastInput);
        }
    }

    private void GroundCheck()
    {
        {
            groundCheckOrigin = transform.position;

            float capsuleHeight = 2f; // Adjust if your character is shorter
            float checkRadius = capsuleRadius * 0.9f; // Slightly smaller than actual collider

            Vector3 start = groundCheckOrigin + Vector3.up * 0.1f;
            Vector3 end = groundCheckOrigin + Vector3.down * groundCheckDistance;

            isGrounded = Physics.CheckCapsule(start, end, checkRadius, groundLayer, QueryTriggerInteraction.Ignore);

            if (showGroundCheckGizmos)
            {
                Debug.DrawLine(start, end, isGrounded ? Color.green : Color.red);
            }
        }
    }

    public void Move(Vector3 input)
    {
        lastInput = input;
        lastInputTime = Time.time;
    }

    public void Jump()
    {
        if (isGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (_animator != null)
                _animator.SetTrigger("jump");
        }
    }

    private void ApplyMovement(Vector3 input)
    {
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
        float yawDelta = lookInput.x * mouseSensitivity;
        Quaternion deltaRotation = Quaternion.Euler(0f, yawDelta, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (showGroundCheckGizmos && Application.isPlaying)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckOrigin, 0.1f);
            Gizmos.DrawLine(groundCheckOrigin, groundCheckOrigin + Vector3.down * (groundCheckDistance + 0.1f));
        }
    }
}