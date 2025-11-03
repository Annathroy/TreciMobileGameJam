using UnityEngine;

public class DoubleShootingPowerUp : PowerUp
{
    private PlayerAttack playerAttack;

    protected override void OnActivate()
    {
        playerAttack = player.GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.EnableDoubleShooting();
        }
    }

    protected override void OnDeactivate()
    {
        Debug.Log("DoubleShootingPowerUp deactivated.");
        if (playerAttack != null)
        {
            playerAttack.DisableDoubleShooting();
        }
    }
}