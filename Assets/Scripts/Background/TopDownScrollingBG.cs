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
    [SerializeField] private Texture2D[] sequence;          // Assign your PNG/JPGs
    [SerializeField] private bool randomizeOrder = true;     // Avoid immediate repeats

    [Header("Sizing")]
    [SerializeField] private Vector2 imageWorldSize = new Vector2(20f, 20f); // X = width, Y = height along scroll
    [SerializeField] private float spacing = 0f;                                 // 0 for seamless

    [Header("Scroll & Parallax")]
    [SerializeField] private float scrollSpeed = 8f;          // + = down the screen
    [Range(0f, 1f)][SerializeField] private float parallax = 0.15f;

    [Header("Rendering")]
    [SerializeField] private Shader unlitShader;          // Defaults to URP Unlit, fallback Built-in Unlit/Texture
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
        public float posAlong;    // along scroll axis in world units
        public int texIndex;
    }

    private readonly List<Tile> tiles = new();
    private Material baseMat;
    private MaterialPropertyBlock mpb;
    private Vector3 lastCamPos;
    private Vector3 axisDir;      // screen-down projected on XZ
    private Vector3 axisRight;    // perpendicular in plane
    private float tileSpan;       // imageWorldSize.y + spacing
    private int tileCount;        // ring size
    private int lastTexIndex = -1;
    private bool isOrtho;

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

        baseMat = new Material(unlitShader);
        baseMat.color = tint;
        if (forcedRenderQueue >= 0) baseMat.renderQueue = forcedRenderQueue;

        mpb = new MaterialPropertyBlock();

        InitAxes();
        BuildTiles();

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
            axisDir = Vector3.forward;    // FIXED: Positive Z = down the screen for top-down camera
            axisRight = Vector3.right;    // width
        }
        else
        {
            axisDir = Vector3.ProjectOnPlane(-cam.transform.up, Vector3.up).normalized;
            if (axisDir.sqrMagnitude < 1e-4f) axisDir = Vector3.forward; // FIXED: Positive direction
            axisRight = Vector3.Cross(Vector3.up, axisDir).normalized;
        }
        tileSpan = Mathf.Max(0.01f, imageWorldSize.y + spacing);
    }

    private void BuildTiles()
    {
        float viewSpan = EstimateViewSpanAlongAxis();
        tileCount = Mathf.Max(3, Mathf.CeilToInt(viewSpan / Mathf.Max(0.01f, imageWorldSize.y)) + 2);

        for (int i = 0; i < tileCount; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"ParallaxTile_{i}";
            quad.layer = layer;
            quad.transform.SetParent(transform, worldPositionStays: false);

            // FIXED: Face UP (+Y) so a top-down camera (looking down -Y) sees the front face
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face upward toward camera
            quad.transform.localScale = new Vector3(imageWorldSize.x, imageWorldSize.y, 1f);

            var col = quad.GetComponent<Collider>();
            if (col) Destroy(col);

            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = baseMat;
            if (!string.IsNullOrEmpty(sortingLayerName)) mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // FIXED: Start positioning from camera center for immediate visibility
            float cameraPosAlong = PosAlongFromWorld(cam.transform.position);
            float startPos;
            
            if (i == 0)
            {
                // First tile appears immediately at camera position
                startPos = cameraPosAlong;
            }
            else
            {
                // Subsequent tiles positioned above (negative direction)
                startPos = cameraPosAlong - (i * tileSpan);
            }

            var tile = new Tile
            {
                t = quad.transform,
                mr = mr,
                posAlong = startPos,
                texIndex = NextTextureIndex()
            };

            ApplyTexture(tile.mr, sequence[tile.texIndex]);
            SetTileWorldTransform(tile);
            tiles.Add(tile);
        }
    }

    private float EstimateViewSpanAlongAxis()
    {
        if (isOrtho) return cam.orthographicSize * 2f;

        var camPos = cam.transform.position;
        var ray = new Ray(camPos, cam.transform.forward);
        if (Mathf.Abs(ray.direction.y) < 1e-4f) return 30f;

        float t = (groundY - camPos.y) / ray.direction.y;
        if (t < 0f) t = Mathf.Abs(t);
        float dist = t;

        float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
        return halfHeight * 2f;
    }

    private float StartPosAlong()
    {
        // FIXED: Start at camera position for immediate visibility
        return PosAlongFromWorld(cam.transform.position);
    }

    private void LateUpdate()
    {
        if (tiles.Count == 0) return;

        var camPos = cam.transform.position;
        var camDelta = camPos - lastCamPos;
        lastCamPos = camPos;

        float parallaxOffset = Vector3.Dot(camDelta, axisDir) * parallax;
        float deltaMove = (scrollSpeed * Time.deltaTime) + parallaxOffset;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            tile.posAlong += deltaMove;
            SetTileWorldTransform(tile);
            tiles[i] = tile;
        }

        float halfView = EstimateViewSpanAlongAxis() * 0.5f;
        float bottomLimit = PosAlongFromWorld(camPos) + halfView + imageWorldSize.y * 1.1f;
        float topInsertPos = FindCurrentTopPos() - tileSpan;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile.posAlong > bottomLimit)
            {
                tile.posAlong = topInsertPos;
                topInsertPos -= tileSpan;

                int nextIdx = NextTextureIndex();
                if (nextIdx != tile.texIndex)
                {
                    tile.texIndex = nextIdx;
                    ApplyTexture(tile.mr, sequence[tile.texIndex]);
                }
                SetTileWorldTransform(tile);
                tiles[i] = tile;
            }
        }
    }

    private float FindCurrentTopPos()
    {
        float top = float.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].posAlong < top) top = tiles[i].posAlong;
        return top;
    }

    private float PosAlongFromWorld(Vector3 worldPos)
    {
        var p = new Vector3(worldPos.x, groundY, worldPos.z);
        return Vector3.Dot(p, axisDir);
    }

    private Vector3 WorldFromPosAlong(float posAlong)
    {
        var baseCenter = new Vector3(cam.transform.position.x, groundY, cam.transform.position.z);
        return baseCenter + axisDir * (posAlong - Vector3.Dot(baseCenter, axisDir));
    }

    private void SetTileWorldTransform(Tile tile)
    {
        Vector3 center = WorldFromPosAlong(tile.posAlong);
        tile.t.position = center;

        if (axisRight.sqrMagnitude > 1e-4f)
        {
            float yaw = Mathf.Atan2(axisRight.x, axisRight.z) * Mathf.Rad2Deg;
            tile.t.rotation = Quaternion.Euler(90f, yaw, 0f); // FIXED: Face upward (+Y)
        }
    }

    private int NextTextureIndex()
    {
        if (sequence.Length == 1) return 0;

        if (!randomizeOrder)
        {
            int idx = (lastTexIndex + 1 + sequence.Length) % sequence.Length;
            lastTexIndex = idx;
            return idx;
        }

        int pick;
        if (lastTexIndex < 0) pick = Random.Range(0, sequence.Length);
        else
        {
            do { pick = Random.Range(0, sequence.Length); }
            while (pick == lastTexIndex && sequence.Length > 1);
        }
        lastTexIndex = pick;
        return pick;
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
            SetTileWorldTransform(tile);
        }
    }
#endif
}
