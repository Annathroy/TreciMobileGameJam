using UnityEngine;

public class EightWayPowerUp : PowerUp
{
    private PlayerAttack atk;

    private void EnsureRef()
    {
        if (atk) return;
        if (!player) return;
        atk = player.GetComponent<PlayerAttack>() ?? player.GetComponentInChildren<PlayerAttack>(true);
        if (!atk) Debug.LogError("[EightWayPowerUp] PlayerAttack not found on player.");
    }

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;

        // Last-picked-wins for weapon slot: turn others off
        atk.SetBombShooting(false);
        atk.SetScatterShot(false);
        // if you still have a “double” flag, also disable it:
        // atk.SetDoubleShooting(false);

        atk.SetEightWay(true);

        // Optional: if you want lines to also apply horizontally to eight-way:
        // atk.SetBulletLines(Mathf.Clamp(stackCount, 1, 10));
    }

    protected override void OnStack(int newCount)
    {
        EnsureRef(); if (!atk) return;
        // Optional: let stacks increase barrels horizontally:
        // atk.SetBulletLines(Mathf.Clamp(newCount, 1, 10));
    }

    protected override void OnUnstack(int newCount)
    {
        EnsureRef(); if (!atk) return;

        if (newCount > 0)
        {
            // Optional: keep barrels in sync with stacks
            // atk.SetBulletLines(Mathf.Clamp(newCount, 1, 10));
        }
        else
        {
            atk.SetEightWay(false);
            // Reset to single barrel if you used horizontal stacking:
            // atk.SetBulletLines(1);
        }
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetEightWay(false);
        // atk.SetBulletLines(1);
    }
}

