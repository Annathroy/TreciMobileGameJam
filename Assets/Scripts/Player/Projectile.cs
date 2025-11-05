using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour, IDamageDealer, IOnHitTarget
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private bool destroyOnHit = true;

    [Header("Optional self-handling (leave OFF if EnemyHealth handles triggers)")]
    [SerializeField] private bool applyDamageOnTrigger = false;

    [Header("FX")]
    [SerializeField] private GameObject hitVFX;
    [SerializeField] private AudioClip hitSFX;
    [SerializeField] private float hitSFXVolume = 1f;

    // Backward-compat properties required by EnemyHealth
    public int Damage => damage;
    public bool DestroyOnHit => destroyOnHit;

    float timeAlive;
    Transform tf;
    bool _consumed; // guard vs double-processing

    void Awake() { tf = transform; }
    void OnEnable() { timeAlive = 0f; _consumed = false; }

    void Update()
    {
        tf.position += tf.forward * speed * Time.deltaTime;
        timeAlive += Time.deltaTime;
        if (timeAlive >= lifetime) Despawn();
    }

    // Only used if you enable "applyDamageOnTrigger"
    void OnTriggerEnter(Collider other)
    {
        if (_consumed || !applyDamageOnTrigger) return;

        // Prefer KrakenHealth (boss) — common grunts with EnemyHealth will self-handle.
        var kh = other.GetComponent<KrakenHealth>() ?? other.GetComponentInParent<KrakenHealth>();
        if (kh != null)
        {
            Vector3 hitPoint = other.ClosestPoint(tf.position);
            bool did = kh.TakeDamage(damage, hitPoint, tf.position);
            if (did) Consume(hitPoint);
            return;
        }

        // Fallback: if no KrakenHealth present, try EnemyHealth *only if* they didn't already handle it.
        var eh = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            eh.ApplyDamage(damage);
            Consume(other.ClosestPoint(tf.position));
        }
    }

    void Consume(Vector3 at)
    {
        if (_consumed) return;
        _consumed = true;

        if (hitVFX)
        {
            var vfx = Instantiate(hitVFX, at, Quaternion.identity);
            Destroy(vfx, 2f);
        }
        if (hitSFX)
            AudioSource.PlayClipAtPoint(hitSFX, at, hitSFXVolume);

        if (destroyOnHit) Despawn();
    }

    public void ForceDespawn() => Despawn();
    void Despawn() { gameObject.SetActive(false); }

    // ---- Interfaces ----
    public int GetDamage() => damage;              // IDamageDealer (KrakenHealth reads this)
    public void OnHit(GameObject target)           // IOnHitTarget (KrakenHealth can notify us)
    {
        if (_consumed) return;
        _consumed = true;
        if (destroyOnHit) Despawn();
    }
}
