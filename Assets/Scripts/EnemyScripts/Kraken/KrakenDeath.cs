using UnityEngine;

[DisallowMultipleComponent]
public class KrakenDeath : MonoBehaviour
{
    public enum DirectionMode { CameraUpOnPlane, WorldAxisZ, WorldAxisY, Custom }

    [Header("Refs")]
    [SerializeField] Camera cam;                             // if null => Camera.main
    [SerializeField] Vector3 planeNormal = Vector3.up;       // XZ gameplay => Vector3.up

    [Header("Motion")]
    [SerializeField] DirectionMode directionMode = DirectionMode.CameraUpOnPlane;
    [SerializeField] Vector3 customDirection = Vector3.forward;
    [SerializeField] float moveSpeed = 2.0f;
    [SerializeField] float maxDuration = 8f;
    [SerializeField] float viewportOvershoot = 0.08f;
    [Tooltip("Recompute direction each frame (if camera moves/rotates during death).")]
    [SerializeField] bool followCameraDuringDeath = false;

    [Header("Shake")]
    [SerializeField] float shakeAmplitude = 0.35f;           // in-plane world units
    [SerializeField] float shakeFrequency = 12f;             // Hz
    [SerializeField] AnimationCurve shakeDampen = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Cleanup")]
    [SerializeField] Behaviour[] disableBehaviours;
    [SerializeField] Collider[] disableColliders;
    [SerializeField] bool disableRigidbody = true;
    [SerializeField] float destroyDelay = 0.5f;

    // --- internals (all cached; no GC after start) ---
    bool _running;
    Vector3 _pos;
    Vector3 _dir;                    // offscreen direction on the plane
    Vector3 _axisA, _axisB;          // in-plane orthonormal basis for shake
    float _seedA, _seedB;            // shake phase seeds
    float _targetViewportY;          // 1f + overshoot
    Rigidbody _rb;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>Call when Kraken dies.</summary>
    public void TriggerDeath()
    {
        if (_running) return;
        _running = true;

        // Disable gameplay systems
        if (disableBehaviours != null) for (int i = 0; i < disableBehaviours.Length; i++)
                if (disableBehaviours[i]) disableBehaviours[i].enabled = false;

        if (disableColliders != null) for (int i = 0; i < disableColliders.Length; i++)
                if (disableColliders[i]) disableColliders[i].enabled = false;

        if (disableRigidbody && _rb)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        _pos = transform.position;
        _seedA = Random.value * 1000f;
        _seedB = Random.value * 1000f;
        _targetViewportY = 1f + Mathf.Abs(viewportOvershoot);

        // precompute plane basis + initial direction
        planeNormal = planeNormal.sqrMagnitude > 0f ? planeNormal.normalized : Vector3.up;
        BuildBasis(planeNormal, out _axisA, out _axisB);
        _dir = ComputeDirection().normalized;
        if (_dir.sqrMagnitude < 1e-6f) _dir = _axisB; // fallback

        StopAllCoroutines();
        StartCoroutine(Co_Death());
    }

    System.Collections.IEnumerator Co_Death()
    {
        float t = 0f;
        float maxT = Mathf.Max(0.0001f, maxDuration);

        while (t < maxT)
        {
            t += Time.deltaTime;

            // Optionally track camera changes
            if (followCameraDuringDeath)
            {
                var d = ComputeDirection();
                if (d.sqrMagnitude > 1e-6f) _dir = d.normalized;
            }

            // Integrate forward motion (offscreen / screen-up)
            _pos += _dir * moveSpeed * Time.deltaTime;

            // Cheap, smooth shake (sin/cos on two orthogonal in-plane axes)
            float amp = shakeAmplitude * Mathf.Clamp01(shakeDampen.Evaluate(t / maxT));
            if (amp > 0f)
            {
                float w = shakeFrequency * 6.2831853f; // 2πf
                float s = Mathf.Sin(_seedA + w * Time.time);
                float c = Mathf.Cos(_seedB + w * Time.time * 0.97f); // slight detune
                Vector3 shake = (_axisA * s + _axisB * c) * amp;
                transform.position = _pos + shake;
            }
            else
            {
                transform.position = _pos;
            }

            // Offscreen check (viewport space)
            if (cam)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                if (vp.z > 0f && vp.y > _targetViewportY) break;
            }

            yield return null;
        }

        // Small polish drift
        float driftT = 0f, driftDur = 0.25f;
        while (driftT < driftDur)
        {
            driftT += Time.deltaTime;
            transform.position += _dir * (moveSpeed * 0.6f) * Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject, destroyDelay);
    }

    // --- math helpers (no allocs) ---

    Vector3 ComputeDirection()
    {
        switch (directionMode)
        {
            case DirectionMode.CameraUpOnPlane:
                {
                    if (!cam) cam = Camera.main;
                    Vector3 up = cam ? cam.transform.up : Vector3.forward; // safe fallback
                                                                           // Project 'up' onto plane: up - (up·n) n
                    Vector3 d = up - Vector3.Dot(up, planeNormal) * planeNormal;
                    if (d.sqrMagnitude < 1e-6f) d = _axisB; // avoid degenerate
                    return d;
                }
            case DirectionMode.WorldAxisZ:
                {
                    Vector3 d = Vector3.forward - Vector3.Dot(Vector3.forward, planeNormal) * planeNormal;
                    return d;
                }
            case DirectionMode.WorldAxisY:
                {
                    Vector3 d = Vector3.up - Vector3.Dot(Vector3.up, planeNormal) * planeNormal;
                    return d;
                }
            case DirectionMode.Custom:
            default:
                {
                    Vector3 v = customDirection.sqrMagnitude > 0f ? customDirection : Vector3.forward;
                    Vector3 d = v - Vector3.Dot(v, planeNormal) * planeNormal;
                    return d;
                }
        }
    }

    static void BuildBasis(in Vector3 n, out Vector3 a, out Vector3 b)
    {
        // pick a vector least aligned with n to avoid precision issues
        Vector3 t = (Mathf.Abs(n.y) < 0.9f) ? Vector3.up : Vector3.right;
        a = t - Vector3.Dot(t, n) * n; // project to plane
        a = a.sqrMagnitude > 1e-8f ? a.normalized : Vector3.forward;
        b = Vector3.Normalize(Vector3.Cross(n, a));
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!_running && Input.GetKeyDown(KeyCode.K))
            TriggerDeath();
    }
#endif
}
