using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Lives")]
    [SerializeField] private int maxLives = 5;
    [SerializeField] private int lives; // initialized in Awake to maxLives

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

    [Header("Events")]
    public UnityEvent<int, int> onLivesChanged; // (current, max)
    public UnityEvent onDeath;

    // ---- internals ----
    private bool invulnerable = false;
    private List<Renderer> renderers = new List<Renderer>();
    private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private Coroutine invulnRoutine;

    private void Awake()
    {
        lives = Mathf.Max(1, maxLives);

        // cache all renderers (supports sprites and 3D meshes)
        GetComponentsInChildren(true, renderers);
        GetComponentsInChildren(true, spriteRenderers);

        // initial event fire
        onLivesChanged?.Invoke(lives, maxLives);
    }

    // PUBLIC: direct damage API (use this from other scripts if needed)
    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || invulnerable || lives <= 0) return;

        lives = Mathf.Max(0, lives - amount);
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

        // restart the same flashing coroutine you already use on damage
        if (invulnRoutine != null) StopCoroutine(invulnRoutine);
        invulnRoutine = StartCoroutine(InvulnerabilityFlash(seconds, flashInterval));
    }


    private void Die()
    {
        // stop flashing if running
        if (invulnRoutine != null) StopCoroutine(invulnRoutine);
        SetVisible(true);

        onDeath?.Invoke();

        // Disable player controls/attack here if needed
        // e.g., GetComponent<PlayerAttack>()?.enabled = false;

        // Optional: Destroy(gameObject);
        // Leave it to you to handle respawn / game over.
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
        {
            if (renderers[i] != null) renderers[i].enabled = on;
        }
        // For SpriteRenderers (2D in 3D projects)
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] != null) spriteRenderers[i].enabled = on;
        }
    }

    // ---- optional helpers ----
    public void AddLife(int amount)
    {
        if (amount <= 0 || lives <= 0) return;
        int prev = lives;
        lives = Mathf.Clamp(lives + amount, 0, maxLives);
        if (lives != prev) onLivesChanged?.Invoke(lives, maxLives);
    }

    public int CurrentLives => lives;
    public int MaxLives => maxLives;

#if UNITY_EDITOR
    private void Reset()
    {
        // If you add this to the Player, auto-tag suggestion
        if (!CompareTag("Player"))
        {
            Debug.LogWarning("[PlayerHealth] Consider tagging this GameObject as 'Player' for consistency.");
        }
    }
#endif
}

