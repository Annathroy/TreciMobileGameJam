// RapidFirePowerUp.cs
using UnityEngine;

public class RapidFirePowerUp : PowerUp
{
    [Header("Rapid Fire")]
    [SerializeField] private float perStackMultiplier = 0.7f; // stronger effect: 30% faster per stack
    [SerializeField] private PowerUpSlot slotOverride = PowerUpSlot.FireRate; // <<--- KEY

    private PlayerAttack atk;

    private void Awake()
    {
        // force our slot to FireRate (overrides base default)
        var slotField = typeof(PowerUp).GetField("slot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (slotField != null) slotField.SetValue(this, slotOverride);
    }

    private void EnsureRef()
    {
        if (atk) return;
        if (!player) return;
        atk = player.GetComponent<PlayerAttack>() ?? player.GetComponentInChildren<PlayerAttack>(true);
        if (!atk) Debug.LogError("[RapidFire] PlayerAttack not found.");
    }

    private void Apply()
    {
        EnsureRef(); if (!atk) return;

        // eff = perStack^stacks ; stacks=0 -> 1.0
        float mult = Mathf.Pow(perStackMultiplier, Mathf.Max(0, stackCount));
        atk.SetFireRateMultiplier(mult);

        Debug.Log($"[RapidFire] stacks={stackCount}, mult={mult:0.###}");
    }

    protected override void OnActivate() { Apply(); }
    protected override void OnStack(int newCount) { Apply(); }
    protected override void OnUnstack(int newCount)
    {
        if (!atk) EnsureRef();
        if (!atk) return;

        if (newCount > 0) Apply();
        else atk.SetFireRateMultiplier(1f); // back to base
    }
    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetFireRateMultiplier(1f);
    }
}



