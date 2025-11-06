using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHP = 5;
    [SerializeField] private int hp;

    [Header("Take damage from projectiles automatically")]
    [Tooltip("If on, enemy will read damage from any Projectile it touches (trigger).")]
    [SerializeField] private bool acceptProjectileTriggers = true;
    [Tooltip("If on, enemy will also read damage from collisions (non-trigger).")]
    [SerializeField] private bool acceptProjectileCollisions = false;

    [Header("Invulnerability")]
    [SerializeField] private float invulnDuration = 0.1f;
    [SerializeField] private float flashInterval = 0.06f;

    [Header("Death VFX/SFX")]
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private float deathSFXVolume = 0.9f;

    [Header("Pooling")]
    [Tooltip("If true, will return to SharedFishPool (if available) instead of Destroy/disable.")]
    [SerializeField] private bool useSharedPool = true;
    [Tooltip("Only return to pool if death was caused by a projectile.")]
    [SerializeField] private bool onlyPoolWhenKilledByProjectile = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged;  // (current, max)
    public UnityEvent onDeath;

    // ---- internals ----
    bool invulnerable = false;
    Coroutine invulnCo;
    readonly List<Renderer> renderers = new();
    bool lastHitWasProjectile = false;           // tracks kill source
    PooledObject po;                             // for pool return hint

    void Awake()
    {
        CacheRenderers();
        po = GetComponent<PooledObject>();       // present if this enemy is pooled
        hp = Mathf.Max(1, maxHP);
        onHealthChanged?.Invoke(hp, maxHP);
    }

    // If we're using a pool, this object will be reactivated; reset state here.
    void OnEnable()
    {
        // If already initialized once, top-up hp on reuse
        if (hp <= 0 || hp > maxHP) hp = maxHP;
        SetVisible(true);
        invulnerable = false;
        lastHitWasProjectile = false;
        onHealthChanged?.Invoke(hp, maxHP);
    }

    void OnDisable()
    {
        if (invulnCo != null) { StopCoroutine(invulnCo); invulnCo = null; }
        invulnerable = false;
    }

    // ---------- Public API ----------
    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || invulnerable || hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        onHealthChanged?.Invoke(hp, maxHP);

        if (hp <= 0) { Die(); return; }

        if (invulnCo != null) StopCoroutine(invulnCo);
        invulnCo = StartCoroutine(InvulnerabilityFlash(invulnDuration, flashInterval));
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || hp <= 0) return;
        int prev = hp;
        hp = Mathf.Min(maxHP, hp + amount);
        if (hp != prev) onHealthChanged?.Invoke(hp, maxHP);
    }

    public void Kill() => ApplyDamage(999999);

    public int CurrentHP => hp;
    public int MaxHP => maxHP;
    public bool IsDead => hp <= 0;

    // ---------- Automatic intake from Projectile ----------
    void OnTriggerEnter(Collider other)
    {
        if (!acceptProjectileTriggers || hp <= 0) return;

        var proj = other.GetComponent<Projectile>() ?? other.GetComponentInParent<Projectile>();
        if (proj == null) return;

        lastHitWasProjectile = true;
        ApplyDamage(proj.Damage);
        if (proj.DestroyOnHit) proj.ForceDespawn();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!acceptProjectileCollisions || hp <= 0) return;

        var proj = collision.collider.GetComponent<Projectile>() ??
                   collision.collider.GetComponentInParent<Projectile>();
        if (proj == null) return;

        lastHitWasProjectile = true;
        ApplyDamage(proj.Damage);
        if (proj.DestroyOnHit) proj.ForceDespawn();
    }

    // ---------- Death ----------
    void Die()
    {
        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {name} died (proj:{lastHitWasProjectile}).");

        if (invulnCo != null) { StopCoroutine(invulnCo); invulnCo = null; }
        SetVisible(true);
        onDeath?.Invoke();

        // VFX / SFX first
        if (deathVFX)
        {
            var vfx = Instantiate(deathVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }
        if (deathSFX)
            AudioSource.PlayClipAtPoint(deathSFX, transform.position, deathSFXVolume);

        // --- Manual handling for PufferFish ---
        // Tag it in Unity as "PufferFish" or ensure its name contains "Puffer"
        if (CompareTag("PufferFish") || name.Contains("PufferFish"))
        {
            gameObject.SetActive(false);
            return;
        }

        // --- normal pooled or disable fallback ---
        bool shouldPool = useSharedPool && SharedFishPool.Instance != null &&
                          (!onlyPoolWhenKilledByProjectile || lastHitWasProjectile);

        if (shouldPool)
        {
            hp = maxHP;
            SharedFishPool.Instance.Despawn(gameObject, po ? po.SourcePrefab : null);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // ---------- Flash ----------
    IEnumerator InvulnerabilityFlash(float duration, float interval)
    {
        invulnerable = true;
        float until = Time.time + duration;
        bool vis = true;

        while (Time.time < until)
        {
            vis = !vis;
            SetVisible(vis);
            yield return new WaitForSeconds(interval);
        }

        SetVisible(true);
        invulnerable = false;
        invulnCo = null;
    }

    void SetVisible(bool on)
    {
        if (renderers.Count == 0) CacheRenderers();
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i]) renderers[i].enabled = on;
    }

    void CacheRenderers()
    {
        renderers.Clear();
        GetComponentsInChildren(true, renderers);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        hp = Mathf.Clamp(hp <= 0 ? maxHP : hp, 0, maxHP);
        flashInterval = Mathf.Max(0.01f, flashInterval);
        invulnDuration = Mathf.Max(0f, invulnDuration);
    }

    void Reset()
    {
        maxHP = 5;
        hp = maxHP;
        acceptProjectileTriggers = true;
        acceptProjectileCollisions = false;
        invulnDuration = 0.1f;
        flashInterval = 0.06f;
        useSharedPool = true;
        onlyPoolWhenKilledByProjectile = true;
        enableDebugLogs = false;
    }
#endif
}
