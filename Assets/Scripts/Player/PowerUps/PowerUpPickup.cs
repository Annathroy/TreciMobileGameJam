using UnityEngine;

public enum PowerUpKind { DoubleShot, RapidFire, Scatter, Bomb, EightWay, Invulnerability }

[RequireComponent(typeof(Collider))]
public class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private PowerUpKind kind;
    [SerializeField] private float overrideDuration = -1f;
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float maxLifetime = 15f;
    [SerializeField] private float viewportMargin = 0.1f;

    [Header("Visual Display")]
    [SerializeField] private Texture2D[] powerUpTextures; // PNG files for each PowerUpKind
    [SerializeField] private Vector2 spriteSize = new Vector2(1f, 1f);
    [SerializeField] private Color spriteTint = Color.white;
    [SerializeField] private bool enablePulseAnimation = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.2f;

    private float lifetime;
    private Camera cam;
    private GameObject spriteQuad;
    private MeshRenderer spriteRenderer;
    private MaterialPropertyBlock materialPropertyBlock;
    private Material spriteMaterial;
    private Vector3 baseScale;

    private void Start()
    {
        if (!cam) cam = Camera.main;
        lifetime = 0f;
        
        CreateSpriteDisplay();
        ApplyPowerUpTexture();
    }

    private void CreateSpriteDisplay()
    {
        // Create a quad to display the PNG sprite
        spriteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        spriteQuad.name = $"PowerUpSprite_{kind}";
        spriteQuad.transform.SetParent(transform, false);
        
        // Position and orient for top-down camera view
        spriteQuad.transform.localPosition = Vector3.zero;
        spriteQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face upward
        spriteQuad.transform.localScale = new Vector3(spriteSize.x, spriteSize.y, 1f);
        baseScale = spriteQuad.transform.localScale;

        // Remove collider from the visual quad
        Collider quadCollider = spriteQuad.GetComponent<Collider>();
        if (quadCollider) Destroy(quadCollider);

        // Setup material and renderer
        spriteRenderer = spriteQuad.GetComponent<MeshRenderer>();
        
        // Try to find an appropriate shader (URP Unlit preferred)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!unlitShader) unlitShader = Shader.Find("Unlit/Texture");
        if (!unlitShader) unlitShader = Shader.Find("Sprites/Default");
        
        spriteMaterial = new Material(unlitShader)
        {
            color = spriteTint
        };
        
        spriteRenderer.material = spriteMaterial;
        spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        
        // Setup material property block for texture changes
        materialPropertyBlock = new MaterialPropertyBlock();
    }

    private void ApplyPowerUpTexture()
    {
        if (powerUpTextures == null || powerUpTextures.Length == 0) return;
        
        int textureIndex = (int)kind;
        if (textureIndex >= 0 && textureIndex < powerUpTextures.Length && powerUpTextures[textureIndex] != null)
        {
            Texture2D texture = powerUpTextures[textureIndex];
            
            // Apply texture using MaterialPropertyBlock (more efficient)
            materialPropertyBlock.SetTexture("_BaseMap", texture); // URP Unlit
            materialPropertyBlock.SetTexture("_MainTex", texture);  // Built-in fallback
            spriteRenderer.SetPropertyBlock(materialPropertyBlock);
        }
       
    }

    private void Update()
    {
        // Move towards bottom of screen (negative Z in top-down)
        transform.position += Vector3.back * moveSpeed * Time.deltaTime;
        
        lifetime += Time.deltaTime;
        
        // Pulse animation
        if (enablePulseAnimation && spriteQuad != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            spriteQuad.transform.localScale = baseScale * pulse;
        }
        
        // Destroy if too old or outside view
        if (lifetime >= maxLifetime || IsOutsideView())
        {
            Destroy(gameObject);
        }
    }

    private bool IsOutsideView()
    {
        if (!cam) return false;
        
        Vector3 viewportPoint = cam.WorldToViewportPoint(transform.position);
        
        // Check if behind camera
        if (viewportPoint.z < 0f) return true;
        
        // Check if outside viewport bounds with margin
        float min = -viewportMargin;
        float max = 1f + viewportMargin;
        
        return (viewportPoint.x < min || viewportPoint.x > max || 
                viewportPoint.y < min || viewportPoint.y > max);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.transform.root.gameObject;

        PowerUp p = kind switch
        {
            PowerUpKind.DoubleShot => (PowerUp)(player.GetComponent<DoubleShootingPowerUp>() ?? player.AddComponent<DoubleShootingPowerUp>()),
            PowerUpKind.RapidFire => (PowerUp)(player.GetComponent<RapidFirePowerUp>() ?? player.AddComponent<RapidFirePowerUp>()),
            PowerUpKind.Scatter => (PowerUp)(player.GetComponent<ScatterPowerUp>() ?? player.AddComponent<ScatterPowerUp>()),
            PowerUpKind.Bomb => (PowerUp)(player.GetComponent<BombPowerUp>() ?? player.AddComponent<BombPowerUp>()),
            PowerUpKind.EightWay => (PowerUp)(player.GetComponent<EightWayPowerUp>() ?? player.AddComponent<EightWayPowerUp>()),
            PowerUpKind.Invulnerability => (PowerUp)(player.GetComponent<InvulnerabilityPowerUp>() ?? player.AddComponent<InvulnerabilityPowerUp>()),
            _ => null
        };

        if (p == null) return;

        if (overrideDuration > 0f)
            p.SetDuration(overrideDuration);

        p.Activate(player);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Clean up material to prevent memory leaks
        if (spriteMaterial != null)
        {
            Destroy(spriteMaterial);
        }
    }

    // Helper method to change texture at runtime
    public void SetCustomTexture(Texture2D texture)
    {
        if (materialPropertyBlock != null && spriteRenderer != null && texture != null)
        {
            materialPropertyBlock.SetTexture("_BaseMap", texture);
            materialPropertyBlock.SetTexture("_MainTex", texture);
            spriteRenderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    // Helper method to update visual settings
    public void UpdateVisualSettings(Vector2 newSize, Color newTint)
    {
        spriteSize = newSize;
        spriteTint = newTint;
        
        if (spriteQuad != null)
        {
            spriteQuad.transform.localScale = new Vector3(spriteSize.x, spriteSize.y, 1f);
            baseScale = spriteQuad.transform.localScale;
        }
        
        if (spriteMaterial != null)
        {
            spriteMaterial.color = spriteTint;
        }
    }
}


