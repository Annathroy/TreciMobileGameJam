using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class KrakenHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHP = 1000;
    [SerializeField] private int currentHP;
    [Tooltip("Seconds of invulnerability after each hit.")]
    [SerializeField] private float iFrameDuration = 0.4f;

    [Header("What can damage me")]
    [Tooltip("Only these layers can hurt the Kraken (e.g., PlayerProjectile, Explosion).")]
    [SerializeField] private LayerMask damageLayers;
    [Tooltip("Optional: also filter by tags (leave empty to ignore).")]
    [SerializeField] private string[] damageTags;

    [Header("Damage on contact")]
    [Tooltip("Fixed damage applied when a damaging collider touches me (if the collider doesn't specify its own damage).")]
    [SerializeField] private int contactDamage = 10;
    [Tooltip("Destroy the projectile/hit object if it has no own lifetime logic.")]
    [SerializeField] private bool destroyHitObject = true;

    [Header("Reactions")]
    [Tooltip("Optional knockback force applied to my Rigidbody on hit.")]
    [SerializeField] private float knockback = 10f;
    [Tooltip("Flash my renderers while invulnerable.")]
    [SerializeField] private bool flashOnHit = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashInterval = 0.06f;

    [Header("Death Flash")]
    [Tooltip("Flash semi-white on death before returning to normal texture.")]
    [SerializeField] private bool flashOnDeath = true;
    [SerializeField] private Color deathFlashColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private float deathFlashDuration = 0.3f;

    [Header("Eye Texture Changes")]
    [Tooltip("Change eye textures on death.")]
    [SerializeField] private bool changeEyeTexturesOnDeath = true;
    [Tooltip("Reference to the Kraken's eye GameObject.")]
    [SerializeField] private GameObject eyeGameObject;
    [Tooltip("Renderers that represent the Kraken's eyes (assign manually).")]
    [SerializeField] private Renderer[] eyeRenderers;
    [Tooltip("Base texture for dead eyes.")]
    [SerializeField] private Texture2D deadEyeBaseTexture;
    [Tooltip("Normal map for dead eyes.")]
    [SerializeField] private Texture2D deadEyeNormalTexture;
    [Tooltip("Smoothness map texture for dead eyes.")]
    [SerializeField] private Texture2D deadEyeSmoothnessMap;
    [Tooltip("Smoothness value for dead eyes (0-1).")]
    [SerializeField] private float deadEyeSmoothness = 0.1f;

    [Header("Cleanup / Death")]
    [Tooltip("Disable these behaviours on death (AI, nav, attacks…).")]
    [SerializeField] private Behaviour[] disableOnDeath;
    [Tooltip("Disable these colliders on death.")]
    [SerializeField] private Collider[] collidersOnBody;
    [Tooltip("Optional: reference to your KrakenDeath; if null, searched on this GameObject.")]
    [SerializeField] private KrakenDeath krakenDeath;

    [Header("Victory Integration")]
    [Tooltip("Automatically find and subscribe to OnWin script on death.")]
    [SerializeField] private bool autoSubscribeToVictory = true;

    [Header("Events")]
    public UnityEvent<int, int> onHpChanged; // (current, max)
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    // --- internals ---
    bool invulnerable;
    bool isDead = false; // Track death state to prevent multiple death calls
    Rigidbody rb;
    readonly List<Renderer> rends = new List<Renderer>();
    MaterialPropertyBlock mpb;
    MaterialPropertyBlock eyeMpb; // Separate MPB for eyes
    static readonly int ColorProp = Shader.PropertyToID("_BaseColor"); // URP/Lit & Unlit use _BaseColor
    static readonly int BaseMapProp = Shader.PropertyToID("_BaseMap");
    static readonly int NormalMapProp = Shader.PropertyToID("_BumpMap");
    static readonly int SmoothnessProp = Shader.PropertyToID("_Smoothness");
    static readonly int SmoothnessMapProp = Shader.PropertyToID("_MetallicGlossMap"); // URP uses _MetallicGlossMap for smoothness map
    private OnWin onWinScript;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!krakenDeath) krakenDeath = GetComponent<KrakenDeath>();

        // cache renderers (children too)
        GetComponentsInChildren(true, rends);
        mpb = new MaterialPropertyBlock();
        eyeMpb = new MaterialPropertyBlock(); // Separate MPB for eyes

        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 1, maxHP);

        // Subscribe to death event for eye texture changes
        if (changeEyeTexturesOnDeath)
        {
            onDeath.AddListener(ChangeEyeTextures);
            Debug.Log("[KrakenHealth] Eye texture change listener added to onDeath event.");
        }

        // Don't invoke onHpChanged in Awake to prevent early event triggers
        // onHpChanged?.Invoke(currentHP, maxHP);
    }

    void Start()
    {
        // Subscribe to OnWin after all objects are initialized
        if (autoSubscribeToVictory)
        {
            onWinScript = FindFirstObjectByType<OnWin>();
            if (onWinScript != null)
            {
                onDeath.AddListener(onWinScript.TriggerVictory);
                Debug.Log("[KrakenHealth] OnWin script found and subscribed to death event.");
            }
            else
            {
                Debug.LogWarning("[KrakenHealth] OnWin script not found in scene.");
            }
        }

        // Validate eye setup
        ValidateEyeSetup();

        // Now it's safe to invoke HP changed event
        onHpChanged?.Invoke(currentHP, maxHP);
    }

    /// <summary>
    /// Validates the eye texture setup and logs any issues
    /// </summary>
    private void ValidateEyeSetup()
    {
        Debug.Log($"[KrakenHealth] Validating eye setup:");
        Debug.Log($"  - changeEyeTexturesOnDeath: {changeEyeTexturesOnDeath}");
        Debug.Log($"  - eyeGameObject: {(eyeGameObject != null ? eyeGameObject.name : "NULL")}");
        Debug.Log($"  - eyeRenderers count: {(eyeRenderers != null ? eyeRenderers.Length : 0)}");
        Debug.Log($"  - deadEyeBaseTexture: {(deadEyeBaseTexture != null ? deadEyeBaseTexture.name : "NULL")}");
        Debug.Log($"  - deadEyeNormalTexture: {(deadEyeNormalTexture != null ? deadEyeNormalTexture.name : "NULL")}");
        Debug.Log($"  - deadEyeSmoothnessMap: {(deadEyeSmoothnessMap != null ? deadEyeSmoothnessMap.name : "NULL")}");

        if (eyeRenderers != null)
        {
            for (int i = 0; i < eyeRenderers.Length; i++)
            {
                if (eyeRenderers[i] != null)
                {
                    Debug.Log($"  - Eye Renderer [{i}]: {eyeRenderers[i].name} (Material: {eyeRenderers[i].material?.name ?? "NULL"})");
                }
                else
                {
                    Debug.LogWarning($"  - Eye Renderer [{i}]: NULL");
                }
            }
        }
    }

    // --- public API ---

    /// <summary>Apply damage. Returns true if actually damaged.</summary>
    public bool TakeDamage(int amount, Vector3 hitPoint = default, Vector3 hitFrom = default)
    {
        if (amount <= 0 || invulnerable || currentHP <= 0 || isDead) return false;

        currentHP = Mathf.Max(0, currentHP - amount);
        onHpChanged?.Invoke(currentHP, maxHP);
        onDamaged?.Invoke();

        Debug.Log($"[KrakenHealth] Took {amount} damage. HP: {currentHP}/{maxHP}");

        // knockback (if we have a rigidbody and a direction)
        if (rb && knockback > 0f && hitFrom != Vector3.zero)
        {
            Vector3 dir = (transform.position - hitFrom).normalized;
            rb.AddForce(dir * knockback, ForceMode.VelocityChange);
        }

        if (currentHP <= 0)
        {
            Die();
        }
        else if (iFrameDuration > 0f)
        {
            StopCoroutine(nameof(Co_IFrames));
            StartCoroutine(nameof(Co_IFrames));
        }

        return true;
    }

    /// <summary>
    /// Public property to access the eye GameObject reference.
    /// </summary>
    public GameObject EyeGameObject => eyeGameObject;

    /// <summary>
    /// Changes the Kraken's eye textures to dead state. Called automatically on death if enabled.
    /// </summary>
    private void ChangeEyeTextures()
    {
        Debug.Log("[KrakenHealth] ChangeEyeTextures method called!");

        if (!changeEyeTexturesOnDeath)
        {
            Debug.LogWarning("[KrakenHealth] Eye texture changes disabled - changeEyeTexturesOnDeath is false");
            return;
        }

        if (eyeRenderers == null || eyeRenderers.Length == 0)
        {
            Debug.LogError("[KrakenHealth] No eye renderers assigned! Please assign eye renderers in the inspector.");
            return;
        }

        Debug.Log($"[KrakenHealth] Processing {eyeRenderers.Length} eye renderers...");

        for (int i = 0; i < eyeRenderers.Length; i++)
        {
            var eyeRenderer = eyeRenderers[i];
            if (!eyeRenderer)
            {
                Debug.LogWarning($"[KrakenHealth] Eye renderer [{i}] is null, skipping...");
                continue;
            }

            Debug.Log($"[KrakenHealth] Changing textures for eye renderer [{i}]: {eyeRenderer.name}");

            // Use separate MPB for eyes to avoid conflicts with death flash
            eyeRenderer.GetPropertyBlock(eyeMpb);

            // Log current shader properties
            var material = eyeRenderer.material;
            if (material != null)
            {
                Debug.Log($"  - Current material: {material.name}");
                Debug.Log($"  - Current shader: {material.shader.name}");
            }

            // Set new textures and properties
            if (deadEyeBaseTexture != null)
            {
                eyeMpb.SetTexture(BaseMapProp, deadEyeBaseTexture);
                Debug.Log($"  - Set base texture: {deadEyeBaseTexture.name}");
            }
            else
            {
                Debug.LogWarning("  - No dead eye base texture assigned");
            }

            if (deadEyeNormalTexture != null)
            {
                eyeMpb.SetTexture(NormalMapProp, deadEyeNormalTexture);
                Debug.Log($"  - Set normal texture: {deadEyeNormalTexture.name}");
            }

            if (deadEyeSmoothnessMap != null)
            {
                eyeMpb.SetTexture(SmoothnessMapProp, deadEyeSmoothnessMap);
                Debug.Log($"  - Set smoothness map: {deadEyeSmoothnessMap.name}");
            }

            // Set smoothness value
            eyeMpb.SetFloat(SmoothnessProp, deadEyeSmoothness);
            Debug.Log($"  - Set smoothness value: {deadEyeSmoothness}");

            // Apply the changes using separate MPB
            eyeRenderer.SetPropertyBlock(eyeMpb);
            Debug.Log($"  - Applied eye property block to {eyeRenderer.name}");
        }

        Debug.Log("[KrakenHealth] Eye texture change complete!");
    }

    /// <summary>
    /// Public method to manually test eye texture changes (for debugging)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void TestEyeTextureChange()
    {
        Debug.Log("[KrakenHealth] Manual eye texture test triggered!");
        ChangeEyeTextures();
    }

    // --- collisions ---

    void OnTriggerEnter(Collider other) { HandleHitCollider(other); }
    void OnCollisionEnter(Collision col) { HandleHitCollider(col.collider); }

    void HandleHitCollider(Collider other)
    {
        if (!IsDamaging(other) || isDead) return;

        // detect explicit damage value if the object has a component like IDamageDealer or DamagePayload
        int dmg = contactDamage;
        Vector3 from = other.bounds.center;

        // Common patterns:
        // 1) A component exposing a public int Damage
        // 2) A component implementing an interface with GetDamage()
        // We try both without allocations/reflection-heavy stuff.

        // Try pattern #1
        var comp = other.GetComponent<ComponentWithDamage>();
        if (comp) dmg = Mathf.Max(dmg, comp.Damage);

        // Try pattern #2 (interface)
        var dealer = other.GetComponent<IDamageDealer>();
        if (dealer != null) dmg = Mathf.Max(dmg, dealer.GetDamage());

        if (TakeDamage(dmg, other.ClosestPoint(transform.position), from))
        {
            // tell the projectile it hit (if it has a callback)
            var onHit = other.GetComponent<IOnHitTarget>();
            onHit?.OnHit(gameObject);

            if (destroyHitObject)
            {
                // avoid killing pooled objects that self-return; try a common pool pattern
                var pooled = other.GetComponent<IPooled>();
                if (pooled != null) pooled.Release();
                else Destroy(other.gameObject);
            }
        }
    }

    // --- helpers ---

    bool IsDamaging(Collider other)
    {
        // Layer check
        if (((1 << other.gameObject.layer) & damageLayers) == 0) return false;

        // Tag check (if any tags are defined)
        if (damageTags != null && damageTags.Length > 0)
        {
            bool tagOk = false;
            string tag = other.tag;
            for (int i = 0; i < damageTags.Length; i++)
            {
                if (!string.IsNullOrEmpty(damageTags[i]) && tag == damageTags[i])
                {
                    tagOk = true; break;
                }
            }
            if (!tagOk) return false;
        }

        return true;
    }

    void Die()
    {
        if (currentHP <= 0 && !isDead)
        {
            isDead = true; // Prevent multiple death calls
            Debug.Log("[KrakenHealth] Kraken is dying!");

            // stop i-frames/flash and start death flash
            invulnerable = false;
            StopAllCoroutines();

            // Start death flash before continuing with death sequence
            StartCoroutine(Co_DeathFlash());
        }
    }

    IEnumerator Co_DeathFlash()
    {
        if (flashOnDeath && rends.Count > 0)
        {
            // Create a list of renderers that are NOT eye renderers
            var nonEyeRenderers = new List<Renderer>();
            for (int i = 0; i < rends.Count; i++)
            {
                bool isEyeRenderer = false;
                if (eyeRenderers != null)
                {
                    for (int j = 0; j < eyeRenderers.Length; j++)
                    {
                        if (rends[i] == eyeRenderers[j])
                        {
                            isEyeRenderer = true;
                            break;
                        }
                    }
                }
                if (!isEyeRenderer)
                {
                    nonEyeRenderers.Add(rends[i]);
                }
            }

            // Cache original colors for non-eye renderers only
            var originals = new Color[nonEyeRenderers.Count];
            for (int i = 0; i < nonEyeRenderers.Count; i++)
            {
                var r = nonEyeRenderers[i];
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                originals[i] = mpb.GetColor(ColorProp);
            }

            // Flash to death color (non-eye renderers only)
            for (int i = 0; i < nonEyeRenderers.Count; i++)
            {
                var r = nonEyeRenderers[i];
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorProp, deathFlashColor);
                r.SetPropertyBlock(mpb);
            }

            // Wait for flash duration
            yield return new WaitForSeconds(deathFlashDuration);

            // Restore original colors (non-eye renderers only)
            for (int i = 0; i < nonEyeRenderers.Count; i++)
            {
                var r = nonEyeRenderers[i];
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorProp, originals[i]);
                r.SetPropertyBlock(mpb);
            }
        }

        // Continue with death sequence
        CompleteDeath();
    }

    void CompleteDeath()
    {
        Debug.Log("[KrakenHealth] Completing death sequence and triggering victory!");

        // disable gameplay bits
        if (disableOnDeath != null)
            for (int i = 0; i < disableOnDeath.Length; i++)
                if (disableOnDeath[i]) disableOnDeath[i].enabled = false;

        if (collidersOnBody != null)
            for (int i = 0; i < collidersOnBody.Length; i++)
                if (collidersOnBody[i]) collidersOnBody[i].enabled = false;

        // Trigger death event (this will call OnWin.TriggerVictory if subscribed)
        Debug.Log("[KrakenHealth] Invoking onDeath event...");
        onDeath?.Invoke();

        // trigger your upward exit animation
        if (krakenDeath) krakenDeath.TriggerDeath();
        else Destroy(gameObject); // fallback
    }

    IEnumerator Co_IFrames()
    {
        invulnerable = true;

        if (!flashOnHit || rends.Count == 0)
        {
            yield return new WaitForSeconds(iFrameDuration);
            invulnerable = false;
            yield break;
        }

        float t = 0f;
        bool toggle = false;

        // cache original colors once per renderer (assumes _BaseColor exists)
        var originals = new Color[rends.Count];
        for (int i = 0; i < rends.Count; i++)
        {
            var r = rends[i];
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            originals[i] = mpb.GetColor(ColorProp);
        }

        while (t < iFrameDuration)
        {
            t += Time.deltaTime;
            toggle = !toggle;

            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i]; if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorProp, toggle ? flashColor : originals[i]);
                r.SetPropertyBlock(mpb);
            }

            yield return new WaitForSeconds(flashInterval);
        }

        // restore
        for (int i = 0; i < rends.Count; i++)
        {
            var r = rends[i]; if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorProp, originals[i]);
            r.SetPropertyBlock(mpb);
        }

        invulnerable = false;
    }

    void RestoreRendererColors()
    {
        if (rends.Count == 0) return;
        for (int i = 0; i < rends.Count; i++)
        {
            var r = rends[i]; if (!r) continue;
            r.GetPropertyBlock(mpb);
            // If the material doesn't expose _BaseColor, this no-ops
            // (leaves material as-is).
            // We won't cache originals here—this runs only at end-of-life.
            // Optionally set full-white:
            // mpb.SetColor(ColorProp, Color.white);
            r.SetPropertyBlock(mpb);
        }
    }

    void OnDestroy()
    {
        // Clean up event subscription to prevent memory leaks
        if (onWinScript != null)
        {
            onDeath.RemoveListener(onWinScript.TriggerVictory);
        }

        if (changeEyeTexturesOnDeath)
        {
            onDeath.RemoveListener(ChangeEyeTextures);
        }
    }
}

/// <summary>
/// Optional helper: put this on projectiles to define damage.
/// </summary>
public class ComponentWithDamage : MonoBehaviour
{
    public int Damage = 10;
}

/// <summary>
/// Optional interface: implement on projectiles to supply damage dynamically.
/// </summary>
public interface IDamageDealer
{
    int GetDamage();
}

/// <summary>
/// Optional interface: projectiles can be notified they hit a target (to stop VFX, return to pool, etc.)
/// </summary>
public interface IOnHitTarget
{
    void OnHit(GameObject target);
}

/// <summary>
/// Optional interface for pooled objects with a Release() method (SimplePool/LeanPool style).
/// </summary>
public interface IPooled
{
    void Release();
}