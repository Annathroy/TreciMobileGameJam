using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class KrakenTelegraphBands : MonoBehaviour
{
    [Header("Grid (rows x cols)")]
    [SerializeField] private int rows = 5;   // horizontal divisions
    [SerializeField] private int cols = 3;   // vertical divisions

    [Header("Timing")]
    [SerializeField] private float cycleInterval = 5f;  // seconds between attacks
    [SerializeField] private float warnDuration = 2f;   // how long the marker stays

    [Header("UI")]
    [SerializeField] private Canvas rootCanvas;         // Screen Space - Overlay
    [SerializeField] private Image markerPrefab;        // optional prefab for the marker

    [Header("Visuals")]
    [SerializeField] private Color markerColor = new Color(1f, 0f, 0f, 0.6f); // red tint

    private RectTransform canvasRT;
    private Coroutine loopCo;

    private enum BandType { Row, Column }

    void Awake()
    {
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        if (!rootCanvas)
        {
            Debug.LogError("[KrakenTelegraphBands] No Canvas assigned/found.");
            enabled = false; return;
        }
        canvasRT = rootCanvas.GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        if (loopCo == null) loopCo = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }
    }

    IEnumerator Loop()
    {
        while (true)
        {
            // Pick type and index
            BandType type = (Random.value < 0.5f) ? BandType.Row : BandType.Column;
            int index = (type == BandType.Row)
                ? Random.Range(0, Mathf.Max(1, rows))
                : Random.Range(0, Mathf.Max(1, cols));

            // Spawn marker
            var marker = SpawnMarker(type, index);

            // Wait, then trigger attack
            yield return new WaitForSeconds(warnDuration);

            if (marker) Destroy(marker.gameObject);

            if (type == BandType.Row)
                Debug.Log($"[KRAKEN] ROW attack triggered on row {index} (t={Time.time:F2})");
            else
                Debug.Log($"[KRAKEN] COLUMN attack triggered on column {index} (t={Time.time:F2})");

            // Wait until next cycle
            float wait = Mathf.Max(0f, cycleInterval - warnDuration);
            if (wait > 0f) yield return new WaitForSeconds(wait);
        }
    }

    RectTransform SpawnMarker(BandType type, int index)
    {
        Image img;
        if (markerPrefab)
            img = Instantiate(markerPrefab, rootCanvas.transform);
        else
        {
            var go = new GameObject("KrakenMarker", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rootCanvas.transform, false);
            img = go.GetComponent<Image>();
        }

        img.color = markerColor;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;

        float w = canvasRT.rect.width;
        float h = canvasRT.rect.height;
        float cellW = w / Mathf.Max(1, cols);
        float cellH = h / Mathf.Max(1, rows);

        // half-size marker
        Vector2 size = new Vector2(cellW * 0.5f, cellH * 0.5f);
        rt.sizeDelta = size;

        if (type == BandType.Row)
        {
            // Row: spawn at top edge, random left or right side
            bool onLeft = Random.value < 0.5f;

            float x = onLeft ? 0f : (w - size.x);
            float y = h - ((index + 1) * cellH); // top edge aligned to that row
            rt.anchoredPosition = new Vector2(x, y);
        }
        else // Column
        {
            // Column: spawn at top edge aligned with that column
            float x = index * cellW;
            float y = h - size.y; // hug top border
            rt.anchoredPosition = new Vector2(x, y);
        }

        return rt;
    }
}
