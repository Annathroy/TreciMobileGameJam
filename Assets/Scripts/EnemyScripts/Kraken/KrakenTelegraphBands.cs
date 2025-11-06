using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class KrakenTelegraphBands : MonoBehaviour
{
    [Header("Grid (rows x cols)")]
    [SerializeField] private int rows = 4;   // needs >= 3 if excluding edges
    [SerializeField] private int cols = 3;   // columns currently unused

    // true: "row" is vertical X-band (LEFT↔RIGHT stab); false: horizontal Z-band (TOP↔BOTTOM).
    [SerializeField] private bool rowsAreXBands = true;

    [Header("Timing")]
    [SerializeField] private float cycleInterval = 5f;       // total cycle
    [SerializeField] private float warnDuration = 1.5f;     // telegraph on screen
    [SerializeField] private bool useUnscaledTime = true;   // ignore Time.timeScale

    [Header("UI (Screen Space)")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image markerPrefab;
    [SerializeField] private Color markerColor = new Color(1f, 0f, 0f, 0.6f);

    [Header("Tentacle (world-space)")]
    [SerializeField] private GameObject tentaclePrefab;      // pivot at base
    [SerializeField] private Transform tentacleGraphicRoot;  // optional: child to scale
    [SerializeField] private string tentacleGraphicChildPath = ""; // e.g. "Pivot/Mesh"
    [SerializeField] private Camera mainCam;                 // if null -> Camera.main
    [SerializeField] private float attackPlaneY = 0f;        // reference Y

    [Header("Stab Animation")]
    [SerializeField] private Axis lengthAxis = Axis.Z;     // default; may be overridden
    public enum Axis { X, Y, Z }
    [SerializeField] private float extendTime = 0.35f;      // slower = clearer
    [SerializeField] private float holdTime = 0.30f;
    [SerializeField] private float retractTime = 0.50f;
    [SerializeField, Tooltip("Global playback multiplier. 1=normal, 0.5=slower, 2=faster")]
    private float stabPlaybackSpeed = 0.7f;
    [SerializeField, Tooltip("Guarantee visibility at full extension (seconds).")]
    private float minPeakSeconds = 0.25f;
    [SerializeField] private bool debugFreezeAtPeak = false;

    [Header("Length & Thickness")]
    [SerializeField] private float minLength = 1f;
    [SerializeField] private float extraLengthMargin = 0.3f;
    [SerializeField, Tooltip("Reach multiplier along LENGTH axis (not thickness).")]
    private float stabLengthMultiplier = 1.5f;
    [SerializeField, Tooltip("Enable damage collider only while fully extended")]
    private bool enableDamageOnlyWhenExtended = true;
    [SerializeField] private Collider damageCollider; // optional; else auto-find
    [SerializeField, Tooltip("Used if base localScale on length axis is 0")]
    private float minVisibleAxisScale = 1f;

    [Header("Prefab Facing Fix")]
    [SerializeField] private Axis prefabForwardAxis = Axis.Z;       // set Y if mesh points up
    [SerializeField] private Vector3 prefabExtraEuler = Vector3.zero; // e.g. (-90,0,0)

    [Header("Axis Control")]
    [SerializeField, Tooltip("Force length axis to X for X-bands, Z for Z-bands")]
    private bool forceAxisByBand = true;

    [SerializeField, Tooltip("Ignore auto/row rules and use a fixed axis for length scaling.")]
    private bool overrideLengthAxis = false;

    [SerializeField, Tooltip("Axis to scale as LENGTH *after* prefab-forward fix.\nIf your mesh's long side is Y in the prefab, set Prefab Forward Axis = Y and set this to Z.")]
    private Axis overrideAxis = Axis.Z;

    [Header("Spawn Padding (screen px)")]
    [SerializeField] private float screenEdgePixelPadding = 24f;

    [Header("Attack gating")]
    [SerializeField] private bool excludeTopAndBottomRows = true; // skip outer rows
    [SerializeField] private bool enableColumnAttacks = false;    // columns off

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;
    [SerializeField] private float debugLineSeconds = 2f;

    private RectTransform canvasRT;
    private Coroutine loopCo;

    private enum BandType { Row, Column }
    private enum Side { Left, Right, Top, Bottom }

    private struct MarkerInfo
    {
        public RectTransform rt;        // for cleanup
        public BandType type;
        public int index;
        public Side side;
        public Vector2 screenCenterPx;  // cached (Canvas-scaler safe)
    }

    void Awake()
    {
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        if (!rootCanvas) { Debug.LogError("[Kraken] No Canvas."); enabled = false; return; }
        canvasRT = rootCanvas.GetComponent<RectTransform>();
        if (!mainCam) mainCam = Camera.main;
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
            int minRow = excludeTopAndBottomRows ? 1 : 0;
            int maxRowExclusive = excludeTopAndBottomRows ? rows - 1 : rows;

            if (maxRowExclusive - minRow <= 0)
            {
                Debug.LogWarning("[Kraken] No inner rows (rows must be >=3 when excluding edges).");
                yield return WaitSmart(Mathf.Max(0.25f, cycleInterval));
                continue;
            }

            int index = Random.Range(minRow, maxRowExclusive);
            var m = SpawnMarker(BandType.Row, index);

            yield return WaitSmart(warnDuration);
            if (m.rt) Destroy(m.rt.gameObject); // safe, position cached

            FireTentacleStab(m);

            float wait = Mathf.Max(0f, cycleInterval - warnDuration);
            if (wait > 0f) yield return WaitSmart(wait);
        }
    }

    MarkerInfo SpawnMarker(BandType type, int index)
    {
        if (type == BandType.Column && !enableColumnAttacks) type = BandType.Row;

        var img = markerPrefab ? Instantiate(markerPrefab, rootCanvas.transform) : CreateDefaultMarker();
        img.color = markerColor; img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;

        float w = canvasRT.rect.width, h = canvasRT.rect.height;
        float cellW = w / Mathf.Max(1, cols);
        float cellH = h / Mathf.Max(1, rows);
        rt.sizeDelta = new Vector2(cellW * 0.5f, cellH * 0.5f);

        var info = new MarkerInfo { rt = rt, type = type, index = index, side = Side.Top };

        if (rowsAreXBands)
        {
            // "Row" = X-band (vertical slice) -> warn from LEFT or RIGHT, align Y by index
            bool fromLeft = Random.value < 0.5f;
            float x = fromLeft ? 0f : (w - rt.sizeDelta.x);
            float y = h - ((index + 1) * cellH) + (cellH - rt.sizeDelta.y) * 0.5f;
            rt.anchoredPosition = new Vector2(x, y);
            info.side = fromLeft ? Side.Left : Side.Right;
        }
        else
        {
            // "Row" = Z-band (horizontal) -> warn from TOP or BOTTOM, align X by index
            bool fromTop = Random.value < 0.5f;
            float x = (index + 0.5f) * cellW - rt.sizeDelta.x * 0.5f;
            float y = fromTop ? (h - rt.sizeDelta.y) : 0f;
            rt.anchoredPosition = new Vector2(x, y);
            info.side = fromTop ? Side.Top : Side.Bottom;
        }

        info.screenCenterPx = GetMarkerCenterScreenPx(rt, rootCanvas);
        return info;
    }

    Image CreateDefaultMarker()
    {
        var go = new GameObject("KrakenMarker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rootCanvas.transform, false);
        return go.GetComponent<Image>();
    }

    void FireTentacleStab(MarkerInfo m)
    {
        if (!tentaclePrefab || !mainCam)
        {
            if (logDebug) Debug.LogWarning("[Kraken] Missing tentaclePrefab/mainCam.");
            return;
        }

        Vector2 markerCenterPx = m.screenCenterPx;

        float sw = Screen.width, sh = Screen.height;
        float pad = Mathf.Clamp(screenEdgePixelPadding, 0f, Mathf.Max(0f, Mathf.Min(sw, sh) * 0.49f));

        Vector2 basePx, tipPx;
        if (rowsAreXBands)
        {
            // LEFT/RIGHT stab
            if (m.side == Side.Left)
            {
                basePx = new Vector2(pad, markerCenterPx.y);
                tipPx = new Vector2(sw - pad, markerCenterPx.y);
            }
            else
            {
                basePx = new Vector2(sw - pad, markerCenterPx.y);
                tipPx = new Vector2(pad, markerCenterPx.y);
            }
        }
        else
        {
            // TOP/BOTTOM stab
            if (m.side == Side.Top)
            {
                basePx = new Vector2(markerCenterPx.x, sh - pad);
                tipPx = new Vector2(markerCenterPx.x, pad);
            }
            else
            {
                basePx = new Vector2(markerCenterPx.x, pad);
                tipPx = new Vector2(markerCenterPx.x, sh - pad);
            }
        }

        Vector3 baseWorld = ScreenToWorldOnCameraPlane(basePx, attackPlaneY);
        Vector3 tipWorld = ScreenToWorldOnCameraPlane(tipPx, attackPlaneY);
        baseWorld.y = attackPlaneY; tipWorld.y = attackPlaneY;

        Debug.DrawLine(baseWorld, tipWorld, Color.yellow, debugLineSeconds);

        Vector3 dir = tipWorld - baseWorld;
        float len = dir.magnitude;
        if (len < 0.01f)
        {
            Vector3 camRight = Vector3.ProjectOnPlane(mainCam.transform.right, -mainCam.transform.forward).normalized;
            if (camRight.sqrMagnitude < 1e-4f) camRight = Vector3.right;
            tipWorld = baseWorld + camRight * 2f;
            dir = tipWorld - baseWorld; len = dir.magnitude;
            if (logDebug) Debug.LogWarning("[Kraken] Length ~0; forcing minimal stab.");
        }

        var go = Instantiate(tentaclePrefab, baseWorld, Quaternion.identity);
        var gfx = ResolveGraphic(go.transform, tentacleGraphicRoot, tentacleGraphicChildPath);

        // Orientation in CAMERA plane
        Vector3 camUp = mainCam.transform.up;
        Vector3 camNorm = -mainCam.transform.forward;
        Vector3 stabDir = Vector3.ProjectOnPlane(dir, camNorm).normalized;

        Quaternion toWorld = Quaternion.LookRotation(stabDir, camUp);
        Quaternion rotFix = Quaternion.FromToRotation(AxisVector(prefabForwardAxis), Vector3.forward)
                            * Quaternion.Euler(prefabExtraEuler);
        go.transform.rotation = toWorld * rotFix;

        // ---- choose axis (override → force-by-band → alignment fallback) ----
        Axis activeAxis;
        if (overrideLengthAxis)
        {
            activeAxis = overrideAxis;
        }
        else if (forceAxisByBand)
        {
            // Rows-as-X-bands = LEFT/RIGHT → scale along X; Z-bands → scale along Z
            activeAxis = rowsAreXBands ? Axis.X : Axis.Z;
        }
        else
        {
            activeAxis = ChooseLengthAxisByAlignment(gfx, stabDir);
        }

        // Compute reach and factor along *activeAxis*
        float desiredLen = Mathf.Max(minLength, len * stabLengthMultiplier + extraLengthMargin);
        float prefabLen = GetPrefabBaseLength(gfx, activeAxis);
        float targetFactor = desiredLen / Mathf.Max(0.0001f, prefabLen);

        // Start fully retracted; preserve thickness axes
        Vector3 baseScale = gfx.localScale;
        float axisBase = GetAxis(baseScale, activeAxis);
        if (Mathf.Abs(axisBase) < 1e-4f) axisBase = minVisibleAxisScale;

        Vector3 s0 = baseScale; SetAxis(ref s0, activeAxis, 0f); gfx.localScale = s0;

        // Collider gate
        Collider col = damageCollider ? damageCollider : go.GetComponentInChildren<Collider>();
        if (col && enableDamageOnlyWhenExtended) col.enabled = false;

        StartCoroutine(StabRoutine_Factor_Axis(go, gfx, baseScale, axisBase, targetFactor, col, activeAxis));
    }

    IEnumerator StabRoutine_Factor_Axis(GameObject go, Transform gfx, Vector3 baseScale,
                                        float axisBase, float targetFactor, Collider col, Axis axis)
    {
        float Speed() => Mathf.Max(0.01f, stabPlaybackSpeed);
        float ExtDur() => extendTime / Speed();
        float HoldDur() => holdTime / Speed();
        float RetDur() => retractTime / Speed();
        float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        void ClampPlaneY()
        {
            if (!go) return;
            var p = go.transform.position;
            go.transform.position = new Vector3(p.x, attackPlaneY, p.z); // keep glued to plane
        }

        // EXTEND
        float t = 0f, ext = Mathf.Max(0.01f, ExtDur());
        while (t < ext && go)
        {
            t += Delta();
            float k = Mathf.Clamp01(t / ext);
            float f = 1f - (1f - k) * (1f - k); // easeOutQuad
            float factor = Mathf.Lerp(0f, targetFactor, f);
            Vector3 s = baseScale; SetAxis(ref s, axis, factor * axisBase);
            gfx.localScale = s; ClampPlaneY(); yield return null;
        }

        if (go && col && enableDamageOnlyWhenExtended) col.enabled = true;

        // PEAK HOLD (guaranteed)
        float hold = Mathf.Max(minPeakSeconds, HoldDur());
        if (go && (hold > 0f || debugFreezeAtPeak))
        {
            if (debugFreezeAtPeak) hold = Mathf.Max(hold, 999f);
            yield return WaitSmart(hold);
        }

        if (go && col && enableDamageOnlyWhenExtended) col.enabled = false;

        // RETRACT
        t = 0f; float ret = Mathf.Max(0.01f, RetDur());
        while (t < ret && go)
        {
            t += Delta();
            float k = Mathf.Clamp01(t / ret);
            float f = k * k; // easeInQuad
            float factor = Mathf.Lerp(targetFactor, 0f, f);
            Vector3 s = baseScale; SetAxis(ref s, axis, factor * axisBase);
            gfx.localScale = s; ClampPlaneY(); yield return null;
        }

        if (go) Destroy(go);
    }

    // ---------- Helpers ----------
    static void SetAxis(ref Vector3 v, Axis axis, float value)
    {
        switch (axis) { case Axis.X: v.x = value; break; case Axis.Y: v.y = value; break; default: v.z = value; break; }
    }
    static float GetAxis(Vector3 v, Axis axis)
    {
        switch (axis) { case Axis.X: return v.x; case Axis.Y: return v.y; default: return v.z; }
    }
    static Vector3 AxisVector(Axis a)
    {
        switch (a) { case Axis.X: return Vector3.right; case Axis.Y: return Vector3.up; default: return Vector3.forward; }
    }

    // Alignment fallback (kept for completeness if you disable force/override)
    Axis ChooseLengthAxisByAlignment(Transform gfx, Vector3 stabDirWorld)
    {
        Vector3 camNorm = -mainCam.transform.forward;
        stabDirWorld = Vector3.ProjectOnPlane(stabDirWorld, camNorm);
        if (stabDirWorld.sqrMagnitude < 1e-6f) return Axis.Z;
        stabDirWorld.Normalize();

        Vector3 xw = Vector3.ProjectOnPlane(gfx.TransformDirection(Vector3.right), camNorm).normalized;
        Vector3 yw = Vector3.ProjectOnPlane(gfx.TransformDirection(Vector3.up), camNorm).normalized;
        Vector3 zw = Vector3.ProjectOnPlane(gfx.TransformDirection(Vector3.forward), camNorm).normalized;

        float dx = Mathf.Abs(Vector3.Dot(xw, stabDirWorld));
        float dy = Mathf.Abs(Vector3.Dot(yw, stabDirWorld));
        float dz = Mathf.Abs(Vector3.Dot(zw, stabDirWorld));

        // prefer X/Z over Y in top-down
        dy *= 0.5f;

        if (dx >= dy && dx >= dz) return Axis.X;
        if (dz >= dy && dz >= dx) return Axis.Z;
        return Axis.Z;
    }

    Transform ResolveGraphic(Transform root, Transform hinted, string childPath)
    {
        if (!string.IsNullOrEmpty(childPath))
        {
            var t = root.Find(childPath); if (t) return t;
        }
        if (hinted)
        {
            var t = root.Find(hinted.name); if (t) return t;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == hinted.name) return all[i];
        }
        return root;
    }

    Vector2 GetMarkerCenterScreenPx(RectTransform rt, Canvas canvas)
    {
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
        Camera uiCam = (canvas && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);
    }

    float GetPrefabBaseLength(Transform gfx, Axis axis)
    {
        var rends = gfx.GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = new Bounds(rends[0].bounds.center, Vector3.zero);
            for (int i = 0; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 size = b.size;
            switch (axis)
            {
                case Axis.X: return Mathf.Max(0.0001f, size.x);
                case Axis.Y: return Mathf.Max(0.0001f, size.y);
                default: return Mathf.Max(0.0001f, size.z);
            }
        }

        var mfs = gfx.GetComponentsInChildren<MeshFilter>();
        if (mfs != null && mfs.Length > 0 && mfs[0].sharedMesh)
        {
            var mf = mfs[0];
            Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            switch (axis)
            {
                case Axis.X: return Mathf.Max(0.0001f, size.x);
                case Axis.Y: return Mathf.Max(0.0001f, size.y);
                default: return Mathf.Max(0.0001f, size.z);
            }
        }

        return 1f;
    }

    Vector3 ScreenToWorldOnCameraPlane(Vector2 pixelPos, float planeY)
    {
        if (!mainCam)
        {
            Debug.LogError("[Kraken] No camera.");
            return new Vector3(0f, planeY, 0f);
        }

        Ray ray = mainCam.ScreenPointToRay(new Vector3(pixelPos.x, pixelPos.y, 0f));
        Plane camPlane = new Plane(-mainCam.transform.forward, new Vector3(0f, planeY, 0f));
        if (camPlane.Raycast(ray, out float enterCam))
            return ray.GetPoint(enterCam);

        // Fallback: world-Y plane
        Plane yPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (yPlane.Raycast(ray, out float enterY))
            return ray.GetPoint(enterY);

        return mainCam.ScreenToWorldPoint(new Vector3(pixelPos.x, pixelPos.y, 10f));
    }

    // smart wait (scaled/unscaled)
    IEnumerator WaitSmart(float seconds)
    {
        if (useUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }
}
