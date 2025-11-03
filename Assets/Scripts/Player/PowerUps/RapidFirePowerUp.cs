using UnityEngine;

public class RapidFirePowerUp : PowerUp
{
    private PlayerAttack playerAttack;

    protected override void OnActivate()
    {
        playerAttack = player.GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.EnableRapidFire();
        }
    }

    protected override void OnDeactivate()
    {
        Debug.Log("RapidFirePowerUp deactivated.");
        if (playerAttack != null)
        {
            playerAttack.DisableRapidFire();
        }
    }
}