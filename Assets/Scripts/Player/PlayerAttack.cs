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
                bomb.transform.SetPositionAndRotation(spawnPos, firePoint.rotation);
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