using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class EnsureBoxCollider : MonoBehaviour
{
    [SerializeField] private bool trigger = true;

    void Awake()
    {
        // Remove any non-box colliders on this object or its children
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (!(c is BoxCollider))
                Destroy(c);
        }

        var box = GetComponent<BoxCollider>();
        box.isTrigger = trigger;

        // Optional: if your projectile has visible mesh, auto-fit once
        // var rend = GetComponentInChildren<Renderer>();
        // if (rend) box.size = rend.bounds.size; // careful: world->local scaling
    }

    void OnEnable()
    {
        // Just in case the pool re-enabled an old instance before Awake ever ran
        var box = GetComponent<BoxCollider>();
        if (box) box.enabled = true;
    }
}
