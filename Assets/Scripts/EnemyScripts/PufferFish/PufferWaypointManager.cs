using System.Collections.Generic;
using UnityEngine;

public class PufferWaypointManager : MonoBehaviour
{
    public static PufferWaypointManager Instance;

    [SerializeField] private Transform waypointsParent;
    public List<Transform> Waypoints { get; private set; } = new List<Transform>();
    private bool[] occupied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Waypoints.Clear();
        if (!waypointsParent)
        {
            Debug.LogError("[PufferWaypointManager] Assign the waypoints parent!");
            enabled = false;
            return;
        }

        foreach (Transform t in waypointsParent)
            Waypoints.Add(t);

        occupied = new bool[Waypoints.Count];
    }

    public int GetFreeWaypoint()
    {
        List<int> free = new List<int>();
        for (int i = 0; i < occupied.Length; i++)
            if (!occupied[i]) free.Add(i);

        if (free.Count == 0) return -1;
        int chosen = free[Random.Range(0, free.Count)];
        occupied[chosen] = true;
        return chosen;
    }

    public void ReleaseWaypoint(int index)
    {
        if (index >= 0 && index < occupied.Length)
            occupied[index] = false;
    }

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= Waypoints.Count) return null;
        return Waypoints[index];
    }
}
