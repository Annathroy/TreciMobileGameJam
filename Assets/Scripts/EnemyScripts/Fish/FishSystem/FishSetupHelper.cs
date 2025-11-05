using UnityEngine;

public class FishSetupHelper : MonoBehaviour
{
    [Header("Auto add PooledObject if missing")]
    public bool ensurePooledObject = true;

    private void Reset()
    {
        if (ensurePooledObject && GetComponent<PooledObject>() == null)
            gameObject.AddComponent<PooledObject>();
    }
}
