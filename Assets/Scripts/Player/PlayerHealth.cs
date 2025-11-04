using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI; // <-- for UI hearts

public class PlayerHealth : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 4; // hard-cap at 4
    [SerializeField] private int lives;        // initialized in Awake to maxLives

    [Header("Damage Sources (by Tag)")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string enemyProjectileTag = "EnemyProjectile";

    [Header("Damage")]
    [SerializeField] private int damageFromEnemy = 1;
    [SerializeField] private int damageFromProjectile = 1;
    [SerializeField] private bool destroyProjectileOnHit = true;

    [Header("Invulnerability")]
    [SerializeField] private float invulnDuration = 1.5f;
    [SerializeField] private float flashInterval = 0.08f;

    [Header("UI Hearts (exactly 4)")]
    [Tooltip("Assign 4 UI Image objects (left→right).")]
    [SerializeField] private Image[] heartImages = new Image[4];

    [Header("Events")]
    public UnityEvent<int, int> onLivesChanged; // (current, max)
    public UnityEvent onDeath;

    // ---- internals ----
    private const int MAX_CAP = 4;
    private bool invulnerable = false;
    private List<Renderer> renderers = new List<Renderer>();
    private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private Coroutine invulnRoutine;

    private void Awake()
    {
        // cap at 4 hearts
        maxLives = Mathf.Clamp(maxLives, 1, MAX_CAP);
        lives = Mathf.Clamp(maxLives, 1, MAX_CAP);

        // cache all renderers (supports sprites and 3D meshes)
        GetComponentsInChildren(true, renderers);
        GetComponentsInChildren(true, spriteRenderers);

        RefreshHearts();
        onLivesChanged?.Invoke(lives, maxLives);
    }

    // PUBLIC: direct damage API (use this from other scripts if needed)
    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || invulnerable || lives <= 0) return;

        lives = Mathf.Max(0, lives - amount);
        RefreshHearts();
        onLivesChanged?.Invoke(lives, maxLives);

        if (lives <= 0)
        {
            Die();
            return;
        }

        // start invuln+flash
        if (invulnRoutine != null) StopCoroutine(invulnRoutine);
        invulnRoutine = StartCoroutine(InvulnerabilityFlash(invulnDuration, flashInterval));
    }

    public void GrantInvulnerability(float seconds)
    {
        if (seconds <= 0f || lives <= 0) return;
        if (invulnRoutine != null) StopCoroutine(invulnRoutine);
        invulnRoutine = StartCoroutine(InvulnerabilityFlash(seconds, flashInterval));
    }

    private void Die()
    {
        if (invulnRoutine != null) StopCoroutine(invulnRoutine);
        SetVisible(true);
        onDeath?.Invoke();
        // Optional: disable controls, play animation, etc.
    }

    // ---- collision handling (3D) ----
    private void OnTriggerEnter(Collider other)
    {
        if (invulnerable || lives <= 0) return;

        if (other.CompareTag(enemyTag))
        {
            ApplyDamage(damageFromEnemy);
        }
        else if (other.CompareTag(enemyProjectileTag))
        {
            ApplyDamage(damageFromProjectile);
            if (destroyProjectileOnHit) Destroy(other.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (invulnerable || lives <= 0) return;

        var other = collision.collider;
        if (other.CompareTag(enemyTag))
        {
            ApplyDamage(damageFromEnemy);
        }
        else if (other.CompareTag(enemyProjectileTag))
        {
            ApplyDamage(damageFromProjectile);
            if (destroyProjectileOnHit) Destroy(other.gameObject);
        }
    }

    // ---- invulnerability & flashing ----
    private IEnumerator InvulnerabilityFlash(float duration, float interval)
    {
        invulnerable = true;

        float t = 0f;
        bool visible = true;

        while (t < duration)
        {
            visible = !visible;
            SetVisible(visible);

            yield return new WaitForSeconds(interval);
            t += interval;
        }

        SetVisible(true);
        invulnerable = false;
        invulnRoutine = null;
    }

    private void SetVisible(bool on)
    {
        // For Mesh/SkinnedMesh renderers
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].enabled = on;

        // For SpriteRenderers (2D in 3D projects)
        for (int i = 0; i < spriteRenderers.Count; i++)
            if (spriteRenderers[i] != null) spriteRenderers[i].enabled = on;
    }

    // ---- Hearts UI ----
    private void RefreshHearts()
    {
        // Safety: ensure array length is 4; nulls are tolerated
        int clampedLives = Mathf.Clamp(lives, 0, MAX_CAP);
        for (int i = 0; i < MAX_CAP; i++)
        {
            var img = (heartImages != null && i < heartImages.Length) ? heartImages[i] : null;
            if (img == null) continue;

            // Toggle enabled for performance; or use img.gameObject.SetActive(...)
            img.enabled = (i < clampedLives);
        }
    }

    // ---- optional helpers ----
    public void AddLife(int amount)
    {
        if (amount <= 0 || lives <= 0) return;
        int prev = lives;
        lives = Mathf.Clamp(lives + amount, 0, MAX_CAP);
        if (lives != prev)
        {
            RefreshHearts();
            onLivesChanged?.Invoke(lives, maxLives);
        }
    }

    public void SetLives(int value)
    {
        int prev = lives;
        lives = Mathf.Clamp(value, 0, MAX_CAP);
        if (lives != prev)
        {
            RefreshHearts();
            onLivesChanged?.Invoke(lives, maxLives);
        }
    }

    public int CurrentLives => lives;
    public int MaxLives => maxLives;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // keep caps in editor too
        maxLives = Mathf.Clamp(maxLives, 1, MAX_CAP);
        if (lives > 0) lives = Mathf.Clamp(lives, 0, MAX_CAP);

        // warn if hearts not assigned
        if (heartImages == null || heartImages.Length < MAX_CAP)
            Debug.LogWarning("[PlayerHealth] Assign 4 heart Images (left→right) in the Inspector.");
    }

    private void Reset()
    {
        if (!CompareTag("Player"))
            Debug.LogWarning("[PlayerHealth] Consider tagging this GameObject as 'Player'.");
    }
#endif
}
