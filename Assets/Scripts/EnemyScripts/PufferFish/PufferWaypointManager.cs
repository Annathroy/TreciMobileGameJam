using System.Collections.Generic;
using UnityEngine;

public class PufferWaypointManager : MonoBehaviour
{
    public static PufferWaypointManager Instance;

    [SerializeField] private Transform waypointsParent;
    public List<Transform> Waypoints { get; private set; } = new List<Transform>();

    // Tracks who claimed each slot (0 = free). Use caller.GetInstanceID() as owner.
    private int[] ownerIds;

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

        ownerIds = new int[Waypoints.Count]; // zeroed = free
    }

    // ---- PUBLIC API (owner-aware) ----

    // Claim any free waypoint for this owner. Returns -1 if none.
    public int GetFreeWaypoint(int ownerId)
    {
        CleanupNulls();
        // Collect free indices
        int count = Waypoints.Count;
        int picked = -1;

        // Random free selection
        // (simple linear scan + random start offset to avoid clustering)
        if (count == 0) return -1;
        int start = Random.Range(0, count);
        for (int step = 0; step < count; step++)
        {
            int i = (start + step) % count;
            if (ownerIds[i] == 0)
            {
                picked = i;
                break;
            }
        }

        if (picked != -1) ownerIds[picked] = ownerId;
        return picked;
    }

    // Release only if you own it.
    public void ReleaseWaypoint(int index, int ownerId)
    {
        if (!IndexValid(index)) return;
        if (ownerIds[index] == ownerId) ownerIds[index] = 0;
    }

    // Get transform if still valid AND still owned by you. Otherwise return null.
    public Transform GetWaypoint(int index, int ownerId)
    {
        if (!IndexValid(index)) return null;
        if (ownerIds[index] != ownerId) return null; // no longer yours
        var t = Waypoints[index];
        if (!t) { ownerIds[index] = 0; return null; } // cleaned up
        return t;
    }

    // If your current index is invalid or stolen, try to give you a new one.
    public int ValidateOrReacquire(int currentIndex, int ownerId)
    {
        CleanupNulls();

        if (IndexValid(currentIndex) && ownerIds[currentIndex] == ownerId && Waypoints[currentIndex])
            return currentIndex; // still yours

        // Otherwise, free whatever is there and get a new one
        if (IndexValid(currentIndex) && ownerIds[currentIndex] == ownerId)
            ownerIds[currentIndex] = 0;

        return GetFreeWaypoint(ownerId);
    }

    // ---- INTERNALS ----

    private bool IndexValid(int i) => i >= 0 && i < Waypoints.Count;

    // Handle destroyed/missing children at runtime
    private void CleanupNulls()
    {
        // Fast path: nothing to do
        bool anyNull = false;
        for (int i = 0; i < Waypoints.Count; i++)
        {
            if (!Waypoints[i]) { anyNull = true; break; }
        }
        if (!anyNull) return;

        // Rebuild compacted lists
        var newList = new List<Transform>(Waypoints.Count);
        var newOwners = new List<int>(Waypoints.Count);
        for (int i = 0; i < Waypoints.Count; i++)
        {
            var t = Waypoints[i];
            if (!t) continue; // drop null
            newList.Add(t);
            newOwners.Add(ownerIds[i]); // keep ownership for surviving slots
        }

        Waypoints = newList;
        ownerIds = newOwners.ToArray();
    }
}
