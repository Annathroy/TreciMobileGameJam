using UnityEngine;

public class FishMover_CircleThenStraight : BaseFishMover
{
    [Header("Phases")]
    [SerializeField] float preStraightDistance = 5f;   // X distance before circle
    [SerializeField] float circleRadius = 2.2f; // circle size in XZ
    [SerializeField] float circleDuration = 1.0f; // seconds for one full lap
    [SerializeField] float postSpeedMul = 1f;   // straight speed after circle

    // keep everything on XZ; Y is locked
    enum Phase { Pre, Circle, Post }
    Phase phase;

    Vector3 fwdAxis;       // ±X only
    Vector3 startPos;
    float startY;

    // pre
    float traveledPre;

    // circle (parametric, robust)
    Vector3 center;
    Vector3 basisX;        // local circle X axis (forward)
    Vector3 basisZ;        // local circle Z axis (right)
    int dirSign;       // +1 ccw, -1 cw
    float angle0;        // starting angle
    float elapsedCircle; // seconds in circle
    float maxCircleTime; // safety

    public override void OnSpawned(Vector3 origin, Vector3 dir, float speed)
    {
        base.OnSpawned(origin, dir, speed);

        // lock to ±X plane
        float sx = Mathf.Sign(Mathf.Abs(dir.x) < 1e-4f ? (dir.x >= 0f ? 1f : -1f) : dir.x);
        fwdAxis = new Vector3(sx, 0f, 0f);

        startPos = origin;
        startY = origin.y;
        traveledPre = 0f;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);

        phase = Phase.Pre;
    }

    protected override void TickMove(float dt)
    {
        switch (phase)
        {
            case Phase.Pre:
                {
                    float step = Mathf.Max(0f, speed) * dt;
                    traveledPre += step;

                    Vector3 pos = startPos + fwdAxis * Mathf.Min(traveledPre, preStraightDistance);
                    pos.y = startY;
                    transform.position = pos;
                    transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);

                    if (traveledPre >= preStraightDistance)
                        BeginCircle();
                    break;
                }

            case Phase.Circle:
                {
                    // guard bad config
                    if (circleRadius <= 0.001f || circleDuration <= 0.001f)
                    {
                        phase = Phase.Post;
                        return;
                    }

                    elapsedCircle += dt;
                    float t = Mathf.Clamp01(elapsedCircle / circleDuration);
                    float angle = angle0 + dirSign * (Mathf.PI * 2f) * t;

                    Vector3 circ = center
                        + basisX * Mathf.Cos(angle) * circleRadius
                        + basisZ * Mathf.Sin(angle) * circleRadius;

                    circ.y = startY;
                    transform.position = circ;

                    // face tangent direction (derivative of circle param)
                    Vector3 tangent = (-basisX * Mathf.Sin(angle) + basisZ * Mathf.Cos(angle)) * dirSign;
                    if (tangent.sqrMagnitude > 1e-6f)
                        transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);

                    // finish conditions (normal + safety)
                    if (t >= 1f || elapsedCircle >= maxCircleTime)
                    {
                        // snap facing to pure ±X for the post phase
                        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);
                        phase = Phase.Post;
                    }
                    break;
                }

            case Phase.Post:
                {
                    Vector3 pos = transform.position + fwdAxis * (speed * postSpeedMul * dt);
                    pos.y = startY;
                    transform.position = pos;
                    transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);
                    break;
                }
        }
    }

    void BeginCircle()
    {
        // build orthonormal basis on XZ
        basisX = fwdAxis;                                        // along ±X
        basisZ = Vector3.Cross(Vector3.up, basisX).normalized;   // ±Z
        dirSign = (Random.value < 0.5f) ? 1 : -1;

        // center is offset by radius along ±Z from current position
        center = transform.position + basisZ * dirSign * circleRadius;

        // compute starting angle from center->current in our basis (parametric, not incremental)
        Vector3 r0 = (transform.position - center);
        float x = Vector3.Dot(r0, basisX);
        float z = Vector3.Dot(r0, basisZ);
        angle0 = Mathf.Atan2(z, x); // radians

        elapsedCircle = 0f;
        maxCircleTime = Mathf.Max(circleDuration * 1.25f, 0.25f); // safety cap
        phase = Phase.Circle;
    }
}
