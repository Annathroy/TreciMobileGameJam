using UnityEngine;

public class DoubleShootingPowerUp : PowerUp
{
    private PlayerAttack atk;

    private void EnsureRef()
    {
        if (atk) return;
        if (!player) return;
        atk = player.GetComponent<PlayerAttack>() ?? player.GetComponentInChildren<PlayerAttack>(true);
    }

    // How lines scale with stacks:
    // base 1 line + one per stack -> 2,3,4...
    private int LinesForStacks(int stacks) => Mathf.Clamp(1 + stacks, 1, 10);

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetBulletLines(LinesForStacks(stackCount));  // first pickup -> 2 lines
    }

    protected override void OnStack(int newCount)
    {
        EnsureRef(); if (!atk) return;
        atk.SetBulletLines(LinesForStacks(newCount));    // +1 line each stack
    }

    protected override void OnUnstack(int newCount)
    {
        EnsureRef(); if (!atk) return;
        if (newCount > 0) atk.SetBulletLines(LinesForStacks(newCount));
        else atk.SetBulletLines(1); // back to single barrel
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetBulletLines(1);
    }
}





