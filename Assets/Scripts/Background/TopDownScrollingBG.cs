using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ParallaxScroller3D : MonoBehaviour
{
    [Header("Camera / Plane")]
    [SerializeField] private Camera cam;          // Leave null to use Camera.main
    [SerializeField] private float groundY = 0f;  // Y height where quads are placed

    [Header("Images")]
    [SerializeField] private Texture2D[] sequence;       // Assign PNG/JPGs
    [Tooltip("Random order with no immediate repeats, forever.")]
    [SerializeField] private bool randomizeOrder = true;

    [Header("Sizing")]
    [SerializeField] private Vector2 imageWorldSize = new Vector2(20f, 20f); // X = width, Y = height along scroll
    [SerializeField] private float spacing = 0f; // 0 for edge-to-edge

    [Header("Scroll & Parallax")]
    [SerializeField] private float scrollSpeed = 8f; // + moves toward -Z by default
    [Range(0f, 1f)][SerializeField] private float parallax = 0.15f;

    [Header("Rendering")]
    [SerializeField] private Shader unlitShader;          // URP Unlit preferred; falls back
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = -1000;
    [Tooltip("-1 keeps default; e.g. 1000 for Background")]
    [SerializeField] private int forcedRenderQueue = -1;
    [Tooltip("Optional: put tiles on this layer and ensure the camera renders it.")]
    [SerializeField] private int layer = 0;

    [Header("Axis Mode")]
    [SerializeField] private bool forceTopDownXZ = true; // For strict top-down rigs

    // ---- internals ----
    private struct Tile
    {
        public Transform t;
        public MeshRenderer mr;
        public float posAlong; // along scroll axis in world units
        public int texIndex;
    }

    private readonly List<Tile> tiles = new();
    private Material baseMat;
    private MaterialPropertyBlock mpb;
    private Vector3 lastCamPos;
    private Vector3 axisDir;   // screen-down projected on XZ (default: -Z)
    private Vector3 axisRight; // perpendicular in plane (default: +X)
    private float tileSpan;    // imageWorldSize.y + spacing
    private int tileCount;     // ring size
    private bool isOrtho;

    // bag randomizer state
    private int lastServed = -1;
    private readonly List<int> bag = new();
    private int bagPos = 0;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!cam) { Debug.LogError("[ParallaxScroller3D] No camera."); enabled = false; return; }
        if (sequence == null || sequence.Length == 0) { Debug.LogError("[ParallaxScroller3D] Assign textures."); enabled = false; return; }

        isOrtho = cam.orthographic;

        if (!unlitShader)
        {
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!unlitShader) unlitShader = Shader.Find("Unlit/Texture");
            if (!unlitShader) unlitShader = Shader.Find("Sprites/Default"); // last resort
        }

        baseMat = new Material(unlitShader) { color = tint };
        if (forcedRenderQueue >= 0) baseMat.renderQueue = forcedRenderQueue;

        mpb = new MaterialPropertyBlock();

        InitAxes();
        BuildTiles();
        InitBag(); // after BuildTiles so lastServed can be set properly if needed

        lastCamPos = cam.transform.position;
    }

    private void OnDestroy()
    {
        if (baseMat) Destroy(baseMat);
    }

    private void InitAxes()
    {
        if (forceTopDownXZ)
        {
            axisDir = Vector3.back;   // Negative Z = down the screen for top-down camera
            axisRight = Vector3.right;
        }
        else
        {
            axisDir = Vector3.ProjectOnPlane(-cam.transform.up, Vector3.up).normalized;
            if (axisDir.sqrMagnitude < 1e-4f) axisDir = Vector3.back;
            axisRight = Vector3.Cross(Vector3.up, axisDir).normalized;
        }

        tileSpan = Mathf.Max(0.01f, imageWorldSize.y + spacing);
    }

    private void BuildTiles()
    {
        float viewSpan = EstimateViewSpanAlongAxis();

        // Big safety buffer to survive frame hitches / very high speeds
        tileCount = Mathf.Max(5, Mathf.CeilToInt(viewSpan / tileSpan) + 6);

        float camAlong = PosAlongFromWorld(cam.transform.position);
        float halfView = viewSpan * 0.5f;

        // Start a little ahead of view, fill toward back
        float startFront = camAlong + halfView + tileSpan;

        // Temp: use simple next index during initial fill to avoid duplicates in a row
        lastServed = -1;

        for (int i = 0; i < tileCount; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"ParallaxTile_{i}";
            quad.layer = layer;
            quad.transform.SetParent(transform, false);

            // Face upward toward the camera (top-down)
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(imageWorldSize.x, imageWorldSize.y, 1f);

            var col = quad.GetComponent<Collider>();
            if (col) Destroy(col);

            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = baseMat;
            if (!string.IsNullOrEmpty(sortingLayerName)) mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.allowOcclusionWhenDynamic = false;

            float tilePos = startFront - i * tileSpan;

            int idx = NextIndexNoRepeat(); // initial assignment also respects no-duplicate rule

            var tile = new Tile
            {
                t = quad.transform,
                mr = mr,
                posAlong = tilePos,
                texIndex = idx
            };

            ApplyTexture(tile.mr, sequence[tile.texIndex]);
            SetTileWorldTransform(ref tile);
            tiles.Add(tile);
        }
    }

    private float EstimateViewSpanAlongAxis()
    {
        if (isOrtho) return cam.orthographicSize * 2f;

        // Perspective: estimate ground-plane height coverage at groundY
        var camPos = cam.transform.position;
        var rayDir = cam.transform.forward;
        if (Mathf.Abs(rayDir.y) < 1e-4f) return 30f;

        float t = (groundY - camPos.y) / rayDir.y;
        if (t < 0f) t = Mathf.Abs(t);
        float dist = t;

        float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
        return Mathf.Max(0.01f, halfHeight * 2f);
    }

    private void LateUpdate()
    {
        if (tiles.Count == 0) return;

        // Camera parallax
        var camPos = cam.transform.position;
        var camDelta = camPos - lastCamPos;
        lastCamPos = camPos;

        float parallaxOffset = Vector3.Dot(camDelta, axisDir) * parallax;
        float deltaMove = (scrollSpeed * Time.deltaTime) + parallaxOffset;

        // Move tiles forward (toward axisDir)
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            tile.posAlong += deltaMove;
            SetTileWorldTransform(ref tile);
            tiles[i] = tile;
        }

        // --- Correct recycle logic (front -> back) ---
        float viewSpan = EstimateViewSpanAlongAxis();
        float halfView = viewSpan * 0.5f;
        float camAlong = PosAlongFromWorld(camPos);

        // Anything beyond this is too FAR IN FRONT (past the bottom of the screen).
        float frontThreshold = camAlong + halfView + tileSpan;

        // Find current BACK-most tile (smallest posAlong).
        float backMost = float.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].posAlong < backMost) backMost = tiles[i].posAlong;

        // Place recycled tiles BEHIND the current backmost, in steps of -tileSpan.
        float nextBackPos = backMost - tileSpan;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];

            // If tile has moved too far in front, wrap it to the back.
            while (tile.posAlong > frontThreshold)
            {
                tile.posAlong = nextBackPos;
                nextBackPos -= tileSpan;

                // Random next with no immediate repeat if you used the bag system:
                int idx = NextFromBag(); // or NextTextureIndex() if you kept that
                if (idx != tile.texIndex)
                {
                    tile.texIndex = idx;
                    ApplyTexture(tile.mr, sequence[tile.texIndex]);
                }
            }

            SetTileWorldTransform(ref tile);
            tiles[i] = tile;
        }
    }


    // ----- Random order with no immediate repeats -----

    private void InitBag()
    {
        bag.Clear();
        if (sequence == null || sequence.Length == 0) return;
        if (sequence.Length == 1)
        {
            bag.Add(0);
            bagPos = 0;
            lastServed = 0;
            return;
        }

        // fill 0..N-1 and shuffle
        for (int i = 0; i < sequence.Length; i++) bag.Add(i);
        Shuffle(bag);

        // ensure first != lastServed (only matters after a refill)
        if (lastServed >= 0 && bag[0] == lastServed)
        {
            // swap with any different index
            for (int i = 1; i < bag.Count; i++)
            {
                if (bag[i] != lastServed)
                {
                    (bag[0], bag[i]) = (bag[i], bag[0]);
                    break;
                }
            }
        }

        bagPos = 0;
    }

    private int NextFromBag()
    {
        if (!randomizeOrder)
        {
            // deterministic cycle
            int idx = (lastServed + 1 + sequence.Length) % sequence.Length;
            lastServed = idx;
            return idx;
        }

        if (sequence.Length == 1)
        {
            lastServed = 0;
            return 0;
        }

        if (bagPos >= bag.Count) InitBag();

        int pick = bag[bagPos++];
        lastServed = pick;
        return pick;
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // one-off use for initial fill (no immediate repeat)
    private int NextIndexNoRepeat()
    {
        if (sequence == null || sequence.Length == 0) return 0;
        if (sequence.Length == 1) { lastServed = 0; return 0; }

        int pick;
        do { pick = Random.Range(0, sequence.Length); }
        while (pick == lastServed);
        lastServed = pick;
        return pick;
    }

    // ----- Geometry helpers -----

    private float PosAlongFromWorld(Vector3 worldPos)
    {
        var p = new Vector3(worldPos.x, groundY, worldPos.z);
        return Vector3.Dot(p, axisDir);
    }

    private Vector3 WorldFromPosAlong(float posAlong)
    {
        var baseCenter = new Vector3(cam.transform.position.x, groundY, cam.transform.position.z);
        float baseDot = Vector3.Dot(baseCenter, axisDir);
        return baseCenter + axisDir * (posAlong - baseDot);
    }

    private void SetTileWorldTransform(ref Tile tile)
    {
        Vector3 center = WorldFromPosAlong(tile.posAlong);
        tile.t.position = center;

        if (axisRight.sqrMagnitude > 1e-4f)
        {
            float yaw = Mathf.Atan2(axisRight.x, axisRight.z) * Mathf.Rad2Deg;
            tile.t.rotation = Quaternion.Euler(90f, yaw, 0f);
        }

        tile.t.localScale = new Vector3(imageWorldSize.x, imageWorldSize.y, 1f);
    }

    private void ApplyTexture(MeshRenderer mr, Texture2D tex)
    {
        if (!tex) return;
        mpb ??= new MaterialPropertyBlock();
        mpb.Clear();
        mpb.SetTexture("_BaseMap", tex); // URP Unlit
        mpb.SetTexture("_MainTex", tex); // Built-in/Sprites fallback
        mr.SetPropertyBlock(mpb);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (imageWorldSize.x < 0.01f) imageWorldSize.x = 0.01f;
        if (imageWorldSize.y < 0.01f) imageWorldSize.y = 0.01f;
        tileSpan = Mathf.Max(0.01f, imageWorldSize.y + spacing);

        if (!cam) cam = Camera.main;
        if (cam) InitAxes();

        if (baseMat != null)
        {
            baseMat.color = tint;
            if (forcedRenderQueue >= 0) baseMat.renderQueue = forcedRenderQueue;
        }

        // live update sizes/orientation
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (!tile.t) continue;
            tile.t.localScale = new Vector3(imageWorldSize.x, imageWorldSize.y, 1f);
            SetTileWorldTransform(ref tile);
        }
    }
#endif
}
