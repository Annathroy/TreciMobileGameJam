using System.Collections;
using UnityEngine;

public class SpikeProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 22f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float skin = 0.02f;

    [Header("Arming / Collision")]
    [SerializeField] private float collisionGrace = 0.15f; // ignore hits right after launch (time)
    [SerializeField] private float armDistance = 0.5f;      // must travel at least this far before it can damage
    [SerializeField] private LayerMask hitMask = ~0;        // what the spike can hit
    [SerializeField] private int damage = 1;                // damage applied to PlayerHealth

    private SimplePool originPool;
    private Vector3 dir;
    private float dieAt;
    private float enableHitAt;
    private Collider[] ignoreThese; // colliders to ignore temporarily
    private Vector3 launchPos;
    private float traveled;

    private const string PLAYER_TAG = "Player"; // only interact with this tag

    public void Launch(Vector3 position, Vector3 direction, SimplePool pool, Collider[] ignore = null)
    {
        transform.position = position;
        dir = direction.normalized;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        originPool = pool;
        dieAt = Time.time + lifeTime;
        enableHitAt = Time.time + collisionGrace;
        ignoreThese = ignore;

        launchPos = position;
        traveled = 0f;

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
        float stepLen = step.magnitude;

        // Move + track distance
        transform.position += step;
        traveled = Vector3.Distance(launchPos, transform.position);

        // Not armed yet? Skip hit logic.
        bool armed = (Time.time >= enableHitAt) && (traveled >= armDistance);
        if (!armed) return;

        // Raycast from current position backwards over the step to detect missed hits
        // We cast FROM previous position toward dir to cover this frame's path.
        Vector3 rayOrigin = transform.position - step;
        if (Physics.Raycast(rayOrigin, dir, out var hit, stepLen + skin, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Only damage the Player
            if (hit.collider.CompareTag(PLAYER_TAG))
            {
                var health = hit.collider.GetComponentInParent<PlayerHealth>();
                if (health != null) health.ApplyDamage(damage);
            }

            transform.position = hit.point - dir * skin;
            ReturnToPool();
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore until armed
        bool armed = (Time.time >= enableHitAt) && (traveled >= armDistance);
        if (!armed) return;

        if (!other.CompareTag(PLAYER_TAG)) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.ApplyDamage(damage);

        ReturnToPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore until armed
        bool armed = (Time.time >= enableHitAt) && (traveled >= armDistance);
        if (!armed) return;

        if (!collision.collider.CompareTag(PLAYER_TAG)) return;

        var health = collision.collider.GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.ApplyDamage(damage);

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
