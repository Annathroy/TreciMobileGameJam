using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerDeath : MonoBehaviour
{
    [Header("Death Settings")]
    [SerializeField] private bool disablePlayerOnDeath = true;
    [SerializeField] private float deathDelay = 0.1f;
    
    [Header("Explosion Effect")]
    [SerializeField] private Sprite explosionSprite;
    [SerializeField] private float explosionLifetime = 1f;
    [SerializeField] private float explosionScale = 1f;
    [SerializeField] private bool useWorldSpace = false;
    
    [Header("Audio")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathSoundVolume = 1f;
    
    [Header("Components to Disable")]
    [SerializeField] private Behaviour[] componentsToDisable;
    [SerializeField] private Collider[] collidersToDisable;
    
    [Header("Events")]
    public UnityEvent onPlayerDeath;
    public UnityEvent onExplosionComplete;
    
    private Canvas canvas;
    private Camera mainCamera;
    private bool isDead;
    
    private void Awake()
    {
        // Cache references
        canvas = FindAnyObjectByType<Canvas>();
        mainCamera = Camera.main;
        
        // Auto-populate components to disable if not set
        if (componentsToDisable == null || componentsToDisable.Length == 0)
        {
            var playerController = GetComponent<MonoBehaviour>();
            if (playerController != null)
            {
                componentsToDisable = new Behaviour[] { playerController };
            }
        }
        
        if (collidersToDisable == null || collidersToDisable.Length == 0)
        {
            collidersToDisable = GetComponents<Collider>();
        }
    }
    
    /// <summary>
    /// Main death function to be called from PlayerHealth script
    /// </summary>
    public void TriggerPlayerDeath()
    {
        if (isDead) return;
        
        isDead = true;
        
        // Capture position before potentially disabling the GameObject
        Vector3 deathPosition = transform.position;
        
        // Play death sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, deathPosition, deathSoundVolume);
        }
        
        // Trigger death event
        onPlayerDeath?.Invoke();
        
        // Start death sequence with correct DelayedRunner usage
        if (deathDelay > 0f)
        {
            DelayedRunner.Run(() => StartCoroutine(ExecuteDeathSequence(deathPosition)), deathDelay);
        }
        else
        {
            StartCoroutine(ExecuteDeathSequence(deathPosition));
        }
    }
    
    /// <summary>
    /// Alternative death function with custom explosion sprite
    /// </summary>
    public void TriggerPlayerDeath(Sprite customExplosionSprite)
    {
        Sprite originalSprite = explosionSprite;
        explosionSprite = customExplosionSprite;
        TriggerPlayerDeath();
        explosionSprite = originalSprite;
    }
    
    /// <summary>
    /// Death sequence coroutine
    /// </summary>
    private IEnumerator ExecuteDeathSequence(Vector3 deathPosition)
    {
        // Disable player components
        if (disablePlayerOnDeath)
        {
            DisablePlayerComponents();
        }
        
        // Create explosion effect
        if (explosionSprite != null)
        {
            yield return StartCoroutine(CreateExplosionEffect(deathPosition));
        }
        
        // Trigger completion event
        onExplosionComplete?.Invoke();
        
        // Disable the GameObject if specified
        if (disablePlayerOnDeath)
        {
            gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Creates the explosion effect at the specified world position
    /// </summary>
    private IEnumerator CreateExplosionEffect(Vector3 worldPosition)
    {
        if (explosionSprite == null) yield break;
        
        GameObject explosionObject;
        
        if (useWorldSpace)
        {
            // Create explosion in world space
            explosionObject = CreateWorldSpaceExplosion(worldPosition);
        }
        else
        {
            // Create explosion on UI Canvas
            explosionObject = CreateCanvasExplosion(worldPosition);
        }
        
        if (explosionObject != null)
        {
            // Wait for explosion duration
            yield return new WaitForSeconds(explosionLifetime);
            
            // Destroy explosion
            if (explosionObject != null)
            {
                Destroy(explosionObject);
            }
        }
    }
    
    /// <summary>
    /// Creates explosion effect in world space using SpriteRenderer
    /// </summary>
    private GameObject CreateWorldSpaceExplosion(Vector3 worldPosition)
    {
        GameObject explosionGO = new GameObject("PlayerExplosion");
        explosionGO.transform.position = worldPosition;
        explosionGO.transform.localScale = Vector3.one * explosionScale;
        
        SpriteRenderer spriteRenderer = explosionGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = explosionSprite;
        spriteRenderer.sortingOrder = 100; // Ensure it renders on top
        
        return explosionGO;
    }
    
    /// <summary>
    /// Creates explosion effect on UI Canvas
    /// </summary>
    private GameObject CreateCanvasExplosion(Vector3 worldPosition)
    {
        if (canvas == null) return null;
        
        GameObject explosionGO = new GameObject("PlayerExplosionUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        explosionGO.transform.SetParent(canvas.transform, false);
        
        var image = explosionGO.GetComponent<UnityEngine.UI.Image>();
        image.sprite = explosionSprite;
        image.SetNativeSize();
        
        var rectTransform = image.rectTransform;
        rectTransform.localScale = Vector3.one * explosionScale;
        
        // Convert world position to canvas position
        Vector2 screenPosition = mainCamera != null ? mainCamera.WorldToScreenPoint(worldPosition) : Vector2.zero;
        
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPosition, null, out Vector2 localPosition);
            rectTransform.anchoredPosition = localPosition;
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPosition, canvas.worldCamera, out Vector2 localPosition);
            rectTransform.anchoredPosition = localPosition;
        }
        
        return explosionGO;
    }
    
    /// <summary>
    /// Disables player components and colliders
    /// </summary>
    private void DisablePlayerComponents()
    {
        // Disable specified components
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
            {
                if (component != null)
                {
                    component.enabled = false;
                }
            }
        }
        
        // Disable specified colliders
        if (collidersToDisable != null)
        {
            foreach (var collider in collidersToDisable)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Resets the death state (useful for respawning)
    /// </summary>
    public void ResetDeath()
    {
        isDead = false;
        
        // Re-enable components
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }
        }
        
        // Re-enable colliders
        if (collidersToDisable != null)
        {
            foreach (var collider in collidersToDisable)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
        }
        
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Public properties for external access
    /// </summary>
    public bool IsDead => isDead;
    public Sprite ExplosionSprite { get => explosionSprite; set => explosionSprite = value; }
    public float ExplosionLifetime { get => explosionLifetime; set => explosionLifetime = value; }
    public float ExplosionScale { get => explosionScale; set => explosionScale = value; }
}
