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
    static readonly int ColorProp = Shader.PropertyToID("_BaseColor"); // URP/Lit & Unlit use _BaseColor
    private OnWin onWinScript;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!krakenDeath) krakenDeath = GetComponent<KrakenDeath>();

        // cache renderers (children too)
        GetComponentsInChildren(true, rends);
        mpb = new MaterialPropertyBlock();

        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 1, maxHP);

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

        // Now it's safe to invoke HP changed event
        onHpChanged?.Invoke(currentHP, maxHP);
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
            // Cache original colors
            var originals = new Color[rends.Count];
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                originals[i] = mpb.GetColor(ColorProp);
            }

            // Flash to death color
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorProp, deathFlashColor);
                r.SetPropertyBlock(mpb);
            }

            // Wait for flash duration
            yield return new WaitForSeconds(deathFlashDuration);

            // Restore original colors
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
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