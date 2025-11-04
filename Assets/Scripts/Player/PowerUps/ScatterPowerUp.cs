using UnityEngine;

public class ScatterPowerUp : PowerUp
{
    [SerializeField] private float perPickupMultiplier = 0.5f; // halves fire interval (doubles rate) per pickup

    private PlayerAttack atk;

    private void EnsureRef()
    {
        if (atk) return;
        if (!player) return;
        atk = player.GetComponent<PlayerAttack>() ?? player.GetComponentInChildren<PlayerAttack>(true);
        if (!atk) Debug.LogError("[ScatterPowerUp] PlayerAttack not found.");
    }

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;

        atk.SetScatterShot(true);
        ApplyFireRate();
    }

    protected override void OnStack(int newCount)
    {
        EnsureRef(); if (!atk) return;
        ApplyFireRate();
    }

    protected override void OnUnstack(int newCount)
    {
        EnsureRef(); if (!atk) return;

        if (newCount > 0)
            ApplyFireRate();
        else
        {
            atk.SetFireRateMultiplier(1f);
            atk.SetScatterShot(false);
        }
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;

        atk.SetFireRateMultiplier(1f);
        atk.SetScatterShot(false);
    }

    private void ApplyFireRate()
    {
        // total multiplier = perPickupMultiplier ^ stacks
        float mult = Mathf.Pow(perPickupMultiplier, Mathf.Max(1, stackCount));
        atk.SetFireRateMultiplier(mult);
        Debug.Log($"[ScatterFire] stacks={stackCount}, fire rate ×{1f / mult:0.##}");
    }
}

