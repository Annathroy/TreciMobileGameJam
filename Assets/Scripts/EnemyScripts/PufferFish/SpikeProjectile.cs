using System.Collections;
using UnityEngine;

public class SpikeProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 22f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float skin = 0.02f;

    [Header("Collision")]
    [SerializeField] private float collisionGrace = 0.15f;     // ignore hits right after launch
    [SerializeField] private LayerMask hitMask = ~0;           // what the spike can hit
    [SerializeField] private int damage = 1;                   // damage applied to PlayerHealth

    private SimplePool originPool;
    private Vector3 dir;
    private float dieAt;
    private float enableHitAt;
    private Collider[] ignoreThese; // colliders to ignore temporarily

    public void Launch(Vector3 position, Vector3 direction, SimplePool pool, Collider[] ignore = null)
    {
        transform.position = position;
        dir = direction.normalized;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        originPool = pool;
        dieAt = Time.time + lifeTime;
        enableHitAt = Time.time + collisionGrace;
        ignoreThese = ignore;

        var myCol = GetComponent<Collider>();
        if (myCol && ignoreThese != null)
            StartCoroutine(ReenableCollisionsSoon(myCol, ignoreThese, collisionGrace));
    }

    private IEnumerator ReenableCollisionsSoon(Collider mine, Collider[] others, float delay)
    {
        foreach (var c in others)
            if (c) Physics.IgnoreCollision(mine, c, true);

        yield return new WaitForSeconds(delay);

        foreach (var c in others)
            if (c) Physics.IgnoreCollision(mine, c, false);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (Time.time >= dieAt)
        {
            ReturnToPool();
            return;
        }

        Vector3 step = dir * speed * dt;

        // Raycast to catch hits; ignore during grace window
        if (Time.time >= enableHitAt)
        {
            if (Physics.Raycast(transform.position, dir, out var hit, step.magnitude + skin, hitMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point - dir * skin;

                // Try to apply damage if target has PlayerHealth
                var health = hit.collider.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    health.ApplyDamage(damage);
                }

                ReturnToPool();
                return;
            }
        }

        transform.position += step;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < enableHitAt) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            health.ApplyDamage(damage);
        }

        ReturnToPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < enableHitAt) return;

        var health = collision.collider.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            health.ApplyDamage(damage);
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (originPool != null)
            originPool.Return(gameObject);
        else
            gameObject.SetActive(false);
    }
}
