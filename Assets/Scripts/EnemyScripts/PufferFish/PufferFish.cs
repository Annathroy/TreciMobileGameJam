using System.Collections;
using UnityEngine;

public class PufferFish : MonoBehaviour
{
    [Header("Spike Firing")]
    [SerializeField] private SimplePool spikePool;
    [SerializeField] private int spikesPerWave = 24;
    [SerializeField] private float inflateScaleMultiplier = 4f;
    [SerializeField] private float inflateTime = 0.15f;
    [SerializeField] private float inflatedDuration = 0.5f;
    [SerializeField] private float deflateTime = 0.15f;
    [SerializeField] private Transform fireOrigin;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float moveDelay = 0.5f;
    [SerializeField] private int maxJumps = 6;
    [SerializeField] private bool flipModelForward = true; // NEW: option to flip model 180 degrees

    [Header("Animation")]
    [SerializeField] private string attackBoolParameter = "isAttacking";
    [SerializeField] private string attackTriggerParameter = "spikes_out";
    [SerializeField] private bool useAttackTrigger = true; // Use trigger instead of bool

    [Header("Exit / Despawn")]
    [SerializeField] private float runOutSpeed = 10f;
    [SerializeField] private float runOutMaxTime = 4f;
    [SerializeField] private float offscreenMargin = 3f;
    [SerializeField] private bool destroyOnEnd = false;

    private Vector3 baseScale;
    private Camera cam;
    private PufferWaypointManager mgr;
    private int currentIndex = -1;
    private bool running = false;
    private Animator animator;

    void Awake()
    {
        baseScale = transform.localScale;
        cam = Camera.main;
        mgr = PufferWaypointManager.Instance;
        if (!mgr) { Debug.LogError("[PufferFish] No PufferWaypointManager in scene!"); enabled = false; }

        if (!spikePool)
            Debug.LogError("NO SPIKE POOL ATTACHED");

        animator = GetComponent<Animator>();
        if (!animator)
            Debug.LogWarning("[PufferFish] No Animator component found - animation parameters will not be set");
    }

    void OnEnable()
    {
        // lock scale & plane
        transform.localScale = baseScale;
        cam = Camera.main;

        // Reset animation parameters when enabled
        if (animator)
        {
            if (!string.IsNullOrEmpty(attackBoolParameter))
                animator.SetBool(attackBoolParameter, false);
        }

        // TELEPORT OFF-SCREEN IMMEDIATELY (before first render)
        if (cam)
        {
            Bounds bnd = CameraXZBounds(cam);
            Vector3 spawn = RandomOffscreenXZ(bnd);
            spawn.y = 0f;
            transform.position = spawn;
        }
        else
        {
            // fallback: shove far left if no camera
            transform.position = new Vector3(transform.position.x - offscreenMargin, 0f, transform.position.z);
        }

        // start behavior
        if (!running)
        {
            running = true;
            StartCoroutine(MainRoutine());
        }
    }

    void OnDisable()
    {
        if (currentIndex != -1)
        {
            mgr.ReleaseWaypoint(currentIndex);
            currentIndex = -1;
        }
        running = false;
        
        // Reset animation parameters when disabled
        if (animator && !string.IsNullOrEmpty(attackBoolParameter))
            animator.SetBool(attackBoolParameter, false);
    }

    IEnumerator MainRoutine()
    {
        int jumps = 0;
        while (jumps < maxJumps)
        {
            yield return MoveToFreeWaypoint();
            yield return InflateShootDeflate();
            yield return new WaitForSeconds(moveDelay);
            jumps++;
        }

        // after max jumps → fly offscreen
        yield return RunOffscreenThenDespawn();
    }

    IEnumerator MoveToFreeWaypoint()
    {
        // release old spot
        if (currentIndex != -1)
        {
            mgr.ReleaseWaypoint(currentIndex);
            currentIndex = -1;
        }

        // claim new
        int index = mgr.GetFreeWaypoint();
        if (index == -1)
        {
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        currentIndex = index;
        Transform target = mgr.GetWaypoint(index);
        Vector3 targetPos = new Vector3(target.position.x, 0f, target.position.z);

        while ((transform.position - targetPos).sqrMagnitude > 0.02f)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;

            // Fix rotation - if model faces backwards, flip it 180 degrees
            Vector3 lookDir = flipModelForward ? -dir : dir;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir, Vector3.up), 10f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator InflateShootDeflate()
    {
        yield return TweenScale(baseScale, baseScale * inflateScaleMultiplier, inflateTime);
        
        // Trigger attack animation
        TriggerAttackAnimation();
        
        FireOneWave();
        yield return new WaitForSeconds(inflatedDuration);
        
        // End attack animation
        EndAttackAnimation();
        
        yield return TweenScale(transform.localScale, baseScale, deflateTime);
    }

    private void TriggerAttackAnimation()
    {
        if (!animator) return;

        if (useAttackTrigger && !string.IsNullOrEmpty(attackTriggerParameter))
        {
            // Use trigger parameter (preferred for one-shot animations)
            animator.SetTrigger(attackTriggerParameter);
            Debug.Log($"[PufferFish] Triggering animation: {attackTriggerParameter}");
        }
        else if (!string.IsNullOrEmpty(attackBoolParameter))
        {
            // Use bool parameter as fallback
            animator.SetBool(attackBoolParameter, true);
            Debug.Log($"[PufferFish] Setting bool parameter: {attackBoolParameter} = true");
        }
    }

    private void EndAttackAnimation()
    {
        if (!animator) return;

        // Only reset bool parameters (triggers reset automatically)
        if (!useAttackTrigger && !string.IsNullOrEmpty(attackBoolParameter))
        {
            animator.SetBool(attackBoolParameter, false);
            Debug.Log($"[PufferFish] Setting bool parameter: {attackBoolParameter} = false");
        }
    }

    IEnumerator RunOffscreenThenDespawn()
    {
        if (!cam) cam = Camera.main;

        Bounds bnd = CameraXZBounds(cam);
        Vector3 exit = RandomOffscreenXZ(bnd);
        exit.y = 0f;

        Vector3 dir;
        float elapsed = 0f;
        while (elapsed < runOutMaxTime)
        {
            dir = (exit - transform.position);
            if (dir.sqrMagnitude < 0.05f * 0.05f) break;
            Vector3 moveDir = dir.normalized;
            transform.position += moveDir * runOutSpeed * Time.deltaTime;

            // Fix rotation for exit movement too
            Vector3 lookDir = flipModelForward ? -moveDir : moveDir;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir, Vector3.up), 8f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // free current waypoint
        if (currentIndex != -1)
        {
            mgr.ReleaseWaypoint(currentIndex);
            currentIndex = -1;
        }

        if (destroyOnEnd) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // ----------------------- Helpers -----------------------

    void FireOneWave()
    {
        if (!spikePool || spikesPerWave <= 0) return;

        float step = 360f / spikesPerWave;
        Vector3 origin = fireOrigin ? fireOrigin.position : transform.position;
        float offset = SafeRadiusXZ() + 0.25f;
        var ignore = GetComponentsInChildren<Collider>();

        for (int i = 0; i < spikesPerWave; i++)
        {
            float angle = i * step;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos = origin + dir * offset; pos.y = 0f;

            var go = spikePool.Get();
            var proj = go.GetComponent<SpikeProjectile>();
            if (proj) proj.Launch(pos, dir, spikePool, ignore);
            else go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
        }
    }

    float SafeRadiusXZ()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var c in GetComponentsInChildren<Collider>())
            if (c && c.enabled) b.Encapsulate(c.bounds);
        return 0.5f * Mathf.Max(b.size.x, b.size.z);
    }

    IEnumerator TweenScale(Vector3 from, Vector3 to, float time)
    {
        if (time <= 0f) { transform.localScale = to; yield break; }
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.LerpUnclamped(from, to, t / time);
            yield return null;
        }
        transform.localScale = to;
    }

    Bounds CameraXZBounds(Camera c)
    {
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Vector3[] corners = new Vector3[4];
        Ray ray;

        ray = c.ViewportPointToRay(new Vector3(0, 0, 0)); plane.Raycast(ray, out float e0); corners[0] = ray.GetPoint(e0);
        ray = c.ViewportPointToRay(new Vector3(1, 0, 0)); plane.Raycast(ray, out float e1); corners[1] = ray.GetPoint(e1);
        ray = c.ViewportPointToRay(new Vector3(0, 1, 0)); plane.Raycast(ray, out float e2); corners[2] = ray.GetPoint(e2);
        ray = c.ViewportPointToRay(new Vector3(1, 1, 0)); plane.Raycast(ray, out float e3); corners[3] = ray.GetPoint(e3);

        Bounds b = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++) b.Encapsulate(corners[i]);
        return b;
    }

    Vector3 RandomOffscreenXZ(Bounds visible)
    {
        int side = Random.Range(0, 4); // 0 L,1 R,2 Top,3 Bottom
        float x = 0, z = 0;
        switch (side)
        {
            case 0: x = visible.min.x - offscreenMargin; z = Random.Range(visible.min.z, visible.max.z); break;
            case 1: x = visible.max.x + offscreenMargin; z = Random.Range(visible.min.z, visible.max.z); break;
            case 2: z = visible.max.z + offscreenMargin; x = Random.Range(visible.min.x, visible.max.x); break;
            default: z = visible.min.z - offscreenMargin; x = Random.Range(visible.min.x, visible.max.x); break;
        }
        return new Vector3(x, 0f, z);
    }
}
