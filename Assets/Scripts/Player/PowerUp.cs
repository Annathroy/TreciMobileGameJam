using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PowerUp : MonoBehaviour
{
    [Header("Timing & Stacking")]
    [SerializeField] protected float duration = 5f;
    [SerializeField] protected int maxStacks = 4;

    // If true (default): any pickup refreshes duration to now+duration.
    [SerializeField] private bool refreshOnPickup = true;

    // If false (default): one global timer; all stacks expire together.
    // If true: one timer per stack; on pickup we refresh ALL stack timers.
    [SerializeField] private bool independentStackTimers = false;

    [Header("Slot")]
    [SerializeField] private PowerUpSlot slot = PowerUpSlot.Weapon; // last-picked-wins per slot

    protected int stackCount = 0;
    protected GameObject player;

    private PowerUpManager manager;

    // single-timer mode
    private Coroutine globalTimer;

    // per-stack mode
    private readonly List<Coroutine> stackTimers = new List<Coroutine>();

    public PowerUpSlot Slot => slot;

    public void Activate(GameObject playerGO)
    {
        player = playerGO;
        manager = manager ?? player.GetComponent<PowerUpManager>() ?? player.AddComponent<PowerUpManager>();

        // last-picked-wins per slot
        manager.RequestActivation(this);

        // apply/add stack
        if (stackCount == 0) { stackCount = 1; OnActivate(); }
        else if (stackCount < maxStacks) { stackCount++; OnStack(stackCount); }
        else { OnMaxStackRefreshed(stackCount); }

        // handle timers
        if (!independentStackTimers)
        {
            // --- SINGLE TIMER: all stacks share one expiry, refresh on pickup ---
            if (globalTimer != null) StopCoroutine(globalTimer);
            globalTimer = StartCoroutine(GlobalExpireAfter(duration));
        }
        else
        {
            // --- INDEPENDENT TIMERS: refresh ALL timers on pickup ---
            RefreshAllStackTimers();
        }
    }

    // ===== Single global timer =====
    private IEnumerator GlobalExpireAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        // expire everything
        stackCount = 0;
        OnDeactivate();
        manager?.NotifyDepleted(this);
        globalTimer = null;
    }

    // ===== Per-stack timers (all refreshed on pickup) =====
    private void RefreshAllStackTimers()
    {
        // kill existing timers
        for (int i = 0; i < stackTimers.Count; i++)
        {
            if (stackTimers[i] != null) StopCoroutine(stackTimers[i]);
        }
        stackTimers.Clear();

        // schedule 'stackCount' timers that all expire after full duration
        for (int i = 0; i < stackCount; i++)
        {
            stackTimers.Add(StartCoroutine(ExpireOneStack(duration)));
        }
    }

    private IEnumerator ExpireOneStack(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        stackCount--;

        if (stackCount <= 0)
        {
            // stop any survivors (defensive)
            for (int i = 0; i < stackTimers.Count; i++)
                if (stackTimers[i] != null) StopCoroutine(stackTimers[i]);
            stackTimers.Clear();

            OnDeactivate();
            manager?.NotifyDepleted(this);
        }
        else
        {
            OnUnstack(stackCount);
        }
    }

    // Manager calls this when another powerup in same slot is picked
    public void ForceClearAllStacks()
    {
        if (globalTimer != null) { StopCoroutine(globalTimer); globalTimer = null; }

        for (int i = 0; i < stackTimers.Count; i++)
            if (stackTimers[i] != null) StopCoroutine(stackTimers[i]);
        stackTimers.Clear();

        if (stackCount > 0)
        {
            stackCount = 0;
            OnDeactivate();
        }
    }

    public void SetDuration(float d) => duration = d;

    // Optional helpers to flip behaviors per type at runtime
    public void SetRefreshOnPickup(bool refresh) => refreshOnPickup = refresh;
    public void SetIndependentStackTimers(bool independent) => independentStackTimers = independent;

    protected abstract void OnActivate();
    protected abstract void OnDeactivate();
    protected virtual void OnStack(int newCount) { }
    protected virtual void OnUnstack(int newCount) { }
    protected virtual void OnMaxStackRefreshed(int currentCount) { }
}

