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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Invulnerability")]
    [SerializeField] private float invulnDuration = 0.1f;
    [SerializeField] private float flashInterval = 0.06f;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 0.0f;
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private float deathSFXVolume = 0.9f;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged;  // (current, max)
    public UnityEvent onDeath;

    bool invulnerable = false;
    Coroutine invulnCo;
    readonly List<Renderer> renderers = new();

    void Awake()
    {
        hp = Mathf.Max(1, maxHP);
        GetComponentsInChildren(true, renderers);
        onHealthChanged?.Invoke(hp, maxHP);

        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {gameObject.name} initialized with {hp}/{maxHP} HP");
    }

    // ---------- Public API ----------
    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || invulnerable || hp <= 0) return;

        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {gameObject.name} taking {amount} damage. HP: {hp} -> {hp - amount}");

        hp = Mathf.Max(0, hp - amount);
        onHealthChanged?.Invoke(hp, maxHP);

        if (hp <= 0)
        {
            Die();
            return;
        }

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

    // ---------- Optional automatic intake from Projectile ----------
    void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {gameObject.name} trigger entered by: {other.name} (Tag: {other.tag})");

        if (!acceptProjectileTriggers || hp <= 0) 
        {
            if (enableDebugLogs)
                Debug.Log($"[EnemyHealth] Ignoring trigger - acceptProjectileTriggers: {acceptProjectileTriggers}, hp: {hp}");
            return;
        }

        var proj = other.GetComponent<Projectile>() ?? other.GetComponentInParent<Projectile>();
        if (proj == null) 
        {
            if (enableDebugLogs)
                Debug.Log($"[EnemyHealth] No Projectile component found on {other.name}");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] Found projectile with damage: {proj.Damage}, destroyOnHit: {proj.DestroyOnHit}");

        ApplyDamage(proj.Damage);
        if (proj.DestroyOnHit) proj.ForceDespawn();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {gameObject.name} collision with: {collision.gameObject.name}");

        if (!acceptProjectileCollisions || hp <= 0) 
        {
            if (enableDebugLogs)
                Debug.Log($"[EnemyHealth] Ignoring collision - acceptProjectileCollisions: {acceptProjectileCollisions}, hp: {hp}");
            return;
        }

        var proj = collision.collider.GetComponent<Projectile>() ??
                   collision.collider.GetComponentInParent<Projectile>();
        if (proj == null) 
        {
            if (enableDebugLogs)
                Debug.Log($"[EnemyHealth] No Projectile component found on {collision.gameObject.name}");
            return;
        }

        ApplyDamage(proj.Damage);
        if (proj.DestroyOnHit) proj.ForceDespawn();
    }

    // ---------- Death ----------
    void Die()
    {
        if (enableDebugLogs)
            Debug.Log($"[EnemyHealth] {gameObject.name} died!");

        if (invulnCo != null) StopCoroutine(invulnCo);
        SetVisible(true);
        onDeath?.Invoke();

        if (deathVFX)
        {
            var vfx = Instantiate(deathVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }
        if (deathSFX)
            AudioSource.PlayClipAtPoint(deathSFX, transform.position, deathSFXVolume);

        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        else gameObject.SetActive(false);
    }

    // ---------- Flash ----------
    IEnumerator InvulnerabilityFlash(float duration, float interval)
    {
        invulnerable = true;
        float t = 0f;
        bool vis = true;

        while (t < duration)
        {
            vis = !vis;
            SetVisible(vis);
            yield return new WaitForSeconds(interval);
            t += interval;
        }

        SetVisible(true);
        invulnerable = false;
        invulnCo = null;
    }

    void SetVisible(bool on)
    {
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i]) renderers[i].enabled = on;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        if (hp > 0) hp = Mathf.Clamp(hp, 0, maxHP);
        flashInterval = Mathf.Max(0.01f, flashInterval);
        invulnDuration = Mathf.Max(0f, invulnDuration);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }

    void Reset()
    {
        // Set reasonable defaults when component is added
        maxHP = 5;
        hp = maxHP;
        acceptProjectileTriggers = true;
        acceptProjectileCollisions = false;
        invulnDuration = 0.1f;
        flashInterval = 0.06f;
        destroyOnDeath = false;
        enableDebugLogs = false;
    }
#endif
}
