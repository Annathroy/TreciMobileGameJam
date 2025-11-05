using UnityEngine;

/// <summary>
/// Automatically attached by the pool when an instance is created.
/// Tracks which prefab this instance belongs to so it can be returned properly.
/// </summary>
public class PooledObject : MonoBehaviour
{
    [HideInInspector]
    public GameObject SourcePrefab;
}
