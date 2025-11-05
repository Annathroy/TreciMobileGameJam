using UnityEngine;

public class FishMover_Straight : BaseFishMover
{
    [Header("Sinusoidal Motion (Z-axis)")]
    [SerializeField] float waveAmplitude = 4f;    // world units of Z swing
    [SerializeField] float waveFrequency = 1.8f;  // cycles per second
    [SerializeField] float forwardSpeedMul = 1f;  // 1 = normal speed
    [SerializeField] float phaseOffsetRandom = 2f * Mathf.PI; // random start phase

    [Header("Heading")]
    [SerializeField] float endYawJitterDeg = 0f;  // keep 0 to stay flat

    Vector3 fwdAxis;     // strictly ±X
    Vector3 waveAxis;    // strictly ±Z
    float startY;
    float startTime;
    float phaseOffset;

    public override void OnSpawned(Vector3 origin, Vector3 dir, float speed)
    {
        base.OnSpawned(origin, dir, speed);

        // lock to pure ±X
        float sx = Mathf.Sign(Mathf.Abs(dir.x) < 1e-4f ? (dir.x >= 0f ? 1f : -1f) : dir.x);
        fwdAxis = new Vector3(sx, 0f, 0f);
        waveAxis = Vector3.forward; // Z direction for sine wave

        // randomize phase so all fish aren’t in sync
        phaseOffset = Random.Range(0f, phaseOffsetRandom);

        startY = origin.y;
        startTime = Time.time;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);
    }

    protected override void TickMove(float dt)
    {
        float tNow = Time.time - startTime;

        // forward progress
        Vector3 forwardMove = fwdAxis * (speed * forwardSpeedMul * dt);

        // sinusoidal offset on Z
        float sine = Mathf.Sin((tNow + phaseOffset) * waveFrequency * Mathf.PI * 2f);
        Vector3 offset = waveAxis * (sine * waveAmplitude * dt);
        // dt multiplier keeps oscillation smooth and frame-rate independent

        // apply both
        Vector3 pos = transform.position + forwardMove + offset;
        pos.y = startY;

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(fwdAxis, Vector3.up);
    }
}
