using UnityEngine;

[DisallowMultipleComponent]
public class KrakenIntroSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject krakenPrefab;

    [Header("Timing")]
    [SerializeField] private float spawnDelay = 3f;

    [Header("Plane (top-down XZ)")]
    [Tooltip("For XZ gameplay this should be Vector3.up")]
    [SerializeField] private Vector3 planeNormal = Vector3.up;
    [SerializeField] private float planeHeight = 0f;     // usually Y = 0 plane

    [Header("Spawn placement")]
    [Tooltip("How far above the screen the Kraken starts (world units)")]
    [SerializeField] private float worldOvershoot = 10f; // adjust to taste
    [Tooltip("Lateral offset (keep 0 for centered)")]
    [SerializeField] private float xOffset = 0f;

    [Header("Intro movement")]
    [SerializeField] private float glideSpeed = 1.5f;
    [Tooltip("Stop when Kraken’s pivot reaches this viewport Y")]
    [SerializeField] private float targetViewportY = 0.98f;
    [SerializeField] private bool followCameraDuringGlide = true;

    void Awake() { if (!cam) cam = Camera.main; }

    void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Co_Spawn());
    }

    System.Collections.IEnumerator Co_Spawn()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, spawnDelay));
        if (!krakenPrefab || !cam) yield break;

        // get world position at top center of screen
        if (!ViewportToWorldOnPlane(cam, new Vector2(0.5f, 1f), planeNormal, planeHeight, out var spawnPos))
            yield break;

        // enforce X=0 center (ignore camera projection offset)
        spawnPos.x = xOffset;

        // move upward by overshoot amount along camera.up projected on plane
        var camUp = cam.transform.up;
        var upOnPlane = Vector3.ProjectOnPlane(camUp, planeNormal).normalized;
        spawnPos += upOnPlane * worldOvershoot;

        // instantiate, flipped 180° on Y
        var kraken = Instantiate(krakenPrefab, spawnPos, Quaternion.Euler(0f, 180f, 0f));

        // attach mover
        var mover = kraken.GetComponent<KrakenIntroMover>();
        if (!mover) mover = kraken.AddComponent<KrakenIntroMover>();
        mover.Configure(cam, planeNormal, glideSpeed, targetViewportY, followCameraDuringGlide);
        mover.Begin();
    }

    static bool ViewportToWorldOnPlane(Camera cam, Vector2 vp, Vector3 planeNormal, float planeHeight, out Vector3 world)
    {
        var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        var plane = new Plane(planeNormal.normalized, planeNormal.normalized * planeHeight);
        if (plane.Raycast(ray, out float dist))
        {
            world = ray.origin + ray.direction * dist;
            return true;
        }
        world = default;
        return false;
    }
}
