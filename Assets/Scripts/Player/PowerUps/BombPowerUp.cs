using UnityEngine;

public class BombPowerUp : PowerUp
{
    private PlayerAttack atk;
    private void EnsureRef() { if (!atk && player) atk = player.GetComponent<PlayerAttack>(); if (!atk && player) atk = player.GetComponentInChildren<PlayerAttack>(true); }

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;
        
        // Enable bomb shooting and disable other modes
        atk.SetBombShooting(true);
        atk.SetScatterShot(false);
        atk.SetEightWay(false);  // Also disable eight-way if active
        atk.SetBulletLines(1);
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        
        // Revert to normal projectiles
        atk.SetBombShooting(false);
        atk.SetBulletLines(1);
        // Note: We don't re-enable other modes here as they should be managed by their own power-ups
    }
}
