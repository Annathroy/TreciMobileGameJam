using UnityEngine;
using System.Collections;

public abstract class PowerUp : MonoBehaviour
{
    // Duration for which the power-up is active
    [SerializeField]
    protected float duration = 5f;

    // Maximum number of stacks allowed
    private const int MaxStacks = 4;

    // Current stack count
    private int stackCount = 0;

    // Reference to the player object
    protected GameObject player;

    // Coroutine for handling deactivation
    private Coroutine deactivateCoroutine;

    // Called when the power-up is picked up
    public void Activate(GameObject player)
    {
        this.player = player;

        if (stackCount < MaxStacks)
        {
            stackCount++;
            if (stackCount == 1)
            {
                Debug.Log($"Activating power-up: {gameObject.name}");
                OnActivate();
            }
            else
            {
                Debug.Log($"Stacking power-up: {gameObject.name}. Current stack count: {stackCount}");
                OnStack();
            }
        }

        // Reset the duration timer for the latest stack
        if (deactivateCoroutine != null)
        {
            StopCoroutine(deactivateCoroutine);
        }
        deactivateCoroutine = StartCoroutine(DeactivateAfterDuration());
    }

    private IEnumerator DeactivateAfterDuration()
    {
        Debug.Log($"Power-up {gameObject.name} will deactivate in {duration} seconds.");
        yield return new WaitForSeconds(duration);
        Deactivate();
    }

    // Called when the power-up effect ends
    public void Deactivate()
    {
        Debug.Log($"Deactivate called for {gameObject.name}. Stack count: {stackCount}");
        if (stackCount > 0)
        {
            stackCount--;
            Debug.Log($"Deactivating stack. Remaining stacks: {stackCount}");
            if (stackCount == 0)
            {
                Debug.Log($"Fully deactivating power-up: {gameObject.name}");
                OnDeactivate();

                if (deactivateCoroutine != null)
                {
                    StopCoroutine(deactivateCoroutine);
                }

                Destroy(gameObject); // Destroy the power-up object after use
            }
            else
            {
                OnUnstack();
            }
        }
        else
        {
            Debug.LogWarning($"Deactivate called, but stackCount is already 0 for {gameObject.name}.");
        }
    }

    // Abstract method for activating the power-up effect
    protected abstract void OnActivate();

    // Abstract method for deactivating the power-up effect
    protected abstract void OnDeactivate();

    // Virtual method for handling additional stack activation
    protected virtual void OnStack() { }

    // Virtual method for handling stack reduction
    protected virtual void OnUnstack() { }
}
