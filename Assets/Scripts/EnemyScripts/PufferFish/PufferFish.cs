using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection; // only used if you enable reflection check

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

    [Header("Auto Spike Pool Discovery")]
    [Tooltip("If true, will search the scene for the nearest SimplePool to use for spikes when none is assigned or the assigned one is unavailable.")]
    [SerializeField] private bool autoFindSpikePool = true;
    [Tooltip("If set (recommended), only consider SimplePools on GameObjects with this tag. Leave empty to consider all SimplePools.")]
    [SerializeField] private string spikePoolTag = "SpikePool";
    [Tooltip("If true, prefer pools whose prefab contains a SpikeProjectile component (uses reflection to inspect SimplePool's prefab).")]
    [SerializeField] private bool requireProjectileInPrefab = false;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float moveDelay = 0.5f;
    [SerializeField] private int maxJumps = 6;
    [SerializeField] private bool flipModelForward = true;

    [Header("Animation")]
    [SerializeField] private string attackBoolParameter = "isAttacking";
    [SerializeField] private string attackTriggerParameter = "spikes_out";
    [SerializeField] private bool useAttackTrigger = true;

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

        if (!spikePool && autoFindSpikePool)
            ResolveSpikePool();

        animator = GetComponent<Animator>();
        if (!animator)
            Debug.LogWarning("[PufferFish] No Animator component found - animation parameters will not be set");
    }

    void OnEnable()
    {
        transform.localScale = baseScale;
        cam = Camera.main;

        if (animator && !string.IsNullOrEmpty(attackBoolParameter))
            animator.SetBool(attackBoolParameter, false);

        if (!spikePool && autoFindSpikePool)
            ResolveSpikePool();

        if (cam)
        {
            Bounds bnd = CameraXZBounds(cam);
            Vector3 spawn = RandomOffscreenXZ(bnd);
            spawn.y = 0f;
            transform.position = spawn;
        }
        else
        {
            transform.position = new Vector3(transform.position.x - offscreenMargin, 0f, transform.position.z);
        }

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

        yield return RunOffscreenThenDespawn();
    }

    IEnumerator MoveToFreeWaypoint()
    {
        if (currentIndex != -1)
        {
            mgr.ReleaseWaypoint(currentIndex);
            currentIndex = -1;
        }

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

            Vector3 lookDir = flipModelForward ? -dir : dir;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir, Vector3.up), 10f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator InflateShootDeflate()
    {
        yield return TweenScale(baseScale, baseScale * inflateScaleMultiplier, inflateTime);

        TriggerAttackAnimation();

        // Ensure a pool exists before firing
        if (!spikePool && autoFindSpikePool)
            ResolveSpikePool();

        FireOneWave();

        yield return new WaitForSeconds(inflatedDuration);
        EndAttackAnimation();
        yield return TweenScale(transform.localScale, baseScale, deflateTime);
    }

    private void TriggerAttackAnimation()
    {
        if (!animator) return;

        if (useAttackTrigger && !string.IsNullOrEmpty(attackTriggerParameter))
            animator.SetTrigger(attackTriggerParameter);
        else if (!string.IsNullOrEmpty(attackBoolParameter))
            animator.SetBool(attackBoolParameter, true);
    }

    private void EndAttackAnimation()
    {
        if (!animator) return;
        if (!useAttackTrigger && !string.IsNullOrEmpty(attackBoolParameter))
            animator.SetBool(attackBoolParameter, false);
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
            if (dir.sqrMagnitude < 0.0025f) break;
            Vector3 moveDir = dir.normalized;
            transform.position += moveDir * runOutSpeed * Time.deltaTime;

            Vector3 lookDir = flipModelForward ? -moveDir : moveDir;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir, Vector3.up), 8f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentIndex != -1)
        {
            mgr.ReleaseWaypoint(currentIndex);
            currentIndex = -1;
        }

        if (destroyOnEnd) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // ----------------------- Auto-Find Spike Pool -----------------------

    private void ResolveSpikePool()
    {
        SimplePool found = null;

        // 1) Tagged pools first (if tag provided)
        if (!string.IsNullOrEmpty(spikePoolTag))
        {
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(spikePoolTag);
                float best = float.PositiveInfinity;
                foreach (var go in tagged)
                {
                    if (!go) continue;
                    var p = go.GetComponent<SimplePool>();
                    if (!p) continue;
                    if (requireProjectileInPrefab && !PoolLikelySpawnsSpikeProjectiles(p)) continue;

                    float d = (go.transform.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; found = p; }
                }
            }
            catch { /* tag might not exist; ignore */ }
        }

        // 2) Fallback: any SimplePool in scene (active or inactive)
        if (!found)
        {
            var all = Resources.FindObjectsOfTypeAll<SimplePool>(); // includes inactive & DontDestroyOnLoad
            float best = float.PositiveInfinity;
            foreach (var p in all)
            {
                if (!p || p.gameObject.hideFlags != HideFlags.None) continue; // ignore assets
                if (requireProjectileInPrefab && !PoolLikelySpawnsSpikeProjectiles(p)) continue;

                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; found = p; }
            }
        }

        if (found)
        {
            spikePool = found;
            // Debug.Log($"[PufferFish] Bound nearest spike pool: {found.name}", found);
        }
        else
        {
            Debug.LogWarning("[PufferFish] Could not find a spike pool. No spikes will fire.");
        }
    }

    // Optional: check the pool's prefab via reflection for SpikeProjectile
    private bool PoolLikelySpawnsSpikeProjectiles(SimplePool p)
    {
        if (!requireProjectileInPrefab || p == null) return true;
        try
        {
            var field = typeof(SimplePool).GetField("prefab", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefabGO = field?.GetValue(p) as GameObject;
            return prefabGO && prefabGO.GetComponent<SpikeProjectile>() != null;
        }
        catch
        {
            return true; // fail open
        }
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
            if (!go) continue;

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
