using UnityEngine;

public class PlayerIntro : MonoBehaviour
{
    [SerializeField] private float targetScreenRatio = 0.25f; // 1/4 of screen height
    [SerializeField] private float moveSpeed = 5f;
    
    private float targetZ;
    private bool isMoving = true;
    private Camera mainCamera;

    // Public property to check if player can attack
    public bool CanAttack => !isMoving;

    void Start()
    {
        mainCamera = Camera.main;
        
        // Position player below screen view
        Vector3 startPos = transform.position;
        Vector3 belowScreen = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, -1f));
        transform.position = new Vector3(startPos.x, startPos.y, belowScreen.z - 5f); // Start below screen

        // Calculate target Z position
        Vector3 targetViewportPos = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, targetScreenRatio, 10f));
        targetZ = targetViewportPos.z;
    }

    void Update()
    {
        if (!isMoving) return;

        Vector3 currentPos = transform.position;
        float newZ = Mathf.MoveTowards(currentPos.z, targetZ, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(currentPos.x, currentPos.y, newZ);

        // Stop moving when we reach the target
        if (Mathf.Approximately(newZ, targetZ))
        {
            isMoving = false;
        }
    }
}
