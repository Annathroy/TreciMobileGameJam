using UnityEngine;

[DisallowMultipleComponent]
public class MeshFlipper : MonoBehaviour
{
    [Header("Flip Settings")]
    [Tooltip("If true, flips the mesh 180° around Y-axis at start.")]
    [SerializeField] private bool flipY = true;
    [Tooltip("If true, flips the mesh 180° around Z-axis instead.")]
    [SerializeField] private bool flipZ = false;

    private void Awake()
    {
        Vector3 euler = transform.localEulerAngles;

        if (flipY)
            euler.y += 180f;
        if (flipZ)
            euler.z += 180f;

        transform.localRotation = Quaternion.Euler(euler);
    }
}
