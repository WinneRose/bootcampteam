using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlantGrowthManager : NetworkBehaviour
{
    [Header("Growth Settings")]
    [SerializeField] private int maxGrowthPhase = 4;
    [SerializeField] private Vector3 scalePerGrowth = new Vector3(0.2f, 0.2f, 0.2f);
    [SerializeField] private Vector3 baseScale = new Vector3(1f, 1f, 1f);

    [Header("Colors")]
    [SerializeField] private Color[] phaseColors = new Color[5];
    [SerializeField] private Color witherColor = Color.red;
    [SerializeField] private float witherDuration = 1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem witherEffect;

    [Header("Timing")]
    [SerializeField] private float projectileTimeout = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Network Variables
    private NetworkVariable<int> growthPhase = new NetworkVariable<int>(0);
    private NetworkVariable<int> waterHits = new NetworkVariable<int>(0);
    private NetworkVariable<int> solarHits = new NetworkVariable<int>(0);

    // Inspector Properties
    public int CurrentPhase => growthPhase.Value;
    public int WaterCount => waterHits.Value;
    public int SolarCount => solarHits.Value;
    public bool HasWater => waterHits.Value > 0;
    public bool HasSolar => solarHits.Value > 0;

    // Components
    private Renderer plantRenderer;
    private Collider plantCollider;
    
    // Timeout handling
    private Coroutine timeoutCoroutine;
    
    // Simple collision cooldown instead of complex caching
    private float lastCollisionTime = 0f;
    private const float collisionCooldown = 0.1f;

    private void Awake()
    {
        plantRenderer = GetComponent<Renderer>();
        plantCollider = GetComponent<Collider>();
        
        // Use solid collider for physics-based detection
        if (plantCollider != null)
        {
            plantCollider.isTrigger = false;
        }
        
        // Set default colors
        SetDefaultColors();
    }

    private void SetDefaultColors()
    {
        if (phaseColors.Length < 5) 
            phaseColors = new Color[5];
            
        if (phaseColors[0] == Color.clear)
        {
            phaseColors[0] = new Color(0.8f, 0.4f, 0.2f); // Brown
            phaseColors[1] = new Color(0.6f, 0.8f, 0.3f); // Light Green
            phaseColors[2] = new Color(0.4f, 0.7f, 0.2f); // Green
            phaseColors[3] = new Color(0.2f, 0.6f, 0.1f); // Dark Green
            phaseColors[4] = new Color(0.1f, 0.5f, 0.0f); // Deep Green
        }
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to network variable changes
        growthPhase.OnValueChanged += OnGrowthChanged;
        
        if (IsServer)
        {
            // Initialize on server
            ResetPlantState();
            Log("Plant spawned on server - initialized");
        }

        // Update visuals for all clients
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        growthPhase.OnValueChanged -= OnGrowthChanged;
        
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    #region Collision Detection

    private void OnCollisionEnter(Collision collision)
    {
        HandleProjectileCollision(collision.gameObject);
    }

    private void HandleProjectileCollision(GameObject projectile)
    {
        if (projectile == null) return;

        string tag = projectile.tag;
        int projectileId = projectile.GetInstanceID();
        
        // Check for valid projectile types first
        bool isWater = tag.Equals("waterProjectile", System.StringComparison.OrdinalIgnoreCase);
        bool isSolar = tag.Equals("solarProjectile", System.StringComparison.OrdinalIgnoreCase);

        if (!isWater && !isSolar)
        {
            Log($"Invalid projectile tag: '{tag}' (expected 'waterProjectile' or 'solarProjectile')");
            return;
        }

        Log($"Projectile collision detected: {projectile.name} with tag '{tag}' (ID: {projectileId})");

        // Simple approach: ALWAYS send to server (even if we are the server)
        // This ensures consistent behavior regardless of who shot the projectile
        if (isWater)
        {
            // Always use ServerRpc to ensure single processing point
            if (IsServer)
            {
                // Server calls the handler directly to avoid network overhead
                HandleHitDirectly("water", projectileId);
            }
            else
            {
                // Client sends ServerRpc
                OnProjectileHitServerRpc("water", projectileId);
            }
        }
        else if (isSolar)
        {
            // Always use ServerRpc to ensure single processing point
            if (IsServer)
            {
                // Server calls the handler directly to avoid network overhead
                HandleHitDirectly("solar", projectileId);
            }
            else
            {
                // Client sends ServerRpc
                OnProjectileHitServerRpc("solar", projectileId);
            }
        }

        // Destroy projectile immediately
        DestroyProjectile(projectile);
    }

    #endregion

    #region Network RPCs

    [ServerRpc(RequireOwnership = false)]
    private void OnProjectileHitServerRpc(string projectileType, int projectileId)
    {
        if (!IsServer) return;
        Log($"Server received {projectileType} hit via ServerRpc (ID: {projectileId})");
        HandleHitDirectly(projectileType, projectileId);
    }

    [ClientRpc]
    private void SyncStateClientRpc(int currentGrowth, int currentWater, int currentSolar)
    {
        Log($"Client sync - Growth: {currentGrowth}, Water: {currentWater}, Solar: {currentSolar}");
        UpdateVisuals();
    }

    // Direct hit handling method (used by both ServerRpc and direct server calls)
    private void HandleHitDirectly(string projectileType, int projectileId)
    {
        if (!IsServer) return;

        // Apply cooldown check here on the server to prevent rapid duplicates
        float currentTime = Time.time;
        if (currentTime - lastCollisionTime < collisionCooldown)
        {
            Log($"Server: Hit ignored - too soon after last hit (cooldown: {collisionCooldown}s)");
            return;
        }

        lastCollisionTime = currentTime;

        if (projectileType == "water")
        {
            waterHits.Value++;
            Log($"Water hits: {waterHits.Value}");
        }
        else if (projectileType == "solar")
        {
            solarHits.Value++;
            Log($"Solar hits: {solarHits.Value}");
        }

        // Check for growth/wither immediately
        CheckGrowthConditions();
        
        // Only start timeout if we have mixed hits (not perfect conditions)
        int water = waterHits.Value;
        int solar = solarHits.Value;
        
        // Start timeout only if we have incomplete/mixed state that needs resolution
        if ((water > 0 && solar > 0 && (water != 1 || solar != 1)) || 
            (water > 1 && solar == 0) || 
            (solar > 1 && water == 0))
        {
            Log($"Starting timeout for mixed/excess hits: Water={water}, Solar={solar}");
            StartProjectileTimeout();
        }
        
        // Sync state to all clients
        SyncStateClientRpc(growthPhase.Value, waterHits.Value, solarHits.Value);
    }

    #endregion

    #region Growth Logic

    private void CheckGrowthConditions()
    {
        if (!IsServer) return;

        int water = waterHits.Value;
        int solar = solarHits.Value;
        
        Log($"Checking growth - Water: {water}, Solar: {solar}");

        // Perfect growth: exactly 1 water + 1 solar (ORDER DOESN'T MATTER)
        if (water == 1 && solar == 1)
        {
            if (growthPhase.Value < maxGrowthPhase)
            {
                growthPhase.Value++;
                Log($"🌱 GROWTH! New phase: {growthPhase.Value} (Water={water}, Solar={solar})");
            }
            else
            {
                Log($"🌿 Already at max growth ({maxGrowthPhase})");
            }
            ResetCounters();
            return;
        }

        // Wither conditions: too much of either type
        if (water > 1 || solar > 1)
        {
            if (growthPhase.Value > 0)
            {
                growthPhase.Value--;
                Log($"💀 WITHER! New phase: {growthPhase.Value} (Water={water}, Solar={solar})");
                TriggerWitherEffect();
            }
            else
            {
                Log($"💀 Already at minimum phase (Water={water}, Solar={solar})");
            }
            ResetCounters();
            return;
        }

        // Wait for more projectiles if we only have 1 total hit
        int totalHits = water + solar;
        if (totalHits == 1)
        {
            if (water == 1)
            {
                Log($"✋ Waiting for SOLAR projectile... (Water: {water}, Solar: {solar})");
            }
            else
            {
                Log($"✋ Waiting for WATER projectile... (Water: {water}, Solar: {solar})");
            }
        }
    }

    private void ResetCounters()
    {
        if (!IsServer) return;
        
        Log($"Resetting counters - Was Water: {waterHits.Value}, Solar: {solarHits.Value}");
        
        waterHits.Value = 0;
        solarHits.Value = 0;
        
        // Reset collision cooldown when resetting counters
        lastCollisionTime = 0f;
        Log("Reset collision cooldown");
        
        StopProjectileTimeout();
    }

    #endregion

    #region Timeout Management

    private void StartProjectileTimeout()
    {
        if (!IsServer) return;
        
        StopProjectileTimeout();
        timeoutCoroutine = StartCoroutine(ProjectileTimeoutCoroutine());
    }

    private void StopProjectileTimeout()
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    private IEnumerator ProjectileTimeoutCoroutine()
    {
        yield return new WaitForSeconds(projectileTimeout);
        
        // Timeout reached - only reset if we have problematic combinations
        int water = waterHits.Value;
        int solar = solarHits.Value;
        
        // Reset conditions:
        // 1. Mixed hits that didn't resolve to perfect growth
        // 2. Excess of one type that should have withered by now
        bool shouldReset = (water > 0 && solar > 0 && (water != 1 || solar != 1)) ||
                          (water > 1 && solar == 0) ||
                          (solar > 1 && water == 0);
        
        if (shouldReset)
        {
            Log($"⏰ Timeout! Resetting problematic combination (Water: {water}, Solar: {solar})");
            ResetCounters();
        }
        else
        {
            Log($"⏰ Timeout reached but keeping valid single hits (Water: {water}, Solar: {solar})");
        }
        
        timeoutCoroutine = null;
    }

    #endregion

    #region Visual Updates

    private void OnGrowthChanged(int oldPhase, int newPhase)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        UpdateScale();
        UpdateColor();
    }

    private void UpdateScale()
    {
        transform.localScale = baseScale + (scalePerGrowth * growthPhase.Value);
    }

    private void UpdateColor()
    {
        if (plantRenderer != null && growthPhase.Value < phaseColors.Length)
        {
            // Create new material instance
            plantRenderer.material = new Material(plantRenderer.material);
            plantRenderer.material.color = phaseColors[growthPhase.Value];
        }
    }

    private void TriggerWitherEffect()
    {
        StartCoroutine(WitherEffectCoroutine());
    }

    private IEnumerator WitherEffectCoroutine()
    {
        Color originalColor = phaseColors[growthPhase.Value];
        
        // Flash red
        if (plantRenderer != null)
        {
            plantRenderer.material.color = witherColor;
        }
        
        if (witherEffect != null) 
            witherEffect.Play();
        
        yield return new WaitForSeconds(witherDuration);
        
        // Restore color
        UpdateColor();
    }

    #endregion

    #region Projectile Destruction

    private void DestroyProjectile(GameObject projectile)
    {
        if (projectile == null) return;

        Log($"Destroying projectile: {projectile.name}");

        // Try projectile's ForceDestroy method first
        Projectile projScript = projectile.GetComponent<Projectile>();
        if (projScript != null)
        {
            projScript.ForceDestroy();
            return;
        }

        // Fallback destruction
        NetworkObject netObj = projectile.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && IsServer)
        {
            netObj.Despawn();
        }
        else if (netObj == null)
        {
            Destroy(projectile);
        }
    }

    #endregion

    #region Public Methods (for Editor)

    public void TestGrowth()
    {
        if (!IsServer) return;
        waterHits.Value = 1;
        solarHits.Value = 1;
        CheckGrowthConditions();
    }

    public void TestWither()
    {
        if (!IsServer) return;
        waterHits.Value = 2;
        solarHits.Value = 0;
        CheckGrowthConditions();
    }

    public void ResetPlant()
    {
        if (!IsServer) return;
        ResetPlantState();
    }

    public void MaxGrowth()
    {
        if (!IsServer) return;
        growthPhase.Value = maxGrowthPhase;
        ResetCounters();
    }

    public void AddWaterHit()
    {
        if (!IsServer) return;
        Log("Manual water hit added via editor");
        HandleHitDirectly("water", Random.Range(10000, 99999));
    }

    public void AddSolarHit()
    {
        if (!IsServer) return;
        Log("Manual solar hit added via editor");
        HandleHitDirectly("solar", Random.Range(10000, 99999));
    }

    private void ResetPlantState()
    {
        if (!IsServer) return;
        
        growthPhase.Value = 0;
        waterHits.Value = 0;
        solarHits.Value = 0;
        lastCollisionTime = 0f;
        StopProjectileTimeout();
        
        Log("Plant state reset to initial values");
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{(IsServer ? "SERVER" : "CLIENT")}] {gameObject.name}: {message}");
        }
    }

    [ContextMenu("Debug Plant State")]
    public void DebugPlantState()
    {
        Debug.Log($"=== PLANT STATE DEBUG ===");
        Debug.Log($"Role: {(IsServer ? "SERVER" : "CLIENT")}");
        Debug.Log($"Growth Phase: {growthPhase.Value}/{maxGrowthPhase}");
        Debug.Log($"Water Hits: {waterHits.Value}");
        Debug.Log($"Solar Hits: {solarHits.Value}");
        Debug.Log($"Last Collision Time: {lastCollisionTime}");
        Debug.Log($"Current Time: {Time.time}");
        Debug.Log($"Has Timeout Active: {timeoutCoroutine != null}");
        Debug.Log($"Collider Is Trigger: {plantCollider?.isTrigger} (using solid collider)");
        Debug.Log($"========================");
    }

    [ContextMenu("Force Reset (Server Only)")]
    public void ForceReset()
    {
        if (!IsServer)
        {
            Debug.LogWarning("ForceReset can only be called on server!");
            return;
        }
        
        ResetPlantState();
        Debug.Log("Force reset completed");
    }

    #endregion
}