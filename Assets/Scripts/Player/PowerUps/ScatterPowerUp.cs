using UnityEngine;

public class ScatterPowerUp : PowerUp
{
    private PlayerAttack playerAttack;

    protected override void OnActivate()
    {
        playerAttack = player.GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.EnableScatterShot();
        }
    }

    protected override void OnDeactivate()
    {
        Debug.Log("ScatterPowerUp deactivated.");
        if (playerAttack != null)
        {
            playerAttack.DisableScatterShot();
        }
    }
}