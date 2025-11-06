using UnityEngine;

public class BombPowerUp : PowerUp
{
    [Header("Bomb Settings")]
    [SerializeField] private float bombFireRateMultiplier = 2.0f; // Higher value = slower shooting (2.0 = half speed)
    
    private PlayerAttack atk;
    private float originalFireRateMultiplier = 1.0f; // Store original multiplier to restore later
    
    private void EnsureRef() { if (!atk && player) atk = player.GetComponent<PlayerAttack>(); if (!atk && player) atk = player.GetComponentInChildren<PlayerAttack>(true); }

    protected override void OnActivate()
    {
        EnsureRef(); if (!atk) return;
        
        // Store the original fire rate multiplier (assuming it's 1.0 by default)
        // You might need to add a getter in PlayerAttack if you want to preserve existing multipliers
        
        // Enable bomb shooting and disable other modes
        atk.SetBombShooting(true);
        atk.SetScatterShot(false);
        atk.SetEightWay(false);  // Also disable eight-way if active
        atk.SetBulletLines(1);
        
        // Slow down the fire rate for bombs
        atk.SetFireRateMultiplier(bombFireRateMultiplier);
    }

    protected override void OnDeactivate()
    {
        EnsureRef(); if (!atk) return;
        
        // Revert to normal projectiles
        atk.SetBombShooting(false);
        atk.SetBulletLines(1);
        
        // Restore original fire rate
        atk.SetFireRateMultiplier(originalFireRateMultiplier);
        
        // Note: We don't re-enable other modes here as they should be managed by their own power-ups
    }
}
