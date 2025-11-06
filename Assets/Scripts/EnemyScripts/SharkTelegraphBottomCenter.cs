using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SharkTelegraphBottomCenter : MonoBehaviour
{
    [Header("Start")]
    [SerializeField] private float firstAttackDelay = 2f;   // legacy field
    [SerializeField, Tooltip("Extra idle time before the *first* attack after enabling.")]
    private float initialIdleTime = 3f;

    [Header("Timing")]
    [SerializeField] private float cycleInterval = 6f;
    [SerializeField] private float warnDuration = 1.5f;
    [SerializeField] private float appearDuration = 0.5f;
    [SerializeField] private float stayDuration = 1.5f;
    [SerializeField] private float retreatDuration = 0.6f;
    [SerializeField] private bool useUnscaledTime = true; // freeze-safe via Delta()

    [Header("UI Telegraph")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image markerPrefab;
    [SerializeField] private Color markerTint = new Color(1f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Sprite markerFlashA;
    [SerializeField] private Sprite markerFlashB;
    [SerializeField] private float markerFlashHz = 6f;
    [SerializeField] private bool markerPreserveAspect = true;

    [Header("Marker Size")]
    [SerializeField, Tooltip("Width multiplier relative to canvas width (0.35 = 35% of canvas width).")]
    private float markerWidthMultiplier = 0.25f;
    [SerializeField, Tooltip("Height multiplier relative to canvas height (0.15 = 15% of canvas height).")]
    private float markerHeightMultiplier = 0.1f;

    [Header("Shark (world-space)")]
    [SerializeField] private GameObject sharkPrefab;
    [SerializeField] private Camera mainCam;
    [SerializeField] private float attackPlaneY = 0f;

    [Header("Spawn & Motion")]
    [SerializeField] private float spawnBelowOffset = 5f;
    [SerializeField] private float appearHeight = 8f;
    [SerializeField] private float sharkScaleMultiplier = 2.5f;
    [SerializeField] private Vector3 prefabExtraEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool destroyOnExit = true;

    [Header("Damage on Appear")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField, Tooltip("XZ radius around the shark when it surfaces that will hit the player once.")]
    private float damageRadius = 2.0f;

    private RectTransform canvasRT;
    private Coroutine loopCo;

    void Awake()
    {
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        if (!rootCanvas) { Debug.LogError("[SharkTelegraph] No Canvas."); enabled = false; return; }
        canvasRT = rootCanvas.GetComponent<RectTransform>();
        if (!mainCam) mainCam = Camera.main;
    }

    void OnEnable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        StartCoroutine(BeginAfterDelay());
    }

    void OnDisable()
    {
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }
    }

    IEnumerator BeginAfterDelay()
    {
        // Don't start while paused
        yield return WaitUntilUnpaused();

        // Idle before the first attack (whichever is greater)
        float delay = Mathf.Max(initialIdleTime, firstAttackDelay);
        if (delay > 0f) yield return WaitSmart(delay);

        loopCo = StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (true)
        {
            Image marker = SpawnMarker();
            if (markerFlashA || markerFlashB)
                StartCoroutine(FlashMarker(marker, warnDuration));

            yield return WaitSmart(warnDuration);

            if (marker) Destroy(marker.gameObject);

            yield return SpawnAndAnimateShark();

            yield return WaitSmart(Mathf.Max(0f, cycleInterval - warnDuration));
        }
    }

    Image SpawnMarker()
    {
        var img = markerPrefab ? Instantiate(markerPrefab, rootCanvas.transform) : CreateDefaultMarker();
        img.color = markerTint;
        img.raycastTarget = false;
        img.preserveAspect = markerPreserveAspect;
        img.transform.SetAsLastSibling();

        if (markerFlashA) img.sprite = markerFlashA;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);

        // Use configurable size multipliers instead of hardcoded values
        rt.sizeDelta = new Vector2(canvasRT.rect.width * markerWidthMultiplier, canvasRT.rect.height * markerHeightMultiplier);
        rt.anchoredPosition = new Vector2(0f, 40f);

        return img;
    }

    Image CreateDefaultMarker()
    {
        var go = new GameObject("SharkMarker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rootCanvas.transform, false);
        return go.GetComponent<Image>();
    }

    IEnumerator FlashMarker(Image img, float seconds)
    {
        if (!img) yield break;

        float elapsed = 0f;
        float halfPeriod = 0.5f / Mathf.Max(0.01f, markerFlashHz);
        Sprite a = markerFlashA;
        Sprite b = markerFlashB ? markerFlashB : a;
        if (a) img.sprite = a;

        while (elapsed < seconds && img)
        {
            img.sprite = (img.sprite == a) ? b : a;
            yield return WaitSmart(halfPeriod);
            elapsed += halfPeriod;
        }
    }

    IEnumerator SpawnAndAnimateShark()
    {
        if (!sharkPrefab || !mainCam) yield break;

        // bottom center of screen, same Y plane
        Vector2 screenPos = new Vector2(Screen.width * 0.5f, 40f);
        Vector3 baseWorld = ScreenToWorldOnPlane(screenPos, attackPlaneY);

        Vector3 spawnPos = baseWorld - Vector3.forward * spawnBelowOffset;
        Vector3 appearPos = spawnPos + Vector3.forward * appearHeight;
        Vector3 retreatPos = spawnPos;

        GameObject shark = Instantiate(sharkPrefab, spawnPos, Quaternion.identity);
        shark.transform.rotation = Quaternion.Euler(prefabExtraEuler);
        shark.transform.localScale *= sharkScaleMultiplier;
        shark.transform.SetAsLastSibling();

        // rise
        float t = 0f;
        while (t < appearDuration && shark)
        {
            float d = Delta();
            t += d;
            float k = (appearDuration <= 0f) ? 1f : Mathf.Clamp01(t / appearDuration);
            shark.transform.position = Vector3.Lerp(spawnPos, appearPos, k);
            shark.transform.position = new Vector3(shark.transform.position.x, attackPlaneY, shark.transform.position.z);
            yield return null;
        }

        // one-time contact damage at the moment it surfaces
        if (shark)
            TryDealContactDamage(appearPos);

        // stay
        yield return WaitSmart(stayDuration);

        // retreat
        t = 0f;
        while (t < retreatDuration && shark)
        {
            float d = Delta();
            t += d;
            float k = (retreatDuration <= 0f) ? 1f : Mathf.Clamp01(t / retreatDuration);
            shark.transform.position = Vector3.Lerp(appearPos, retreatPos, k);
            shark.transform.position = new Vector3(shark.transform.position.x, attackPlaneY, shark.transform.position.z);
            yield return null;
        }

        if (destroyOnExit && shark) Destroy(shark);
    }

    void TryDealContactDamage(Vector3 center)
    {
        var hits = Physics.OverlapSphere(center + Vector3.up * 0.1f, damageRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            var ph = hits[i].GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.ApplyDamage(contactDamage);
                break; // only once
            }
        }
    }

    Vector3 ScreenToWorldOnPlane(Vector2 pixelPos, float planeY)
    {
        Ray ray = mainCam.ScreenPointToRay(pixelPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
        return mainCam.ScreenToWorldPoint(new Vector3(pixelPos.x, pixelPos.y, 10f));
    }

    // ---- Pause-aware time helpers ----
    float Delta()
    {
        if (Time.timeScale == 0f) return 0f; // freeze when paused
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    IEnumerator WaitSmart(float seconds)
    {
        float t = seconds;
        while (t > 0f)
        {
            float d = Delta();
            yield return null;
            t -= d;
        }
    }

    IEnumerator WaitUntilUnpaused()
    {
        while (Time.timeScale == 0f) yield return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!mainCam) mainCam = Camera.main;
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Vector2 screenPos = new Vector2(Screen.width * 0.5f, 40f);
        Vector3 baseWorld = mainCam ? ScreenToWorldOnPlane(screenPos, attackPlaneY) : Vector3.zero;
        Vector3 appearPos = baseWorld - Vector3.forward * spawnBelowOffset + Vector3.forward * appearHeight;
        Gizmos.DrawWireSphere(new Vector3(appearPos.x, attackPlaneY, appearPos.z), damageRadius);
    }
#endif
}