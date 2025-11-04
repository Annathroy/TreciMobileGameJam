using UnityEngine;

public class EnemyBeamPhoneEdgeEncounter : MonoBehaviour
{
    [Header("Prefabs & Beam")]
    [SerializeField] EnemyUnit enemyPrefab;
    [SerializeField] BeamLink beamPrefab;

    [Header("Camera / Ground")]
    [SerializeField] Camera cam;              // assign; if null uses Camera.main
    [SerializeField] float groundY = 0f;      // y-plane enemies live on

    [Header("Spawn at phone edges")]
    [Tooltip("Inset from left/right edges in viewport units (0..0.5). Ex: 0.05 = 5% from each side.")]
    [SerializeField] float edgeInset = 0.05f;
    [SerializeField] float spawnZ = -6f;

    [Header("Movement targets (Z only)")]
    [SerializeField] Vector2 targetZRange = new Vector2(2f, 6f);
    [SerializeField] float minSeparation = 1.5f;

    [Header("Cycle timings")]
    [Tooltip("How long the beam stays on (shooting) each round.")]
    [SerializeField] float beamDuration = 2.5f;
    [Tooltip("Pause after beam turns off, before moving again.")]
    [SerializeField] float postBeamPause = 0.6f;

    [Header("Optional: override edge mode with fixed X from center")]
    [SerializeField] bool useFixedXFromCenter = false;
    [SerializeField] float fixedX = 4.0f;

    [Header("Looping")]
    [SerializeField] bool infiniteLoop = true;
    [SerializeField] int rounds = 3;          // ignored if infiniteLoop=true

    EnemyUnit a, b;
    BeamLink beam;

    enum State { Spawning, Moving, Shooting, PostPause }
    State state = State.Spawning;

    float phaseEndsAt = 0f;
    int roundsDone = 0;

    void Start()
    {
        if (!cam) cam = Camera.main;
        SpawnPair();
        BeginMovePhase();
    }

    void Update()
    {
        switch (state)
        {
            case State.Moving:
                if (a.Arrived && b.Arrived)
                    BeginShootingPhase();
                break;

            case State.Shooting:
                if (Time.time >= phaseEndsAt)
                    BeginPostPause();
                break;

            case State.PostPause:
                if (Time.time >= phaseEndsAt)
                {
                    roundsDone++;
                    if (!infiniteLoop && roundsDone >= rounds)
                    {
                        // stop here
                        beam.Enable(false);
                        enabled = false;
                        return;
                    }
                    // pick new positions and move again
                    RetargetAndMove();
                }
                break;
        }
    }

    // ---- phases ----
    void BeginMovePhase()
    {
        beam.Enable(false);
        // choose two distinct Zs
        float zA, zB;
        int guard = 0;
        do
        {
            zA = Random.Range(targetZRange.x, targetZRange.y);
            zB = Random.Range(targetZRange.x, targetZRange.y);
        } while (Mathf.Abs(zA - zB) < minSeparation && ++guard < 24);

        a.SetTargetZ(zA);
        b.SetTargetZ(zB);
        state = State.Moving;
    }

    void BeginShootingPhase()
    {
        beam.Enable(true);                // “shooting” = beam on
        phaseEndsAt = Time.time + beamDuration;
        state = State.Shooting;
    }

    void BeginPostPause()
    {
        beam.Enable(false);               // stop shooting
        phaseEndsAt = Time.time + Mathf.Max(0f, postBeamPause);
        state = State.PostPause;
    }

    void RetargetAndMove()
    {
        BeginMovePhase();
    }

    // ---- spawn/despawn ----
    void SpawnPair()
    {
        float leftX, rightX;
        if (useFixedXFromCenter)
        {
            leftX = -Mathf.Abs(fixedX);
            rightX = Mathf.Abs(fixedX);
        }
        else
        {
            leftX = WorldXAtViewportX(0f + edgeInset, spawnZ);
            rightX = WorldXAtViewportX(1f - edgeInset, spawnZ);
        }

        Vector3 spawnLeft = new Vector3(leftX, groundY, spawnZ);
        Vector3 spawnRight = new Vector3(rightX, groundY, spawnZ);

        a = Instantiate(enemyPrefab, spawnLeft, Quaternion.identity);
        b = Instantiate(enemyPrefab, spawnRight, Quaternion.identity);

        a.SetFixedX(leftX);
        b.SetFixedX(rightX);

        beam = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        beam.a = a.transform;
        beam.b = b.transform;
        beam.Enable(false);
    }

    // Convert a viewport X edge to world X at given Z (ortho or perspective)
    float WorldXAtViewportX(float vx, float atZ)
    {
        if (cam.orthographic)
        {
            var p = cam.ViewportToWorldPoint(new Vector3(vx, 0.5f, Mathf.Abs(atZ - cam.transform.position.z)));
            return p.x;
        }
        else
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(vx, 0.5f, 0f));
            Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            return plane.Raycast(ray, out float t) ? ray.GetPoint(t).x : 0f;
        }
    }
}
