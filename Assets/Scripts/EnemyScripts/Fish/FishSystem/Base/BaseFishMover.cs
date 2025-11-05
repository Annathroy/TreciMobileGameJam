using UnityEngine;

[RequireComponent(typeof(PooledObject))]
public abstract class BaseFishMover : MonoBehaviour
{
    protected Vector3 origin;
    protected Vector3 dir;
    protected float speed;

    [Header("Lifetime/View")]
    [SerializeField] protected float maxLifetime = 20f;
    [SerializeField] protected float viewportMargin = 0.12f;

    protected float t;
    protected Camera cam;
    protected PooledObject po;

    public virtual void OnSpawned(Vector3 origin, Vector3 dir, float speed)
    {
        this.origin = origin;
        this.dir = dir.normalized;
        this.speed = speed;
        t = 0f;
        if (!cam) cam = Camera.main;
        if (!po) po = GetComponent<PooledObject>();
    }

    protected virtual void Update()
    {
        t += Time.deltaTime;
        TickMove(Time.deltaTime);
        if (t >= maxLifetime || IsOutsideView()) Despawn();
    }

    protected abstract void TickMove(float dt);

    protected void Despawn()
    {
        if (SharedFishPool.Instance) SharedFishPool.Instance.Despawn(gameObject, po ? po.SourcePrefab : null);
        else gameObject.SetActive(false);
    }

    protected bool IsOutsideView()
    {
        if (!cam) return false;
        var vp = cam.WorldToViewportPoint(transform.position);
        if (vp.z < 0f) return true;
        float min = -viewportMargin, max = 1f + viewportMargin;
        return (vp.x < min || vp.x > max || vp.y < min || vp.y > max);
    }
}
