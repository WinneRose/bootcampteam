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
    public float groundCheckDistance = 0.3f; // INCREASED from 0.1f
    public LayerMask groundLayer = -1; // Default to everything
    [SerializeField] private bool isGrounded = false;
    
    // Ground check improvements
    [Header("Ground Check Debug")]
    public bool showGroundCheckGizmos = true;
    private Vector3 groundCheckOrigin;
    private Vector3 sphereCheckPosition; // Store for gizmo drawing
    [SerializeField] private float capsuleRadius = 0.5f; // Adjust based on your character's collider

    // Anti-flicker input caching
    private Vector3 lastInput;
    private float lastInputTime;
    private float inputStabilityTime = 0.05f;

    void Start()
    {
        // Get capsule radius FIRST for all clients (needed for ground check visualization)
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsuleRadius = capsule.radius;
        }

        // YALNIZCA KENDİ OYUNCUMUZ İÇİN ÇALIŞACAK KODLAR
        // Eğer bu nesnenin sahibi ben değilsem, diğer istemcilerdeki kopyası için bu metodu çalıştırma.
        if (!IsOwner)
        {
            // Eğer diğer oyuncuların kamerası veya input sistemi varsa,
            // bunları burada kapatarak çakışmayı önleyebilirsiniz.
            // Örneğin: GetComponent<Camera>().enabled = false;
            // GetComponent<PlayerInput>().enabled = false; // Input System kullanıyorsanız
            // Cursor.lockState ve Cursor.visible ayarları sadece yerel oyuncuya uygulanmalı.
            // Bu nedenle, aşağıdaki satırları buraya değil, sadece IsOwner ise çalışacak şekilde taşıyacağız.
            return; 
        }

        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Fare kilit ve görünürlük ayarları sadece yerel oyuncuya ait olmalı
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate()
    {
        // Ground check runs for ALL clients (each calculates locally)
        GroundCheck();

        // ÖNEMLİ: Sadece bu NetworkObject'in sahibi ise hareketi işle
        if (!IsOwner) return;

        if (Time.fixedTime - lastInputTime < inputStabilityTime)
        {
            ApplyMovement(lastInput);
        }
    }

    // GroundCheck metodu tüm istemcilerde çalışabilir, 
    // çünkü bu görsel hata ayıklama ve zıplama kontrolü için gerekli bir bilgi.
    private void GroundCheck()
    {
        groundCheckOrigin = transform.position;
    
        // FIXED: Better sphere position calculation
        // Calculate the bottom of the capsule collider
        float capsuleBottom = capsuleRadius; // Distance from center to bottom
        sphereCheckPosition = groundCheckOrigin - Vector3.up * capsuleBottom - Vector3.up * groundCheckDistance;
    
        // Check for ground using sphere
        bool previousGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(sphereCheckPosition, capsuleRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // Debug logging to see what's happening
        if (previousGrounded != isGrounded)
        {
            Debug.Log($"[{gameObject.name}] Ground state changed: {previousGrounded} -> {isGrounded}");
            Debug.Log($"Check position: {sphereCheckPosition}");
            Debug.Log($"Check radius: {capsuleRadius }");
            Debug.Log($"Ground layer: {groundLayer.value}");
        }

        // Additional debug - test what we're actually hitting
        if (Physics.CheckSphere(sphereCheckPosition, capsuleRadius, -1, QueryTriggerInteraction.Ignore))
        {
            // Something is there, but maybe not on the right layer
            Collider[] hits = Physics.OverlapSphere(sphereCheckPosition, capsuleRadius, -1, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0 && !isGrounded)
            {
                Debug.Log($"[{gameObject.name}] Found {hits.Length} colliders but none match ground layer:");
                foreach (Collider hit in hits)
                {
                    Debug.Log($"  - {hit.name} on layer {hit.gameObject.layer} ({LayerMask.LayerToName(hit.gameObject.layer)})");
                    Debug.Log($"  - Is in ground layer mask: {((1 << hit.gameObject.layer) & groundLayer) != 0}");
                }
            }
        }

        if (showGroundCheckGizmos)
        {
            // Draw line from center to check position
            Debug.DrawLine(groundCheckOrigin, sphereCheckPosition, isGrounded ? Color.green : Color.red);
        }
    }

    // Input fonksiyonları, sadece sahibi olan istemci tarafından çağrılmalı.
    // Bu yüzden içine IsOwner kontrolü eklemeye gerek yok, çünkü çağıran kod zaten IsOwner kontrolü yapmalı.
    public void Move(Vector3 input)
    {
        if (!IsOwner) return; // Yine de emin olmak için buraya da ekleyebiliriz.
        lastInput = input;
        lastInputTime = Time.time;
    }

    public void Jump()
    {
        if (!IsOwner) return; // Yine de emin olmak için buraya da ekleyebiliriz.
        if (isGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (_animator != null)
                _animator.SetTrigger("jump");
        }
    }

    private void ApplyMovement(Vector3 input)
    {
        // Bu metot zaten FixedUpdate içinden çağrılıyor ve FixedUpdate'te IsOwner kontrolü var.
        // Tekrar eklemeye gerek yok, ancak güvenlik için eklenebilir.
        // if (!IsOwner) return; 

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
        if (!IsOwner) return; // Yine de emin olmak için buraya da ekleyebiliriz.
        float yawDelta = lookInput.x * mouseSensitivity;
        Quaternion deltaRotation = Quaternion.Euler(0f, yawDelta, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    // Debug visualization - Bu metot tüm istemcilerde çalışabilir.
    void OnDrawGizmos()
    {
        if (showGroundCheckGizmos && Application.isPlaying)
        {
            // Use the actual ground check results now that all clients calculate it
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(sphereCheckPosition, capsuleRadius * 0.9f);
            
            // Draw origin point
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(groundCheckOrigin, 0.1f);
            
            // Draw line connecting them
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheckOrigin, sphereCheckPosition);
        }
    }
}