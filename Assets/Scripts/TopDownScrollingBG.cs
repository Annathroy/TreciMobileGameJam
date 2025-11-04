using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ParallaxPlane3D_Seq : MonoBehaviour
{
    [Header("Camera / Plane")]
    public Camera cam;                    // leave null => Camera.main
    public float planeDistance = 20f;     // in front of camera along cam.forward

    [Header("Textures (A -> B -> C -> loop)")]
    public Texture2D[] sequence;          // assign PNGs here (identical pixel size/aspect)
    public bool randomizeOnWrap = false;  // optional: pick random on each wrap

    [Header("Tile / Material")]
    public Shader unlitShader;            // default: URP Unlit
    public Vector2 tileWorldSize = new Vector2(10f, 10f);
    public Color tint = Color.white;

    [Header("Scroll & Parallax")]
    [Tooltip("World units/sec DOWN the screen. Positive = down.")]
    public float scrollSpeed = 2f;
    [Range(0f, 1f)] public float parallax = 0.2f; // camera-move influence

    [Header("Coverage")]
    [Min(0)] public int extraTiles = 1;

    [Header("Sorting / Render")]
    public int sortingOrder = -1000;
    public string sortingLayerName = "";  // optional
    public int forcedRenderQueue = -1;    // -1: keep default (e.g., 1999 for background)

    // ----- internals -----
    Transform planeRoot;
    Material baseMat;
    readonly List<Renderer> rends = new();
    int nx, ny;

    // UV offsets split into auto-scroll and parallax so only auto triggers swaps
    float autoOffY;           // 0..1 progress purely from scrollSpeed (down)
    float parOffX, parOffY;   // parallax offsets (0..1)
    Vector3 lastCamPos;

    int seqIndex;             // current texture index
    Texture2D currentTex;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!cam) { Debug.LogError("No Camera"); enabled = false; return; }

        if (sequence == null || sequence.Length == 0 || sequence[0] == null)
        {
            Debug.LogError("Assign Textures in 'sequence' (A, B, C...)."); enabled = false; return;
        }

        if (!unlitShader)
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        baseMat = new Material(unlitShader);
        baseMat.color = tint;
        if (forcedRenderQueue >= 0) baseMat.renderQueue = forcedRenderQueue;

        // Camera-locked plane
        planeRoot = new GameObject($"{name}_PlaneRoot").transform;
        planeRoot.SetParent(cam.transform, false);
        planeRoot.localPosition = new Vector3(0, 0, planeDistance);
        planeRoot.localRotation = Quaternion.identity;
        planeRoot.localScale = Vector3.one;

        // First texture
        seqIndex = 0;
        currentTex = sequence[seqIndex];

        BuildGrid();
        lastCamPos = cam.transform.position;
        ApplyTextureToAll(currentTex); // initial
    }

    void LateUpdate()
    {
        // 1) Auto scroll DOWN (positive speed => down) in tile units
        float dyTiles = (scrollSpeed / Mathf.Max(0.0001f, tileWorldSize.y)) * Time.deltaTime;
        autoOffY = Repeat01(autoOffY + dyTiles);

        // 2) Camera parallax (project onto camera plane)
        Vector3 camDelta = cam.transform.position - lastCamPos;
        lastCamPos = cam.transform.position;

        float px = Vector3.Dot(camDelta, cam.transform.right) * parallax / Mathf.Max(0.0001f, tileWorldSize.x);
        float py = Vector3.Dot(camDelta, cam.transform.up) * parallax / Mathf.Max(0.0001f, tileWorldSize.y);
        parOffX = Repeat01(parOffX + px);
        parOffY = Repeat01(parOffY + py);

        // 3) When auto component crosses a full tile, advance sequence
        // Compute total auto progress this frame in tiles and detect wraps robustly
        // (works even with very high speeds/frame skips)
        float autoYTotal = autoOffY;                // 0..1 after adding dyTiles above
        // We need how many whole tiles were traversed this frame:
        // The simplest: maintain a separate accumulator and check integer steps.
        // Here we implement by tracking a running sum in tiles using Time.deltaTime.
        // For stability, detect wraps by checking if adding dyTiles pushed over 1 multiple times:
        int steps = Mathf.FloorToInt((dyTiles > 0 ? dyTiles : 0) + autoOffY); // conservative
        // More reliable: compute in world units
        // But dyTiles is tiny; multiple wraps are unlikely unless speed is huge.
        // We'll handle generic case:
        if (dyTiles > 0f)
        {
            float prev = Repeat01(autoOffY - dyTiles);
            steps = Mathf.FloorToInt(prev + dyTiles);
            if (steps < 0) steps = 0;
        }
        for (int i = 0; i < steps; i++) AdvanceSequence();

        // 4) Compose final UV offset (URP: _BaseMap_ST = (scale.x, scale.y, offset.x, offset.y))
        Vector2 uv = new Vector2(parOffX, Repeat01(autoOffY + parOffY));

        var mpb = new MaterialPropertyBlock();
        foreach (var r in rends)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", currentTex);
            mpb.SetVector("_BaseMap_ST", new Vector4(1f, 1f, uv.x, uv.y));
            r.SetPropertyBlock(mpb);
        }

        // Keep plane in front of camera
        planeRoot.localPosition = new Vector3(0, 0, planeDistance);
        planeRoot.localRotation = Quaternion.identity;
    }

    void AdvanceSequence()
    {
        if (sequence.Length == 0) return;

        if (randomizeOnWrap)
        {
            int next;
            // avoid immediate repeat
            do { next = Random.Range(0, sequence.Length); } while (sequence.Length > 1 && next == seqIndex);
            seqIndex = next;
        }
        else
        {
            seqIndex = (seqIndex + 1) % sequence.Length;
        }
        currentTex = sequence[seqIndex];
        ApplyTextureToAll(currentTex);
    }

    void BuildGrid()
    {
        rends.Clear();

        if (!cam.orthographic)
            Debug.LogWarning("Use an ORTHOGRAPHIC camera for consistent tile coverage.");

        float viewW = 2f * cam.orthographicSize * cam.aspect;
        float viewH = 2f * cam.orthographicSize;

        nx = Mathf.Max(1, Mathf.CeilToInt(viewW / tileWorldSize.x) + 2 * extraTiles);
        ny = Mathf.Max(1, Mathf.CeilToInt(viewH / tileWorldSize.y) + 2 * extraTiles);

        int left = nx / 2, right = nx - 1 - left;
        int down = ny / 2, up = ny - 1 - down;

        for (int iy = -down; iy <= up; iy++)
            for (int ix = -left; ix <= right; ix++)
            {
                var q = CreateQuad($"Tile_{ix}_{iy}");
                q.SetParent(planeRoot, false);
                q.localPosition = new Vector3(ix * tileWorldSize.x, iy * tileWorldSize.y, 0);
                q.localRotation = Quaternion.identity;
                q.localScale = new Vector3(tileWorldSize.x, tileWorldSize.y, 1f);
            }
    }

    Transform CreateQuad(string n)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = n;

        var col = go.GetComponent<Collider>(); if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = baseMat;
        if (!string.IsNullOrEmpty(sortingLayerName)) mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = sortingOrder;

        rends.Add(mr);
        return go.transform;
    }

    void ApplyTextureToAll(Texture2D tex)
    {
        // Update base material for Scene view; runtime uses MPBs
        baseMat.mainTexture = tex;

        if (forcedRenderQueue >= 0) baseMat.renderQueue = forcedRenderQueue;

        var mpb = new MaterialPropertyBlock();
        foreach (var r in rends)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", tex);                 // URP Unlit
            mpb.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
            r.SetPropertyBlock(mpb);
        }
    }

    static float Repeat01(float x)
    {
        x %= 1f;
        if (x < 0f) x += 1f;
        return x;
    }

    void OnDestroy()
    {
        if (baseMat) Destroy(baseMat);
        if (planeRoot) Destroy(planeRoot.gameObject);
    }
}



