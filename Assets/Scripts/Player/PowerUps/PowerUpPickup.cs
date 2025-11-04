using UnityEngine;

public enum PowerUpKind { DoubleShot, RapidFire, Scatter, Bomb, EightWay, Invulnerability }


[RequireComponent(typeof(Collider))]
public class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private PowerUpKind kind;
    [SerializeField] private float overrideDuration = -1f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.transform.root.gameObject;

        PowerUp p = kind switch
        {
            PowerUpKind.DoubleShot => (PowerUp)(player.GetComponent<DoubleShootingPowerUp>() ?? player.AddComponent<DoubleShootingPowerUp>()),
            PowerUpKind.RapidFire => (PowerUp)(player.GetComponent<RapidFirePowerUp>() ?? player.AddComponent<RapidFirePowerUp>()),
            PowerUpKind.Scatter => (PowerUp)(player.GetComponent<ScatterPowerUp>() ?? player.AddComponent<ScatterPowerUp>()),
            PowerUpKind.Bomb => (PowerUp)(player.GetComponent<BombPowerUp>() ?? player.AddComponent<BombPowerUp>()),
            PowerUpKind.EightWay => (PowerUp)(player.GetComponent<EightWayPowerUp>() ?? player.AddComponent<EightWayPowerUp>()),
            PowerUpKind.Invulnerability => (PowerUp)(player.GetComponent<InvulnerabilityPowerUp>() ?? player.AddComponent<InvulnerabilityPowerUp>()),
            _ => null
        };


        if (p == null) return;

        if (overrideDuration > 0f)
            p.SetDuration(overrideDuration);

        p.Activate(player);

        Destroy(gameObject);
    }
}


