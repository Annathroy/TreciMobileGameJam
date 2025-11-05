using UnityEngine;

public class FishMover_ReversedBell : BaseFishMover
{
    [Header("Path (move along X only, dip along Z)")]
    [SerializeField] float travelDistance = 30f;   // total X distance
    [SerializeField] float dipAmplitude = 10f;   // U-depth along Z (±)
    [SerializeField, Range(0.1f, 1.0f)]
    float sigma = 0.22f; // narrower => sharper U
    [SerializeField, Range(0.0f, 1.0f)]
    float dipCenter = 0.35f; // where peak dip occurs (0..1), lower => earlier dip
    [SerializeField] float dipSpeedMul = 1.8f;  // >1 = dip completes faster than forward travel

    [Header("Randomness")]
    [SerializeField] bool randomizeDipSide = true;  // bend toward +Z or -Z
    [SerializeField] float endYawJitterDeg = 0f;    // keep 0 to avoid diagonal

    // internals
    float xTravel;            // accumulated X distance
    float dipU;               // dip progress 0..1, can advance faster via dipSpeedMul
    Vector3 startPos;
    Vector3 fwdAxis;          // strictly ±X
    Vector3 dipAxis;          // strictly ±Z
    float startY;

    public override void OnSpawned(Vector3 origin, Vector3 dir, float speed)
    {
        base.OnSpawned(origin, dir, speed);

        // LOCK to ±X only (ignore any Y/Z in dir to prevent diagonal launch)
        float signX = Mathf.Sign(Mathf.Abs(dir.x) < 1e-4f ? (dir.x >= 0f ? 1f : -1f) : dir.x);
        fwdAxis = new Vector3(signX, 0f, 0f); // ±X

        // Dip along ±Z only
        int dipSign = (randomizeDipSide && Random.value < 0.5f) ? -1 : 1;
        dipAxis = new Vector3(0f, 0f, dipSign); // ±Z

        // Starting transform — keep things flat (no tilt)
        startPos = origin;
        startY = origin.y;
        xTravel = 0f;
        dipU = 0f;

        // Face along X, keep upright
        var yaw = Mathf.Clamp(endYawJitterDeg, 0f, 0f); // force 0 unless you really want it
        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);
        transform.position = origin;
    }

    protected override void TickMove(float dt)
    {
        // advance forward (X)
        float step = Mathf.Max(0f, speed) * dt;
        xTravel += step;

        // forward progress 0..1 across the path
        float uForward = (travelDistance <= 0.01f) ? 1f : Mathf.Clamp01(xTravel / travelDistance);

        // advance dip faster/slower than forward depending on dipSpeedMul
        dipU = Mathf.Clamp01(dipU + (step / Mathf.Max(travelDistance, 0.01f)) * Mathf.Max(0.01f, dipSpeedMul));

        // reversed bell: negative Gaussian centered at dipCenter, width sigma
        float s2 = Mathf.Max(1e-4f, sigma * sigma);
        float gauss = Mathf.Exp(-((dipU - dipCenter) * (dipU - dipCenter)) / (2f * s2)); // 0..1
        float dip = -Mathf.Abs(dipAmplitude) * gauss;  // negative peak at center

        // compute position: X from forward progress, Z from dip, keep Y fixed
        Vector3 basePos = startPos + fwdAxis * (uForward * travelDistance);
        Vector3 offset = dipAxis * dip;
        Vector3 pos = basePos + offset;
        pos.y = startY; // freeze Y so it never climbs "45° up"

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up); // keep heading flat along X
    }
}
