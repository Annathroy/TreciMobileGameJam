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
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
        timeAlive += Time.deltaTime;

        if (timeAlive >= lifetime)
        {
            gameObject.SetActive(false);
        }
    }
}
