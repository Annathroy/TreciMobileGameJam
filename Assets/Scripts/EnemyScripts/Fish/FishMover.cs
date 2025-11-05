using UnityEngine;

[RequireComponent(typeof(PooledObject))]
public class FishMover : MonoBehaviour
{
    // movement params
    private Vector3 origin;
    private Vector3 dir;
    private Vector3 lateral; // perpendicular to dir on plane
    private float speed;
    private float amp;
    private float freq;
    private float t;
    private float phase;
    private bool planeXY;

    // screen/cleanup
    private Camera cam;
    [SerializeField] private float viewportMargin = 0.12f; // despawn after fully outside this padded rect
    [SerializeField] private float maxLifetime = 20f; // safety

    private PooledObject po;

    public void OnSpawned(Vector3 origin, Vector3 direction, Vector3 lateral, float speed, float amp, float freq, bool planeXY)
    {
        this.origin = origin;
        this.dir = direction.normalized;
        this.lateral = lateral.normalized;
        this.speed = speed;
        this.amp = amp;
        this.freq = freq;
        this.planeXY = planeXY;

        t = 0f;
        phase = Random.value * Mathf.PI * 2f; // randomize sine start
        if (!cam) cam = Camera.main;
        if (!po) po = GetComponent<PooledObject>();
    }

    private void Update()
    {
        t += Time.deltaTime;

        Vector3 p = origin + dir * (speed * t) + lateral * (Mathf.Sin(phase + t * freq * Mathf.PI * 2f) * amp);
        transform.position = p;

        // Optional: orient to travel direction on the plane
        Vector3 forward = dir;
        if (planeXY) transform.up = forward;           // 2D sprites on XY: "up" faces travel
        else transform.forward = forward;       // 3D models on XZ: forward faces travel

        if (t >= maxLifetime || IsOutsideView())
        {
            SharedFishPool.Instance.Despawn(gameObject, po ? po.SourcePrefab : null);
        }
    }

    private bool IsOutsideView()
    {
        if (!cam) return false;
        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        // if behind camera, treat as outside
        if (vp.z < 0f) return true;

        float min = -viewportMargin;
        float max = 1f + viewportMargin;
        return (vp.x < min || vp.x > max || vp.y < min || vp.y > max);
    }
}
