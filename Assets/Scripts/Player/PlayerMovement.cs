using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb; // Reference to the Rigidbody component
    private Camera mainCamera; // Reference to the main camera
    private bool isTouchingPlayer = false; // Tracks if the finger is on the player
    private Vector3 touchWorldPosition; // Stores the world position of the touch

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
    }

    // This method is invoked by the Input System when the "Touch" action is triggered
    public void OnTouch(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            // Check if the touch started on the player
            Vector2 touchPosition = Pointer.current.position.ReadValue(); // Read the touch position
            Ray ray = mainCamera.ScreenPointToRay(touchPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    isTouchingPlayer = true;
                }
            }
        }
        else if (context.canceled)
        {
            // Stop following when the touch ends
            isTouchingPlayer = false;
        }
    }

    // This method is invoked by the Input System to update the touch position
    public void OnTouchPosition(InputAction.CallbackContext context)
    {
        if (isTouchingPlayer && context.performed)
        {
            Vector2 touchPosition = context.ReadValue<Vector2>();
            touchWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(touchPosition.x, touchPosition.y, mainCamera.WorldToScreenPoint(transform.position).z));
        }
    }

    private void FixedUpdate()
    {
        if (isTouchingPlayer)
        {
            FollowTouch();
        }
    }

    private void FollowTouch()
    {
        // Move the player towards the touch position
        Vector3 direction = (touchWorldPosition - transform.position).normalized;
        rb.MovePosition(transform.position + direction * moveSpeed * Time.fixedDeltaTime);
    }
}
