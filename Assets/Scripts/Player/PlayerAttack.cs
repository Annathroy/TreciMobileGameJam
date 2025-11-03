using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private ObjectPool projectilePool;
    [SerializeField] private ObjectPool bombPool; // Pool for bomb projectiles
    [SerializeField] private Transform firePoint;
    [SerializeField] private float baseFireRate = 0.2f;

    private float fireRate;
    private float fireCooldown;

    private bool isDoubleShooting = false;
    private bool isScatterShot = false;
    private bool isBombShooting = false;

    private void Start()
    {
        fireRate = baseFireRate;
    }

    // Update is called once per frame
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
        if (isBombShooting)
        {
            // Bomb shooting: Fire a bomb projectile
            GameObject bomb = bombPool.GetObject();
            bomb.transform.position = firePoint.position;
            bomb.transform.rotation = firePoint.rotation;
        }
        else if (isDoubleShooting)
        {
            // Double shooting: Fire two projectiles
            GameObject projectile1 = projectilePool.GetObject();
            projectile1.transform.position = firePoint.position + Vector3.left * 0.2f;
            projectile1.transform.rotation = firePoint.rotation;

            GameObject projectile2 = projectilePool.GetObject();
            projectile2.transform.position = firePoint.position + Vector3.right * 0.2f;
            projectile2.transform.rotation = firePoint.rotation;
        }
        else if (isScatterShot)
        {
            // Scatter shot: Fire projectiles in a spread (forward, 45 degrees downward, -45 degrees downward)
            GameObject projectile1 = projectilePool.GetObject();
            projectile1.transform.position = firePoint.position;
            projectile1.transform.rotation = firePoint.rotation;

            GameObject projectile2 = projectilePool.GetObject();
            projectile2.transform.position = firePoint.position;
            projectile2.transform.rotation = firePoint.rotation * Quaternion.Euler(0, 45, 0); // 45 degrees to the right

            GameObject projectile3 = projectilePool.GetObject();
            projectile3.transform.position = firePoint.position;
            projectile3.transform.rotation = firePoint.rotation * Quaternion.Euler(0, -45, 0); // 45 degrees to the left

            Debug.Log("Scatter shot fired three projectiles.");
        }
        else
        {
            // Normal shooting: Fire one projectile
            GameObject projectile = projectilePool.GetObject();
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = firePoint.rotation;
        }
    }

    public void EnableDoubleShooting()
    {
        isDoubleShooting = true;

        // Reset the duration timer for the latest stack
        CancelInvoke(nameof(DeactivateDoubleShooting));
        Debug.Log($"Canceling previous DeactivateDoubleShooting and rescheduling for {gameObject.name}.");
        Invoke(nameof(DeactivateDoubleShooting), 5f); // Example duration of 5 seconds
    }

    public void DisableDoubleShooting()
    {
        Debug.Log("Disabling double shooting.");
        isDoubleShooting = false;
    }

    public void EnableRapidFire()
    {
        fireRate = baseFireRate / 2; // Double the fire rate

        // Reset the duration timer for the latest stack
        CancelInvoke(nameof(DeactivateRapidFire));
        Debug.Log($"Canceling previous DeactivateRapidFire and rescheduling for {gameObject.name}.");
        Invoke(nameof(DeactivateRapidFire), 5f); // Example duration of 5 seconds
    }

    public void DisableRapidFire()
    {
        Debug.Log("Disabling rapid fire. Fire rate reset to base.");
        fireRate = baseFireRate;
    }

    public void EnableScatterShot()
    {
        isScatterShot = true;

        // Reset the duration timer for the latest stack
        CancelInvoke(nameof(DeactivateScatterShot));
        Debug.Log($"Canceling previous DeactivateScatterShot and rescheduling for {gameObject.name}.");
        Invoke(nameof(DeactivateScatterShot), 5f); // Example duration of 5 seconds
    }

    public void DisableScatterShot()
    {
        Debug.Log("Disabling scatter shot. Scatter shot flag set to false.");
        isScatterShot = false;
    }

    public void EnableBombShooting()
    {
        isBombShooting = true;

        // Reset the duration timer for the latest stack
        CancelInvoke(nameof(DeactivateBombShooting));
        Debug.Log($"Canceling previous DeactivateBombShooting and rescheduling for {gameObject.name}.");
        Invoke(nameof(DeactivateBombShooting), 5f); // Example duration of 5 seconds
    }

    public void DisableBombShooting()
    {
        Debug.Log("Disabling bomb shooting.");
        isBombShooting = false;
    }

    private void DeactivateDoubleShooting()
    {
        DisableDoubleShooting();
    }

    private void DeactivateRapidFire()
    {
        DisableRapidFire();
    }

    private void DeactivateScatterShot()
    {
        DisableScatterShot();
    }

    private void DeactivateBombShooting()
    {
        DisableBombShooting();
    }
}
