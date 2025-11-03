using UnityEngine;

public class BombPowerUp : PowerUp
{
    private PlayerAttack playerAttack;

    protected override void OnActivate()
    {
        playerAttack = player.GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.EnableBombShooting();
        }
    }

    protected override void OnDeactivate()
    {
        Debug.Log("BombPowerUp deactivated.");
        if (playerAttack != null)
        {
            playerAttack.DisableBombShooting();
        }
    }
}