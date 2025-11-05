using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ScreenBoundsWalls : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float wallThickness = 5f;   // world units
    [SerializeField] private float wallDepth = 10f;      // along Y for 3D collisions
    [SerializeField] private float planeY = 0f;          // where gameplay happens
    [SerializeField] private LayerMask wallLayer = 0;    // optional "Walls" layer
    [SerializeField] private bool showGizmos = false;

    private Camera cam;
    private BoxCollider[] walls = new BoxCollider[4];

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;           // mobile: use frameRate, not vSync
        Application.targetFrameRate = 120;         // or 120 if your device can handle it
        Time.fixedDeltaTime = 1f / 60f;           // keep physics in sync with visuals
        Time.maximumDeltaTime = 1f / 15f;         // avoid big physics catch-ups
        Physics.autoSyncTransforms = false;       // cheaper
        Physics.sleepThreshold = 0.005f;          // let bodies sleep sooner
        cam = GetComponent<Camera>();

        // Create 4 edge colliders
        for (int i = 0; i < 4; i++)
        {
            GameObject w = new GameObject("ScreenWall_" + i);
            w.transform.parent = transform;
            walls[i] = w.AddComponent<BoxCollider>();
            if (wallLayer != 0)
                w.layer = Mathf.RoundToInt(Mathf.Log(wallLayer.value, 2));
        }

        UpdateWalls();
    }

    private void LateUpdate()
    {
        UpdateWalls();
    }

    private void UpdateWalls()
    {
        if (!cam) return;

        // Distance from camera to gameplay plane (Y difference)
        float distance = Mathf.Abs(cam.transform.position.y - planeY);

        // Corners of the camera view projected onto Y = planeY
        Vector3 bottomLeft = cam.ScreenToWorldPoint(new Vector3(0, 0, distance));
        Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, distance));

        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float depthZ = Mathf.Abs(topRight.z - bottomLeft.z);
        float midX = (bottomLeft.x + topRight.x) / 2f;
        float midZ = (bottomLeft.z + topRight.z) / 2f;

        // Y position for all walls
        float yPos = planeY;

        // LEFT
        walls[0].center = new Vector3(bottomLeft.x - wallThickness * 0.5f, yPos, midZ);
        walls[0].size = new Vector3(wallThickness, wallDepth, depthZ + wallThickness * 2f);

        // RIGHT
        walls[1].center = new Vector3(topRight.x + wallThickness * 0.5f, yPos, midZ);
        walls[1].size = new Vector3(wallThickness, wallDepth, depthZ + wallThickness * 2f);

        // BOTTOM
        walls[2].center = new Vector3(midX, yPos, bottomLeft.z - wallThickness * 0.5f);
        walls[2].size = new Vector3(width + wallThickness * 2f, wallDepth, wallThickness);

        // TOP
        walls[3].center = new Vector3(midX, yPos, topRight.z + wallThickness * 0.5f);
        walls[3].size = new Vector3(width + wallThickness * 2f, wallDepth, wallThickness);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || walls == null) return;
        Gizmos.color = Color.cyan;
        foreach (var w in walls)
        {
            if (w) Gizmos.DrawWireCube(w.bounds.center, w.bounds.size);
        }
    }
}
