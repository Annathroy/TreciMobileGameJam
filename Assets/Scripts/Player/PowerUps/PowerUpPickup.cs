using UnityEngine;

public class PowerUpPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Get the PowerUp component and activate it
            PowerUp powerUp = GetComponent<PowerUp>();
            if (powerUp != null)
            {
                powerUp.Activate(other.gameObject);
            }

            // Destroy the power-up object after activation
            Destroy(gameObject);
        }
    }
}