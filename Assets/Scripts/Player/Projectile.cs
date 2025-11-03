using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;

    private float timeAlive;

    private void OnEnable()
    {
        timeAlive = 0f;
    }

    private void Update()
    {
        // Move the projectile in the direction it is facing
        transform.position += transform.forward * speed * Time.deltaTime;

        // Track the time the projectile has been alive

        if (timeAlive >= lifetime)
        {
            gameObject.SetActive(false);
        }
    }
}
