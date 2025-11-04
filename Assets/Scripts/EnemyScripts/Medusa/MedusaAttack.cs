using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyUnit : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float speed = 3.5f;
    [SerializeField] float arriveEpsilon = 0.05f;

    float fixedX;
    float targetZ;
    bool hasTarget;

    public bool Arrived => hasTarget && Mathf.Abs(transform.position.z - targetZ) <= arriveEpsilon;

    void Awake()
    {
        fixedX = transform.position.x; // lock X from spawn
    }

    public void SetFixedX(float x)
    {
        fixedX = x;
        var p = transform.position;
        transform.position = new Vector3(fixedX, p.y, p.z);
    }

    public void SetTargetZ(float z)
    {
        targetZ = z;
        hasTarget = true;
    }

    void FixedUpdate()
    {
        if (!hasTarget || Arrived) return;

        var p = transform.position;
        float dir = Mathf.Sign(targetZ - p.z);
        float step = speed * Time.fixedDeltaTime * dir;

        float newZ = p.z + step;
        // clamp overshoot
        if ((dir > 0f && newZ > targetZ) || (dir < 0f && newZ < targetZ))
            newZ = targetZ;

        transform.position = new Vector3(fixedX, p.y, newZ);
    }
}

