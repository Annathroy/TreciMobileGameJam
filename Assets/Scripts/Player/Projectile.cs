using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour, IDamageDealer, IOnHitTarget
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Camera cam;            // leave null => Camera.main
    [SerializeField] private float yOffset = -2f;   // spawn height offset

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private bool destroyOnHit = true;

    [Header("Optional self-handling (leave OFF if EnemyHealth handles triggers)")]
    [SerializeField] private bool applyDamageOnTrigger = false;

    [Header("FX")]
    [SerializeField] private GameObject hitVFX;
    [SerializeField] private AudioClip hitSFX;
    [SerializeField] private float hitSFXVolume = 1f;

    public int Damage => damage;
    public bool DestroyOnHit => destroyOnHit;

    private float timeAlive;
    private Transform tf;
    private bool _consumed;
    private Vector3 moveDir;
    private float fixedY;

    void Awake()
    {
        tf = transform;
        if (!cam) cam = Camera.main;
    }

    void OnEnable()
    {
        timeAlive = 0f;
        _consumed = false;

        // Force exact world rotation from your JSON
        tf.rotation = new Quaternion(-0.5f, -0.5f, -0.5f, -0.5f);

        // Set absolute Y offset once
        fixedY = tf.position.y + yOffset;
        tf.position = new Vector3(tf.position.x, fixedY, tf.position.z);

        // Direction: toward top of screen (camera up, flattened to XZ)
        Vector3 screenUp = cam ? cam.transform.up : Vector3.forward;
        moveDir = Vector3.ProjectOnPlane(screenUp, Vector3.up);
        if (moveDir.sqrMagnitude < 1e-6f) moveDir = Vector3.forward;
        moveDir.Normalize();

        // --- IGNORE ANYTHING WITH PlayerDeath COMPONENT ---
        Collider myCol = GetComponent<Collider>();
        if (myCol)
        {
            PlayerDeath[] playerDeaths = FindObjectsOfType<PlayerDeath>(includeInactive: false);
            foreach (var pd in playerDeaths)
            {
                Collider otherCol = pd.GetComponent<Collider>();
                if (otherCol)
                    Physics.IgnoreCollision(myCol, otherCol, true);
            }
        }
    }

    void Update()
    {
        // Move in world-space, lock Y
        Vector3 pos = tf.position + moveDir * speed * Time.deltaTime;
        pos.y = fixedY;
        tf.position = pos;

        timeAlive += Time.deltaTime;
        if (timeAlive >= lifetime)
            Despawn();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_consumed || !applyDamageOnTrigger) return;

        // Skip any object that has PlayerDeath component (extra safeguard)
        if (other.GetComponent<PlayerDeath>() || other.GetComponentInParent<PlayerDeath>())
            return;

        var kh = other.GetComponent<KrakenHealth>() ?? other.GetComponentInParent<KrakenHealth>();
        if (kh != null)
        {
            Vector3 hitPoint = other.ClosestPoint(tf.position);
            if (kh.TakeDamage(damage, hitPoint, tf.position))
                Consume(hitPoint);
            return;
        }

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

        if (destroyOnHit)
            Despawn();
        Debug.Log("Hit");
    }

    public void ForceDespawn() => Despawn();
    void Despawn() => gameObject.SetActive(false);

    // ---- Interfaces ----
    public int GetDamage() => damage;
    public void OnHit(GameObject target)
    {
        if (_consumed) return;
        _consumed = true;
        if (destroyOnHit) Despawn();
        Debug.Log("hit");
    }
    private void OnDisable()
    {
        Debug.Log("Hit");
    }
    private void OnDestroy()
    {
        Debug.Log("hit destroyed");
    }
}
