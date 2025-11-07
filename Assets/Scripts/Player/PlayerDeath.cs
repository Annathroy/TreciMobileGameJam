using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerDeath : MonoBehaviour
{
    [Header("Death Settings")]
    [SerializeField] private bool disablePlayerOnDeath = true;
    [SerializeField] private float deathDelay = 0.1f;
    [SerializeField] private bool pauseAfterExplosion = true;           // NEW: open pause after explosion
    [SerializeField] private bool useUnscaledForExplosionWait = false;  // NEW: wait ignores timescale if true

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

    [Header("Refs")]
    [SerializeField] private PauseController pauseController; // NEW: hook up in Inspector or auto-find

    private Canvas canvas;
    private Camera mainCamera;
    private bool isDead;

    private void Awake()
    {
        // Cache references
        canvas = FindAnyObjectByType<Canvas>();
        mainCamera = Camera.main;

        // Try to find PauseController if not assigned
        if (pauseController == null)
            pauseController = FindAnyObjectByType<PauseController>();

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

        Vector3 deathPosition = transform.position;

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, deathPosition, deathSoundVolume);

        onPlayerDeath?.Invoke();

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
        // Disable player components first so they stop interacting
        if (disablePlayerOnDeath)
            DisablePlayerComponents();

        // Explosion effect
        if (explosionSprite != null)
            yield return StartCoroutine(CreateExplosionEffect(deathPosition));

        // Open pause AFTER explosion (as requested)
        if (pauseAfterExplosion)
        {
            if (pauseController != null)
            {
                // Ensure panel shows even if something else fiddled with timescale
                if (Time.timeScale == 0f) Time.timeScale = 1f; // normalize briefly
                pauseController.OnPauseButton();               // this sets Time.timeScale = 0 and shows panel
            }
            else
            {
                Debug.LogWarning("[PlayerDeath] PauseController not found; cannot open pause menu after death.");
            }
        }

        // Notify listeners that the explosion phase is done
        onExplosionComplete?.Invoke();

        // Finally disable player object if desired
        if (disablePlayerOnDeath)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Creates the explosion effect at the specified world position
    /// </summary>
    private IEnumerator CreateExplosionEffect(Vector3 worldPosition)
    {
        if (explosionSprite == null) yield break;

        GameObject explosionObject = useWorldSpace
            ? CreateWorldSpaceExplosion(worldPosition)
            : CreateCanvasExplosion(worldPosition);

        if (explosionObject != null)
        {
            if (explosionLifetime > 0f)
            {
                if (useUnscaledForExplosionWait)
                    yield return new WaitForSecondsRealtime(explosionLifetime);
                else
                    yield return new WaitForSeconds(explosionLifetime);
            }

            if (explosionObject != null)
                Destroy(explosionObject);
        }
    }

    private GameObject CreateWorldSpaceExplosion(Vector3 worldPosition)
    {
        GameObject explosionGO = new GameObject("PlayerExplosion");
        explosionGO.transform.position = worldPosition;
        explosionGO.transform.localScale = Vector3.one * explosionScale;

        SpriteRenderer sr = explosionGO.AddComponent<SpriteRenderer>();
        sr.sprite = explosionSprite;
        sr.sortingOrder = 100; // on top

        return explosionGO;
    }

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

        Vector2 screenPosition = mainCamera != null ? mainCamera.WorldToScreenPoint(worldPosition) : Vector2.zero;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPosition, null, out Vector2 localPos);
            rectTransform.anchoredPosition = localPos;
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPosition, canvas.worldCamera, out Vector2 localPos);
            rectTransform.anchoredPosition = localPos;
        }

        return explosionGO;
    }

    private void DisablePlayerComponents()
    {
        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
                if (component != null) component.enabled = false;
        }

        if (collidersToDisable != null)
        {
            foreach (var col in collidersToDisable)
                if (col != null) col.enabled = false;
        }
    }

    public void ResetDeath()
    {
        isDead = false;

        if (componentsToDisable != null)
        {
            foreach (var component in componentsToDisable)
                if (component != null) component.enabled = true;
        }

        if (collidersToDisable != null)
        {
            foreach (var col in collidersToDisable)
                if (col != null) col.enabled = true;
        }

        gameObject.SetActive(true);
    }

    public bool IsDead => isDead;
    public Sprite ExplosionSprite { get => explosionSprite; set => explosionSprite = value; }
    public float ExplosionLifetime { get => explosionLifetime; set => explosionLifetime = value; }
    public float ExplosionScale { get => explosionScale; set => explosionScale = value; }
}
