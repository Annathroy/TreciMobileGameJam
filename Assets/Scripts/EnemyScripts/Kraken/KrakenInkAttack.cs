using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class KrakenInkAttack : MonoBehaviour
{
    [Header("Ink Projectile Settings")]
    [SerializeField] private GameObject inkProjectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
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
        if (projectileSpawnPoint == null || inkProjectilePrefab == null) return;

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
        
        InkProjectile inkProjectile = projectile.AddComponent<InkProjectile>();
        inkProjectile.Initialize(projectileSpeed, this);
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
    private KrakenInkAttack krakenInkAttack;

    public void Initialize(float speed, KrakenInkAttack krakenInkAttack)
    {
        this.speed = speed;
        this.krakenInkAttack = krakenInkAttack;

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
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // PlayerHealth will automatically handle damage due to "EnemyProjectile" tag
            // Create ink splatter effect
            krakenInkAttack.CreateInkSplatter();
            
            // Destroy the projectile
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
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
            
            // Destroy the projectile
            Destroy(gameObject);
        }
    }
}
