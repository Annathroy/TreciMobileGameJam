using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SharkTelegraphBottomCenter : MonoBehaviour
{

    // add field
    [SerializeField] private float firstAttackDelay = 2f;
    IEnumerator BeginAfterDelay()
    {
        if (firstAttackDelay > 0f) yield return WaitSmart(firstAttackDelay);
        loopCo = StartCoroutine(Loop());
    }

    [Header("Timing")]
    [SerializeField] private float cycleInterval = 6f;
    [SerializeField] private float warnDuration = 1.5f;
    [SerializeField] private float appearDuration = 0.5f;
    [SerializeField] private float stayDuration = 1.5f;
    [SerializeField] private float retreatDuration = 0.6f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("UI Telegraph")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image markerPrefab;
    [SerializeField] private Color markerTint = new Color(1f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Sprite markerFlashA;
    [SerializeField] private Sprite markerFlashB;
    [SerializeField] private float markerFlashHz = 6f;
    [SerializeField] private bool markerPreserveAspect = true;

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

    IEnumerator Loop()
    {
        while (true)
        {
            Image marker = SpawnMarker();
            if (markerFlashA || markerFlashB)
                StartCoroutine(FlashMarker(marker, warnDuration));
            yield return WaitSmart(warnDuration);
            if (marker) Destroy(marker.gameObject);

            yield return StartCoroutine(SpawnAndAnimateShark());

            yield return WaitSmart(Mathf.Max(0f, cycleInterval - warnDuration));
        }
    }

    Image SpawnMarker()
    {
        var img = markerPrefab ? Instantiate(markerPrefab, rootCanvas.transform) : CreateDefaultMarker();
        img.color = markerTint;
        img.raycastTarget = false;
        img.preserveAspect = markerPreserveAspect;
        if (markerFlashA) img.sprite = markerFlashA;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);

        // --- smaller marker ---
        rt.sizeDelta = new Vector2(canvasRT.rect.width * 0.35f, canvasRT.rect.height * 0.15f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        // -----------------------

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
            yield return useUnscaledTime ? new WaitForSecondsRealtime(halfPeriod)
                                         : new WaitForSeconds(halfPeriod);
            elapsed += halfPeriod;
        }
    }

    IEnumerator SpawnAndAnimateShark()
    {
        if (!sharkPrefab || !mainCam) yield break;

        Vector2 screenPos = new Vector2(Screen.width * 0.5f, 40f);
        Vector3 baseWorld = ScreenToWorldOnPlane(screenPos, attackPlaneY);

        Vector3 spawnPos = baseWorld - Vector3.forward * spawnBelowOffset;
        Vector3 appearPos = spawnPos + Vector3.forward * appearHeight;
        Vector3 retreatPos = spawnPos;

        GameObject shark = Instantiate(sharkPrefab, spawnPos, Quaternion.identity);
        shark.transform.rotation = Quaternion.Euler(prefabExtraEuler);
        shark.transform.localScale *= sharkScaleMultiplier;

        // rise
        float t = 0f;
        while (t < appearDuration && shark)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            shark.transform.position = Vector3.Lerp(spawnPos, appearPos, t / appearDuration);
            shark.transform.position = new Vector3(shark.transform.position.x, attackPlaneY, shark.transform.position.z);
            yield return null;
        }

        // stay
        yield return WaitSmart(stayDuration);

        // retreat
        t = 0f;
        while (t < retreatDuration && shark)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            shark.transform.position = Vector3.Lerp(appearPos, retreatPos, t / retreatDuration);
            shark.transform.position = new Vector3(shark.transform.position.x, attackPlaneY, shark.transform.position.z);
            yield return null;
        }

        if (destroyOnExit && shark) Destroy(shark);
    }

    Vector3 ScreenToWorldOnPlane(Vector2 pixelPos, float planeY)
    {
        Ray ray = mainCam.ScreenPointToRay(pixelPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
        return mainCam.ScreenToWorldPoint(new Vector3(pixelPos.x, pixelPos.y, 10f));
    }

    IEnumerator WaitSmart(float seconds)
    {
        if (useUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }
}
