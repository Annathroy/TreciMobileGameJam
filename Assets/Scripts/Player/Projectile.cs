using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("FX")]
    [SerializeField] private GameObject hitVFX;
    [SerializeField] private AudioClip hitSFX;
    [SerializeField] private float hitSFXVolume = 1f;

    public int Damage => damage;
    public bool DestroyOnHit => destroyOnHit;

    float timeAlive;
    Transform tf;

    void Awake() { tf = transform; }

    void OnEnable() { timeAlive = 0f; }

    void Update()
    {
        tf.position += tf.forward * speed * Time.deltaTime;

        timeAlive += Time.deltaTime;
        if (timeAlive >= lifetime) Despawn();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(enemyTag)) return;

        var health = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
        if (health != null)
        {
            health.ApplyDamage(damage);

            if (hitVFX)
            {
                var vfx = Instantiate(hitVFX, tf.position, Quaternion.identity);
                Destroy(vfx, 2f);
            }
            if (hitSFX)
                AudioSource.PlayClipAtPoint(hitSFX, tf.position, hitSFXVolume);

            if (destroyOnHit) Despawn();
        }
    }

    public void ForceDespawn() => Despawn();

    void Despawn()
    {
        gameObject.SetActive(false); // pool-friendly
    }
}
