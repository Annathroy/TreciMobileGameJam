using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class BeamLink : MonoBehaviour
{
    public enum RenderMode
    {
        Line,
        Sprite
    }

    [Header("References")]
    public Transform a;
    public Transform b;

    [Header("Render Mode")]
    [SerializeField] private RenderMode renderMode = RenderMode.Line;
    [SerializeField, Tooltip("PNG sprite to render instead of line")]
    private Sprite beamSprite;
    [SerializeField, Tooltip("Material for sprite rendering (optional)")]
    private Material spriteMaterial;

    [Header("Periodic Activation")]
    [SerializeField, Tooltip("Enable periodic beam activation")]
    private bool periodicBeam = false; // Changed to false by default for testing
    [SerializeField, Tooltip("Time beam stays active")]
    private float activeTime = 2f;
    [SerializeField, Tooltip("Time beam stays inactive")]
    private float inactiveTime = 3f;
    [SerializeField, Tooltip("Initial delay before first activation")]
    private float initialDelay = 1f;

    [Header("Beam")]
    [SerializeField] float beamRadius = 0.15f;
    [SerializeField] float lineWidth = 0.08f;
    [SerializeField] LayerMask playerMask;
    [SerializeField] int damagePerTick = 1;
    [SerializeField] float tickInterval = 0.2f;

    [Header("Sprite Settings")]
    [SerializeField, Tooltip("Width of the sprite beam")]
    private float spriteWidth = 1f;
    [SerializeField, Tooltip("How many sprite segments to use based on distance")]
    private float spriteSegmentsPerUnit = 2f;
    [SerializeField, Tooltip("Sprite rendering sorting order")]
    private int sortingOrder = 10; // Increased default sorting order
    [SerializeField, Tooltip("Sprite rendering layer name")]
    private string sortingLayerName = "Default";
    [SerializeField, Tooltip("Use single stretched sprite instead of multiple segments")]
    private bool useSingleSprite = true;

    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging for sprite rendering")]
    private bool enableDebugLogs = false;
    [SerializeField, Tooltip("Force sprite to be visible for testing")]
    private bool forceVisible = true;
    [SerializeField, Tooltip("Test color for sprite")]
    private Color debugColor = Color.red;

    [Header("Manual Rotation Override")]
    [SerializeField, Tooltip("Override automatic rotation calculation")]
    private bool useManualRotation = false;
    [SerializeField, Tooltip("Manual rotation values (X, Y, Z)")]
    private Vector3 manualRotation = new Vector3(90f, 0f, 0f);

    LineRenderer line;
    float nextTick;
    readonly HashSet<PlayerHealth> hitThisTick = new();

    // Sprite rendering components
    private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private Transform spriteContainer;

    // Periodic beam state
    private bool beamActive = false;
    private Coroutine periodicCoroutine;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.enabled = false;

        // Create container for sprite renderers
        GameObject container = new GameObject("SpriteBeamContainer");
        container.transform.SetParent(transform);
        spriteContainer = container.transform;

        if (enableDebugLogs)
        {
            Debug.Log($"[BeamLink] Awake - Render Mode: {renderMode}, Has Sprite: {beamSprite != null}, Periodic: {periodicBeam}");
        }
    }

    void Start()
    {
        if (periodicBeam)
        {
            periodicCoroutine = StartCoroutine(PeriodicBeamCycle());
        }
        else
        {
            // If not periodic, enable immediately
            beamActive = true; // Set beam as active immediately
            Enable(true);

            if (enableDebugLogs)
                Debug.Log("[BeamLink] Non-periodic beam enabled at start");
        }
    }

    void OnDestroy()
    {
        if (periodicCoroutine != null)
        {
            StopCoroutine(periodicCoroutine);
        }

        // Clean up sprite renderers
        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
                DestroyImmediate(sr.gameObject);
        }
        spriteRenderers.Clear();
    }

    IEnumerator PeriodicBeamCycle()
    {
        // Initial delay
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (true)
        {
            // Activate beam
            if (enableDebugLogs)
                Debug.Log("[BeamLink] Activating beam (periodic)");

            Enable(true);
            beamActive = true;
            yield return new WaitForSeconds(activeTime);

            // Deactivate beam
            if (enableDebugLogs)
                Debug.Log("[BeamLink] Deactivating beam (periodic)");

            Enable(false);
            beamActive = false;
            yield return new WaitForSeconds(inactiveTime);
        }
    }

    public void Enable(bool on)
    {
        bool hasValidPoints = a && b;

        if (enableDebugLogs)
        {
            Debug.Log($"[BeamLink] Enable called - On: {on}, HasValidPoints: {hasValidPoints}, RenderMode: {renderMode}, HasSprite: {beamSprite != null}");
        }

        if (renderMode == RenderMode.Line)
        {
            line.enabled = on && hasValidPoints;
            SetSpritesEnabled(false);
        }
        else if (renderMode == RenderMode.Sprite)
        {
            line.enabled = false;
            SetSpritesEnabled(on && hasValidPoints && beamSprite);
        }

        nextTick = 0f;
    }

    void Update()
    {
        if (!a || !b) return;

        Vector3 pa = a.position, pb = b.position;

        // Update rendering based on mode
        if (renderMode == RenderMode.Line && line.enabled)
        {
            line.SetPosition(0, pa);
            line.SetPosition(1, pb);
        }
        else if (renderMode == RenderMode.Sprite && beamSprite)
        {
            // Always update sprite beam if we're in sprite mode and beam is active
            bool shouldShowSprite = !periodicBeam || beamActive;
            if (shouldShowSprite)
            {
                UpdateSpriteBeam(pa, pb);
            }
            else if (AnySpritesEnabled())
            {
                // Disable sprites if beam shouldn't be showing
                SetSpritesEnabled(false);
            }
        }

        // Damage logic (same for both modes)
        bool shouldDamage = false;
        if (periodicBeam)
        {
            shouldDamage = beamActive && ((renderMode == RenderMode.Line && line.enabled) ||
                          (renderMode == RenderMode.Sprite && beamSprite && AnySpritesEnabled()));
        }
        else
        {
            shouldDamage = (renderMode == RenderMode.Line && line.enabled) ||
                          (renderMode == RenderMode.Sprite && beamSprite && AnySpritesEnabled());
        }

        if (!shouldDamage || Time.time < nextTick) return;

        nextTick = Time.time + tickInterval;
        hitThisTick.Clear();

        var cols = Physics.OverlapCapsule(pa, pb, beamRadius, playerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cols.Length; i++)
        {
            PlayerHealth ph = cols[i].GetComponent<PlayerHealth>();
            if (ph == null)
                ph = cols[i].GetComponentInParent<PlayerHealth>();

            if (ph != null && hitThisTick.Add(ph))
                ph.ApplyDamage(damagePerTick);
        }
    }

    void UpdateSpriteBeam(Vector3 startPos, Vector3 endPos)
    {
        if (!beamSprite)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[BeamLink] UpdateSpriteBeam called but beamSprite is null!");
            return;
        }

        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        if (enableDebugLogs && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
        {
            Debug.Log($"[BeamLink] UpdateSpriteBeam - Distance: {distance}, Direction: {direction}, StartPos: {startPos}, EndPos: {endPos}");
        }

        if (distance < 0.01f)
        {
            SetSpritesEnabled(false);
            return;
        }

        if (useSingleSprite)
        {
            UpdateSingleSprite(startPos, endPos, direction, distance);
        }
        else
        {
            UpdateMultipleSprites(startPos, endPos, direction, distance);
        }
    }

    void UpdateSingleSprite(Vector3 startPos, Vector3 endPos, Vector3 direction, float distance)
    {
        // Ensure we have at least one sprite renderer
        if (spriteRenderers.Count == 0)
        {
            CreateSpriteRenderer();
            if (enableDebugLogs)
                Debug.Log("[BeamLink] Created first sprite renderer");
        }

        // Disable extra sprites
        for (int i = 1; i < spriteRenderers.Count; i++)
        {
            spriteRenderers[i].enabled = false;
        }

        SpriteRenderer sr = spriteRenderers[0];
        sr.enabled = true;

        // Position at center of beam
        Vector3 center = (startPos + endPos) * 0.5f;
        sr.transform.position = center;

        // COMPREHENSIVE ROTATION FIX - Try different approaches based on sprite orientation
        Vector3 directionXZ = new Vector3(direction.x, 0, direction.z);
        
        if (directionXZ.magnitude > 0.001f)
        {
            // Calculate the Y-axis rotation to face the direction
            float yAngle = Mathf.Atan2(directionXZ.x, directionXZ.z) * Mathf.Rad2Deg;
            
            // Try multiple rotation combinations to find what works
            // Option 1: Standard XZ plane with potential Z correction
            if (beamSprite && beamSprite.bounds.size.y > beamSprite.bounds.size.x)
            {
                // Sprite is taller than wide - needs Z rotation to make horizontal
                sr.transform.rotation = Quaternion.Euler(90f, yAngle, 90f);
            }
            else
            {
                // Sprite is wider than tall - use as-is
                sr.transform.rotation = Quaternion.Euler(90f, yAngle, 0f);
            }
        }
        else
        {
            // Fallback rotation
            sr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Manual rotation override for testing
        if (useManualRotation)
        {
            sr.transform.rotation = Quaternion.Euler(manualRotation);
        }
        else
        {
            // Your existing rotation logic here...
        }

        // ENHANCED SCALING with proper axis handling
        Vector3 scale = Vector3.one;
        
        if (beamSprite && beamSprite.bounds.size.x > 0 && beamSprite.bounds.size.y > 0)
        {
            // Log sprite dimensions for debugging
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[BeamLink] Sprite dimensions - Width: {beamSprite.bounds.size.x}, Height: {beamSprite.bounds.size.y}");
                Debug.Log($"[BeamLink] Sprite is taller than wide: {beamSprite.bounds.size.y > beamSprite.bounds.size.x}");
            }
            
            // Determine scaling based on sprite orientation and rotation applied
            bool spriteIsTaller = beamSprite.bounds.size.y > beamSprite.bounds.size.x;
            bool appliedZRotation = (sr.transform.rotation.eulerAngles.z > 45f && sr.transform.rotation.eulerAngles.z < 135f);
            
            if (spriteIsTaller && appliedZRotation)
            {
                // Sprite was rotated 90° around Z, so axes are swapped
                scale.x = spriteWidth / beamSprite.bounds.size.y;   // Width becomes X after Z rotation
                scale.y = distance / beamSprite.bounds.size.x;     // Length becomes Y after Z rotation
            }
            else if (!spriteIsTaller || !appliedZRotation)
            {
                // Standard scaling - sprite is naturally horizontal or no Z rotation applied
                scale.x = distance / beamSprite.bounds.size.x;     // Length along X
                scale.y = spriteWidth / beamSprite.bounds.size.y;  // Width along Y
            }
        }
        else
        {
            // Fallback scaling
            scale.x = distance;
            scale.y = spriteWidth;
        }
        
        scale.z = 1f;
        sr.transform.localScale = scale;

        // Apply debug color
        if (forceVisible)
        {
            sr.color = debugColor;
        }
        else
        {
            sr.color = Color.white;
        }

        // Enhanced debug logging
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[BeamLink] DETAILED SPRITE INFO:");
            Debug.Log($"  Position: {center}");
            Debug.Log($"  Rotation: {sr.transform.rotation.eulerAngles}");
            Debug.Log($"  Scale: {scale}");
            Debug.Log($"  Distance: {distance}");
            Debug.Log($"  Direction: {direction}");
            Debug.Log($"  DirectionXZ: {directionXZ}");
            
            if (beamSprite)
            {
                Debug.Log($"  Sprite Bounds: {beamSprite.bounds.size}");
                Debug.Log($"  Sprite Pivot: {beamSprite.pivot}");
            }
        }
    }

    void UpdateMultipleSprites(Vector3 startPos, Vector3 endPos, Vector3 direction, float distance)
    {
        direction.Normalize();

        // Calculate number of segments needed
        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(distance * spriteSegmentsPerUnit));
        float segmentLength = distance / segmentCount;

        // Ensure we have enough sprite renderers
        while (spriteRenderers.Count < segmentCount)
        {
            CreateSpriteRenderer();
        }

        // FIXED: Calculate rotation for XZ plane AND make it horizontal
        Vector3 directionXZ = new Vector3(direction.x, 0, direction.z);
        Quaternion rotation;
        
        if (directionXZ.magnitude > 0.001f)
        {
            // First rotate 90° around X-axis to lay sprite flat on XZ plane
            // Then rotate around Y-axis to align with beam direction
            // Finally rotate 90° around Z-axis to make it horizontal
            float yAngle = Mathf.Atan2(directionXZ.x, directionXZ.z) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(90f, yAngle, 90f);
        }
        else
        {
            rotation = Quaternion.Euler(90f, 0f, 90f);
        }

        // Update sprite positions and rotations
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (i < segmentCount)
            {
                SpriteRenderer sr = spriteRenderers[i];
                sr.enabled = true;

                // Position sprite along the beam
                float t = (i + 0.5f) / segmentCount;
                Vector3 position = Vector3.Lerp(startPos, endPos, t);
                sr.transform.position = position;

                // Apply XZ plane rotation with horizontal orientation
                sr.transform.rotation = rotation;

                // Scale sprite to fit segment (swapped axes due to Z rotation)
                Vector3 scale = Vector3.one;
                if (beamSprite.bounds.size.x > 0 && beamSprite.bounds.size.y > 0)
                {
                    scale.x = spriteWidth / beamSprite.bounds.size.x;     // Width
                    scale.y = segmentLength / beamSprite.bounds.size.y;  // Length
                }
                else
                {
                    scale.x = spriteWidth;
                    scale.y = segmentLength;
                }
                scale.z = 1f;
                sr.transform.localScale = scale;

                // Apply debug color if forceVisible is enabled
                if (forceVisible)
                {
                    sr.color = debugColor;
                }
                else
                {
                    sr.color = Color.white;
                }
            }
            else
            {
                spriteRenderers[i].enabled = false;
            }
        }

        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[BeamLink] Multiple sprites updated - Segments: {segmentCount}");
        }
    }

    void CreateSpriteRenderer()
    {
        GameObject spriteObj = new GameObject($"BeamSprite_{spriteRenderers.Count}");
        spriteObj.transform.SetParent(spriteContainer);

        SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.sprite = beamSprite;
        sr.sortingOrder = sortingOrder;
        sr.sortingLayerName = sortingLayerName;

        // Initialize lying flat on XZ plane and horizontal
        sr.transform.position = Vector3.zero;
        sr.transform.localScale = Vector3.one;
        sr.transform.rotation = Quaternion.Euler(90f, 0f, 90f); // Lay flat on XZ plane + horizontal

        if (spriteMaterial)
        {
            sr.material = spriteMaterial;
        }

        spriteRenderers.Add(sr);

        if (enableDebugLogs)
        {
            Debug.Log($"[BeamLink] Created sprite renderer {spriteRenderers.Count}: Sprite={sr.sprite?.name}, SortingOrder={sr.sortingOrder}, Layer={sr.sortingLayerName}, Material={sr.material?.name}");
        }
    }

    void SetSpritesEnabled(bool enabled)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BeamLink] SetSpritesEnabled: {enabled}, Sprite count: {spriteRenderers.Count}");
        }

        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = enabled;
                if (enableDebugLogs && enabled)
                {
                    Debug.Log($"[BeamLink] Sprite {sr.name} enabled: {sr.enabled}, GameObject active: {sr.gameObject.activeInHierarchy}");
                }
            }
        }
    }

    bool AnySpritesEnabled()
    {
        foreach (var sr in spriteRenderers)
        {
            if (sr != null && sr.enabled)
                return true;
        }
        return false;
    }

    // Public methods for external control
    public void StartPeriodicBeam()
    {
        if (periodicCoroutine != null)
        {
            StopCoroutine(periodicCoroutine);
        }
        periodicBeam = true;
        periodicCoroutine = StartCoroutine(PeriodicBeamCycle());
    }

    public void StopPeriodicBeam()
    {
        if (periodicCoroutine != null)
        {
            StopCoroutine(periodicCoroutine);
            periodicCoroutine = null;
        }
        periodicBeam = false;
        Enable(false);
        beamActive = false;
    }

    public bool IsBeamActive => beamActive;

#if UNITY_EDITOR
    void OnValidate()
    {
        // Update sprite settings in editor
        if (Application.isPlaying && renderMode == RenderMode.Sprite)
        {
            foreach (var sr in spriteRenderers)
            {
                if (sr != null)
                {
                    sr.sprite = beamSprite;
                    sr.sortingOrder = sortingOrder;
                    sr.sortingLayerName = sortingLayerName;
                    if (spriteMaterial)
                        sr.material = spriteMaterial;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (a && b)
        {
            // Use different colors based on beam state
            if (Application.isPlaying)
            {
                Gizmos.color = beamActive ? Color.red : Color.gray;
            }
            else
            {
                Gizmos.color = Color.red;
            }

            Gizmos.DrawWireSphere(a.position, beamRadius);
            Gizmos.DrawWireSphere(b.position, beamRadius);

            // Draw damage capsule
            Gizmos.color = new Color(1f, 0f, 0f, beamActive ? 0.3f : 0.1f);
            Vector3 direction = b.position - a.position;
            if (direction.magnitude > 0.01f)
            {
                Gizmos.matrix = Matrix4x4.TRS(a.position, Quaternion.LookRotation(direction), Vector3.one);
                Gizmos.DrawWireCube(Vector3.forward * direction.magnitude * 0.5f,
                                   new Vector3(beamRadius * 2, beamRadius * 2, direction.magnitude));
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Draw sprite positions if in sprite mode
            if (renderMode == RenderMode.Sprite && Application.isPlaying)
            {
                Gizmos.color = beamActive ? Color.cyan : Color.gray;
                foreach (var sr in spriteRenderers)
                {
                    if (sr != null && sr.enabled)
                    {
                        Gizmos.DrawWireCube(sr.transform.position, sr.bounds.size);
                    }
                }
            }
        }
    }
#endif
}