using UnityEngine;

public class BombPowerUp : PowerUp
{
    private PlayerAttack atk;
    private void EnsureRef() { if (!atk && player) atk = player.GetComponent<PlayerAttack>(); if (!atk && player) atk = player.GetComponentInChildren<PlayerAttack>(true); }

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetBombShooting(true);
        atk.SetScatterShot(false);
        atk.SetBulletLines(1);
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        atk.SetBombShooting(false);
        atk.SetBulletLines(1);
    }
}
