using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private ObjectPool projectilePool;
    [SerializeField] private ObjectPool bombPool;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float baseFireRate = 0.2f;
    // In your player's attack script
    private PlayerIntro playerIntro;

    private float fireRate;
    private float fireCooldown;

    private bool scatterShot = false;
    private bool bombShooting = false;

    private int bulletLines = 1;

    private bool eightWay = false;

    private void Start()
    {
        fireRate = baseFireRate;
        playerIntro = GetComponent<PlayerIntro>();
    }

    void Update()
    {
        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            Shoot();
            fireCooldown = fireRate;
        }
    }

    private void Shoot()
    {
        // play shooting SFX once per attack

        for (int i = 0; i < bulletLines; i++)
        {
            float xOffset = (i - (bulletLines - 1) / 2f) * 0.4f;
            Vector3 spawnPos = firePoint.position + Vector3.right * xOffset;

            if (bombShooting)
            {
                var bomb = bombPool.GetObject();

                // Make sure bomb faces the same direction as regular projectiles
                Quaternion bombRotation = firePoint.rotation * Quaternion.Euler(90, 0, 0);
                bomb.transform.SetPositionAndRotation(spawnPos, firePoint.rotation);
                
                // If the bomb has a movement script, try to set its direction explicitly
                var bombMovement = bomb.GetComponent<MonoBehaviour>();
                if (bombMovement != null)
                {
                    // Try to find and set movement direction using reflection or specific component
                    // This is a fallback - you should replace this with the actual component name
                    
                    // Option 1: If bomb uses Rigidbody for movement
                    var rb = bomb.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = firePoint.forward * 10f; // Adjust speed as needed
                    }
                    
                    // Option 2: If bomb has a specific movement component (replace 'YourBombScript' with actual name)
                    // var bombScript = bomb.GetComponent<YourBombScript>();
                    // if (bombScript != null)
                    // {
                    //     bombScript.SetDirection(firePoint.forward);
                    // }
                }
            }
            else if (eightWay)
            {
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * 45f;
                    var p = projectilePool.GetObject();
                    p.transform.SetPositionAndRotation(
                        spawnPos,
                        firePoint.rotation * Quaternion.Euler(0f, angle, 0f)
                    );
                }
            }
            else if (scatterShot)
            {
                var p1 = projectilePool.GetObject();
                p1.transform.SetPositionAndRotation(spawnPos, firePoint.rotation);

                var p2 = projectilePool.GetObject();
                p2.transform.SetPositionAndRotation(spawnPos, firePoint.rotation * Quaternion.Euler(0, 45, 0));

                var p3 = projectilePool.GetObject();
                p3.transform.SetPositionAndRotation(spawnPos, firePoint.rotation * Quaternion.Euler(0, -45, 0));
            }
            else
            {
                var projectile = projectilePool.GetObject();
                projectile.transform.SetPositionAndRotation(spawnPos, firePoint.rotation);
            }
        }
    }


    // ---- setters ----
    public void SetEightWay(bool on) => eightWay = on;  // NEW
    // existing setters:
    public void SetScatterShot(bool on) => scatterShot = on;
    public void SetBombShooting(bool on) => bombShooting = on;
    public void SetBulletLines(int lines) => bulletLines = Mathf.Clamp(lines, 1, 10);
    public void SetFireRateMultiplier(float mult)
    {
        mult = Mathf.Max(0.01f, mult);
        fireRate = Mathf.Max(0.01f, baseFireRate * mult);
    }
}