using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpManager : MonoBehaviour
{
    // one active powerup per slot
    private readonly Dictionary<PowerUpSlot, PowerUp> activeBySlot = new();

    /// <summary>
    /// Called by a PowerUp BEFORE it applies stacks.
    /// Ensures only one active powerup per slot (last picked wins).
    /// </summary>
    public void RequestActivation(PowerUp requester)
    {
        var slot = requester.Slot;
        if (activeBySlot.TryGetValue(slot, out var current) && current != null && current != requester)
        {
            // clear the existing one completely
            current.ForceClearAllStacks();
        }

        activeBySlot[slot] = requester;
    }

    /// <summary>
    /// If a powerup depletes to 0 stacks, unassign it for cleanliness.
    /// </summary>
    public void NotifyDepleted(PowerUp power)
    {
        if (activeBySlot.TryGetValue(power.Slot, out var current) && current == power)
            activeBySlot.Remove(power.Slot);
    }
}

