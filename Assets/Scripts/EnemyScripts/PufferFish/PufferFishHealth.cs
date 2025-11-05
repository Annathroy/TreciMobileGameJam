using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Optimized health system for PufferFish enemies.
/// Supports pooling, damage detection, visual feedback, and animation triggers.
/// </summary>
[DisallowMultipleComponent]
public class PufferFishHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    [Header("Damage Detection")]
    [Tooltip("Tag for player projectiles that can damage this enemy")]
    [SerializeField] private string playerProjectileTag = "PlayerProjectile";
    [Tooltip("Tag for player contact damage (e.g., 'Player')")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private bool destroyProjectileOnHit = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Invincibility Frames")]
    [SerializeField] private float invincibilityDuration = 0.25f;
    [SerializeField] private float flashInterval = 0.05f;
    [SerializeField] private Color damageFlashColor = Color.red;

    [Header("Death Behavior")]
    [SerializeField] private bool usePooling = true;
    [SerializeField] private float deathDelay = 0.1f;
    
    [Header("Animation Parameters")]
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Die";

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged; // current, max
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    // Cached components for performance
    private Animator animator;
    private Renderer[] renderers;
    private Material[] originalMaterials;
    private Color[] originalColors;
    
    // State management
    private bool isInvincible;
    private bool isDead;
    private Coroutine invincibilityCoroutine;

    // Static property IDs for performance
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    #region Unity Lifecycle

    void Awake()
    {
        InitializeComponents();
        ResetHealth();
    }

    void OnEnable()
    {
        // Reset state when object is pooled/reused
        if (isDead) ResetHealth();
        isInvincible = false;
        
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
            invincibilityCoroutine = null;
        }
        
        RestoreOriginalColors();
    }

    void OnDisable()
    {
        // Clean up coroutines
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
            invincibilityCoroutine = null;
        }
        isInvincible = false;
    }

    #endregion

    #region Initialization

    void InitializeComponents()
    {
        // Cache animator
        animator = GetComponent<Animator>();
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        // Cache renderers for flash effect
        renderers = GetComponentsInChildren<Renderer>();
        CacheOriginalMaterials();
    }

    void CacheOriginalMaterials()
    {
        if (renderers == null || renderers.Length == 0) return;

        var materialsList = new List<Material>();
        var colorsList = new List<Color>();

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materialsList.Add(materials[i]);
                
                // Try different color properties
                if (materials[i].HasProperty(ColorProperty))
                    colorsList.Add(materials[i].GetColor(ColorProperty));
                else if (materials[i].HasProperty(BaseColorProperty))
                    colorsList.Add(materials[i].GetColor(BaseColorProperty));
                else
                    colorsList.Add(Color.white);
            }
        }

        originalMaterials = materialsList.ToArray();
        originalColors = colorsList.ToArray();
    }

    void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        if (onHealthChanged != null)
            onHealthChanged.Invoke(currentHealth, maxHealth);
    }

    #endregion

    #region Damage System

    public void TakeDamage(int damage)
    {
        // Early exits for performance
        if (damage <= 0 || isDead || isInvincible) return;

        if (enableDebugLogs)
            Debug.Log("[PufferFishHealth] Taking " + damage + " damage. Health: " + currentHealth + " -> " + (currentHealth - damage));

        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // Trigger hit animation
        TriggerAnimation(hitTrigger);
        
        // Visual feedback
        if (invincibilityCoroutine != null)
            StopCoroutine(invincibilityCoroutine);
        invincibilityCoroutine = StartCoroutine(InvincibilityFrames());

        // Fire events
        if (onHealthChanged != null)
            onHealthChanged.Invoke(currentHealth, maxHealth);
        if (onDamaged != null)
            onDamaged.Invoke();

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void InstantKill()
    {
        if (isDead) return;
        
        currentHealth = 0;
        if (onHealthChanged != null)
            onHealthChanged.Invoke(currentHealth, maxHealth);
        Die();
    }

    void Die()
    {
        if (isDead) return;
        
        if (enableDebugLogs)
            Debug.Log("[PufferFishHealth] PufferFish died!");

        isDead = true;
        isInvincible = true; // Prevent further damage during death sequence
        
        // Trigger death animation
        TriggerAnimation(deathTrigger);
        
        // Fire death event
        if (onDeath != null)
            onDeath.Invoke();
        
        // Handle death with delay
        StartCoroutine(HandleDeath());
    }

    IEnumerator HandleDeath()
    {
        // Wait for death delay (allows animation to play)
        if (deathDelay > 0f)
            yield return new WaitForSeconds(deathDelay);
        
        // Return to pool or destroy
        if (usePooling)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Collision Detection

    void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
            Debug.Log("[PufferFishHealth] Trigger entered by: " + other.name + " with tag: " + other.tag);
        
        HandleCollision(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (enableDebugLogs)
            Debug.Log("[PufferFishHealth] Collision with: " + collision.gameObject.name + " with tag: " + collision.gameObject.tag);
        
        HandleCollision(collision.gameObject);
    }

    void HandleCollision(GameObject other)
    {
        if (isDead || isInvincible) 
        {
            if (enableDebugLogs)
                Debug.Log("[PufferFishHealth] Ignoring collision - isDead: " + isDead + ", isInvincible: " + isInvincible);
            return;
        }

        // Player projectile damage
        if (!string.IsNullOrEmpty(playerProjectileTag) && other.CompareTag(playerProjectileTag))
        {
            if (enableDebugLogs)
                Debug.Log("[PufferFishHealth] Hit by player projectile: " + other.name);
                
            int damage = GetProjectileDamage(other);
            TakeDamage(damage);
            
            if (destroyProjectileOnHit)
                DestroyProjectile(other);
            
            return;
        }

        // Player contact damage
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            if (enableDebugLogs)
                Debug.Log("[PufferFishHealth] Contact damage from player: " + other.name);
                
            TakeDamage(contactDamage);
        }
    }

    int GetProjectileDamage(GameObject projectile)
    {
        // Try to get damage from IDamageDealer interface
        var damageDealer = projectile.GetComponent<PufferFishDamageDealer>();
        if (damageDealer != null)
        {
            int dealerDamage = damageDealer.GetDamage();
            if (enableDebugLogs)
                Debug.Log("[PufferFishHealth] IDamageDealer damage: " + dealerDamage);
            return Mathf.Max(1, dealerDamage);
        }

        // Try to get damage from ComponentWithDamage
        var damageComponent = projectile.GetComponent<PufferFishDamageComponent>();
        if (damageComponent != null)
        {
            if (enableDebugLogs)
                Debug.Log("[PufferFishHealth] ComponentWithDamage damage: " + damageComponent.Damage);
            return Mathf.Max(1, damageComponent.Damage);
        }

        // Fallback to default damage
        if (enableDebugLogs)
            Debug.Log("[PufferFishHealth] Using fallback damage: " + projectileDamage);
        return projectileDamage;
    }

    void DestroyProjectile(GameObject projectile)
    {
        // Check if projectile uses pooling
        var pooledObject = projectile.GetComponent<PufferFishPooledObject>();
        if (pooledObject != null)
        {
            pooledObject.ReturnToPool();
        }
        else
        {
            // Try ObjectPool approach (common in Unity)
            var poolable = projectile.GetComponent<PufferFishPoolable>();
            if (poolable != null)
            {
                poolable.ReturnToPool();
            }
            else
            {
                // Standard destruction
                Destroy(projectile);
            }
        }
    }

    #endregion

    #region Visual Effects

    IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        float elapsed = 0f;
        bool isFlashing = false;

        while (elapsed < invincibilityDuration)
        {
            elapsed += Time.deltaTime;
            
            // Toggle flash effect
            if (elapsed % flashInterval < Time.deltaTime)
            {
                isFlashing = !isFlashing;
                SetFlashEffect(isFlashing);
            }
            
            yield return null;
        }

        // Restore original appearance
        SetFlashEffect(false);
        isInvincible = false;
        invincibilityCoroutine = null;
    }

    void SetFlashEffect(bool flash)
    {
        if (originalMaterials == null) return;

        for (int index = 0; index < originalMaterials.Length; index++)
        {
            var material = originalMaterials[index];
            if (material == null) continue;

            Color targetColor = flash ? damageFlashColor : originalColors[index];
            
            if (material.HasProperty(ColorProperty))
                material.SetColor(ColorProperty, targetColor);
            else if (material.HasProperty(BaseColorProperty))
                material.SetColor(BaseColorProperty, targetColor);
        }
    }

    void RestoreOriginalColors()
    {
        SetFlashEffect(false);
    }

    #endregion

    #region Animation

    void TriggerAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    #endregion

    #region Public Properties

    public int CurrentHealth { get { return currentHealth; } }
    public int MaxHealth { get { return maxHealth; } }
    public bool IsDead { get { return isDead; } }
    public bool IsInvincible { get { return isInvincible; } }
    public float HealthPercentage { get { return maxHealth > 0 ? (float)currentHealth / maxHealth : 0f; } }

    #endregion

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        if (currentHealth <= 0) currentHealth = maxHealth;
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        flashInterval = Mathf.Max(0.01f, flashInterval);
        deathDelay = Mathf.Max(0f, deathDelay);
    }

    void Reset()
    {
        // Set reasonable defaults
        maxHealth = 3;
        currentHealth = maxHealth;
        playerProjectileTag = "PlayerProjectile";
        playerTag = "Player";
        projectileDamage = 1;
        contactDamage = 1;
        invincibilityDuration = 0.25f;
        flashInterval = 0.05f;
        damageFlashColor = Color.red;
        usePooling = true;
        deathDelay = 0.1f;
        hitTrigger = "Hit";
        deathTrigger = "Die";
        enableDebugLogs = false;
    }
#endif
}

// Interface for projectiles that can deal damage (renamed to avoid conflicts)
public interface IPufferFishDamageDealer
{
    int GetDamage();
}

// Component for simple damage values on projectiles (renamed to avoid conflicts)
public class PufferFishDamageComponent : MonoBehaviour
{
    public int Damage = 1;
}

// Implementation class for damage dealer interface
public class PufferFishDamageDealer : MonoBehaviour, IPufferFishDamageDealer
{
    [SerializeField] private int damage = 1;
    
    public int GetDamage()
    {
        return damage;
    }
}

// Interface for pooled objects (renamed to avoid conflicts)
public interface IPufferFishPooledObject
{
    void ReturnToPool();
}

// Alternative interface for pooled objects (renamed to avoid conflicts)
public interface IPufferFishPoolable
{
    void ReturnToPool();
}

// Implementation classes for pooling interfaces
public class PufferFishPooledObject : MonoBehaviour, IPufferFishPooledObject
{
    public virtual void ReturnToPool()
    {
        gameObject.SetActive(false);
    }
}

public class PufferFishPoolable : MonoBehaviour, IPufferFishPoolable
{
    public virtual void ReturnToPool()
    {
        gameObject.SetActive(false);
    }
}