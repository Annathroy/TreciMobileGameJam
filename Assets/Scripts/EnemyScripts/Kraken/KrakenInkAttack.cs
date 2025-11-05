using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class KrakenInkAttack : MonoBehaviour
{
    [Header("Ink Projectile Settings")]
    [SerializeField] private GameObject inkProjectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileLifetime = 5f; // Add lifetime setting
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private Transform projectileSpawnPoint;

    [Header("Ink Splatter Effect")]
    [SerializeField] private GameObject inkSplatterPrefab;
    [SerializeField] private Canvas overlayCanvas; // Reference to UI canvas
    [SerializeField] private float splatterDuration = 2f;
    [SerializeField] private float splatterFadeSpeed = 1f;

    private Camera mainCamera;
    private float nextAttackTime;

    private void Start()
    {
        mainCamera = Camera.main;

        // Try to find canvas if not assigned
        if (overlayCanvas == null)
        {
            overlayCanvas = FindFirstObjectByType<Canvas>();
            if (overlayCanvas == null)
            {
                Debug.LogError("No Canvas found! Please assign a UI Canvas for ink splatters.");
            }
        }
    }

    private void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            ShootInk();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void ShootInk()
    {
        if (projectileSpawnPoint == null || inkProjectilePrefab == null)
        {
            Debug.LogWarning("[KrakenInkAttack] Cannot shoot ink - missing spawn point or prefab!");
            return;
        }

        Debug.Log("[KrakenInkAttack] Shooting ink projectile...");
        GameObject projectile = Instantiate(inkProjectilePrefab, projectileSpawnPoint.position, Quaternion.identity);

        // Ensure projectile has the correct tag for PlayerHealth to recognize it
        projectile.tag = "EnemyProjectile";

        // Ensure projectile has required components
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = projectile.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        Collider col = projectile.GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphereCol = projectile.AddComponent<SphereCollider>();
            sphereCol.radius = 0.5f;
        }

        // Get Kraken's colliders to ignore initially
        Collider[] krakenColliders = GetComponentsInParent<Collider>();

        InkProjectile inkProjectile = projectile.AddComponent<InkProjectile>();
        inkProjectile.Initialize(projectileSpeed, projectileLifetime, this, krakenColliders); // Pass Kraken colliders to ignore
    }

    public void CreateInkSplatter()
    {
        if (inkSplatterPrefab == null || overlayCanvas == null) return;

        // Create splatter as UI element
        GameObject splatter = Instantiate(inkSplatterPrefab, overlayCanvas.transform);
        RectTransform rectTransform = splatter.GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            // Get canvas rect for proper positioning
            RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Position ink splatter in bottom quarter of screen (below 3/4)
            float minY = -canvasHeight * 0.375f; // Bottom edge
            float maxY = -canvasHeight * 0.125f; // 3/4 down from top

            rectTransform.anchoredPosition = new Vector2(
                Random.Range(-canvasWidth * 0.4f, canvasWidth * 0.4f), // Random X position
                Random.Range(minY, maxY) // Bottom quarter of screen
            );

            // Random rotation
            rectTransform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));

            // Random scale
            float randomScale = Random.Range(0.8f, 1.2f);
            rectTransform.localScale = new Vector3(randomScale, randomScale, 1);
        }

        StartCoroutine(FadeOutSplatter(splatter));
    }

    private IEnumerator FadeOutSplatter(GameObject splatter)
    {
        Image splatterImage = splatter.GetComponent<Image>();
        if (splatterImage == null)
        {
            Destroy(splatter);
            yield break;
        }

        // Wait for duration before starting fade
        yield return new WaitForSeconds(splatterDuration);

        // Fade out
        Color color = splatterImage.color;
        while (color.a > 0)
        {
            color.a -= Time.deltaTime * splatterFadeSpeed;
            splatterImage.color = color;
            yield return null;
        }

        Destroy(splatter);
    }
}

// Separate class for ink projectile behavior
public class InkProjectile : MonoBehaviour
{
    private float speed;
    private float destroyTime; // Time when projectile should be destroyed
    private float collisionGraceTime; // Time before collisions are enabled
    private KrakenInkAttack krakenInkAttack;
    private bool canCollide = false;

    public void Initialize(float speed, float lifetime, KrakenInkAttack krakenInkAttack, Collider[] ignoreColliders = null)
    {
        this.speed = speed;
        this.krakenInkAttack = krakenInkAttack;
        this.destroyTime = Time.time + lifetime; // Set destruction time
        this.collisionGraceTime = Time.time + 0.2f; // 0.2 second grace period

        Debug.Log("[InkProjectile] Projectile initialized with " + lifetime + "s lifetime");

        // Ignore collisions with Kraken initially
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null && ignoreColliders != null)
        {
            foreach (Collider krakenCol in ignoreColliders)
            {
                if (krakenCol != null)
                {
                    Physics.IgnoreCollision(myCollider, krakenCol, true);
                }
            }

            // Re-enable collisions after grace period
            StartCoroutine(EnableCollisionsAfterDelay(myCollider, ignoreColliders, 0.2f));
        }

        // Find direction to player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
            }
        }
        else
        {
            Debug.LogWarning("[InkProjectile] No player found!");
        }
    }

    private IEnumerator EnableCollisionsAfterDelay(Collider myCollider, Collider[] ignoreColliders, float delay)
    {
        yield return new WaitForSeconds(delay);
        canCollide = true;

        // Re-enable collisions with Kraken after grace period
        if (myCollider != null && ignoreColliders != null)
        {
            foreach (Collider krakenCol in ignoreColliders)
            {
                if (krakenCol != null)
                {
                    Physics.IgnoreCollision(myCollider, krakenCol, false);
                }
            }
        }
    }

    private void Update()
    {
        // Enable collision after grace period
        if (!canCollide && Time.time >= collisionGraceTime)
        {
            canCollide = true;
        }

        // Destroy projectile when lifetime expires
        if (Time.time >= destroyTime)
        {
            Debug.Log("[InkProjectile] Projectile destroyed due to lifetime expiration");
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!canCollide) return; // Ignore collisions during grace period

        Debug.Log("[InkProjectile] Collision with: " + collision.gameObject.name + " (Tag: " + collision.gameObject.tag + ")");

        // Check for various enemy projectile types to pass through
        if (ShouldPassThrough(collision.gameObject))
        {
            Debug.Log("[InkProjectile] Passing through: " + collision.gameObject.name);
            return; // Don't destroy, just pass through
        }

        // Handle player collision specially
        if (collision.gameObject.CompareTag("Player"))
        {
            // PlayerHealth will automatically handle damage due to "EnemyProjectile" tag
            // Create ink splatter effect
            krakenInkAttack.CreateInkSplatter();
        }

        // Destroy projectile on collision with non-projectile objects (player, walls, terrain, etc.)
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canCollide) return; // Ignore triggers during grace period

        Debug.Log("[InkProjectile] Trigger with: " + other.gameObject.name + " (Tag: " + other.gameObject.tag + ")");

        // Check for various enemy projectile types to pass through
        if (ShouldPassThrough(other.gameObject))
        {
            Debug.Log("[InkProjectile] Passing through trigger: " + other.gameObject.name);
            return; // Don't destroy, just pass through
        }

        // Handle player trigger specially
        if (other.CompareTag("Player"))
        {
            // Apply damage directly if using trigger colliders
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyDamage(1);
            }

            // Create ink splatter effect
            krakenInkAttack.CreateInkSplatter();
        }

        // Destroy projectile on trigger with non-projectile objects (player, walls, terrain, etc.)
        Destroy(gameObject);
    }

    private bool ShouldPassThrough(GameObject obj)
    {
        // Pass through objects with "EnemyProjectile" tag
        if (obj.CompareTag("EnemyProjectile"))
            return true;

        // Pass through SpikeProjectile components (PufferFish spikes)
        if (obj.GetComponent<SpikeProjectile>() != null)
            return true;

        // Pass through other enemy types but not their colliders used for movement
        if (obj.GetComponent<PufferFish>() != null)
            return true;

        // Pass through any other InkProjectile
        if (obj.GetComponent<InkProjectile>() != null)
            return true;

        // Check if it's any kind of enemy (has EnemyHealth component)
        if (obj.GetComponent<EnemyHealth>() != null)
            return true;

        // Check for Kraken components
        if (obj.GetComponent<KrakenHealth>() != null || obj.GetComponent<KrakenInkAttack>() != null)
            return true;

        return false;
    }
}