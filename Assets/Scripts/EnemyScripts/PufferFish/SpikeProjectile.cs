using System.Collections;
using UnityEngine;

public class SpikeProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] float speed = 22f;
    [SerializeField] float lifeTime = 3f;
    [SerializeField] float skin = 0.02f;

    [Header("Collision")]
    [SerializeField] float collisionGrace = 0.15f;     // ignore hits right after launch
    [SerializeField] LayerMask hitMask = ~0;           // what the spike can hit (set in Inspector)

    SimplePool originPool;
    Vector3 dir;
    float dieAt, enableHitAt;
    Collider[] ignoreThese; // self/puffer colliders to temporarily ignore

    public void Launch(Vector3 position, Vector3 direction, SimplePool pool, Collider[] ignore = null)
    {
        transform.position = position;
        dir = direction.normalized;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        originPool = pool;
        dieAt = Time.time + lifeTime;
        enableHitAt = Time.time + collisionGrace;
        ignoreThese = ignore;

        // If we have our own collider, ignore self vs puffer for the grace period
        var myCol = GetComponent<Collider>();
        if (myCol && ignoreThese != null)
            StartCoroutine(ReenableCollisionsSoon(myCol, ignoreThese, collisionGrace));
    }

    IEnumerator ReenableCollisionsSoon(Collider mine, Collider[] others, float delay)
    {
        foreach (var c in others) if (c) Physics.IgnoreCollision(mine, c, true);
        yield return new WaitForSeconds(delay);
        foreach (var c in others) if (c) Physics.IgnoreCollision(mine, c, false);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Time.time >= dieAt) { ReturnToPool(); return; }

        Vector3 step = dir * speed * dt;

        // Raycast to catch hits; ignore during grace window
        if (Time.time >= enableHitAt)
        {
            if (Physics.Raycast(transform.position, dir, out var hit, step.magnitude + skin, hitMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point - dir * skin;
                ReturnToPool();
                return;
            }
        }

        transform.position += step;
    }

    void OnTriggerEnter(Collider _) { if (Time.time >= enableHitAt) ReturnToPool(); }
    void OnCollisionEnter(Collision _) { if (Time.time >= enableHitAt) ReturnToPool(); }

    void ReturnToPool()
    {
        if (originPool != null) originPool.Return(gameObject);
        else gameObject.SetActive(false);
    }
}
