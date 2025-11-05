using UnityEngine;

[DisallowMultipleComponent]
public class KrakenIntroMover : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugGizmos = false;

    Camera cam;
    Vector3 planeNormal = Vector3.up;
    float speed = 1.5f;
    float stopViewportY = 0.98f;
    bool followCam = true;

    Vector3 dir;     // screen-down projected onto plane
    bool running;
    Transform tf;

    public void Configure(Camera cam, Vector3 planeNormal, float speed, float stopViewportY, bool followCam)
    {
        this.cam = cam ? cam : Camera.main;
        this.planeNormal = planeNormal.sqrMagnitude > 0f ? planeNormal.normalized : Vector3.up;
        this.speed = Mathf.Max(0.01f, speed);
        this.stopViewportY = Mathf.Clamp(stopViewportY, 0f, 1.2f);
        this.followCam = followCam;
        tf = transform;
        dir = ComputeScreenDownOnPlane();
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; // safe fallback
    }

    public void Begin()
    {
        running = true;
        enabled = true;
    }

    void Awake()
    {
        tf = transform;
        if (!cam) cam = Camera.main;
    }

    void OnEnable()
    {
        // If configured via inspector (no spawner), ensure a sane direction.
        if (dir == Vector3.zero)
        {
            planeNormal = planeNormal.sqrMagnitude > 0f ? planeNormal.normalized : Vector3.up;
            dir = ComputeScreenDownOnPlane();
        }
    }

    void Update()
    {
        if (!running) return;
        if (followCam) { var d = ComputeScreenDownOnPlane(); if (d.sqrMagnitude > 1e-6f) dir = d; }

        tf.position += dir * speed * Time.deltaTime;

        if (cam)
        {
            var vp = cam.WorldToViewportPoint(tf.position);
            if (vp.z > 0f && vp.y <= stopViewportY)
            {
                running = false;
                enabled = false;
            }
        }
    }

    Vector3 ComputeScreenDownOnPlane()
    {
        if (!cam) cam = Camera.main;
        var up = cam ? cam.transform.up : Vector3.forward;
        var screenUpOnPlane = up - Vector3.Dot(up, planeNormal) * planeNormal;
        return -screenUpOnPlane.normalized; // downwards (from above into screen)
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, dir.normalized * 2f);
    }
#endif
}
