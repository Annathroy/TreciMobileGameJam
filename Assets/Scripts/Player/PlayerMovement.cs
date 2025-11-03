using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f; // Increased for more responsiveness
    [SerializeField] private float maxDelta = 1f; // Maximum movement delta to prevent excessive speed

    private Rigidbody rb; // Reference to the Rigidbody component
    private Camera mainCamera; // Reference to the main camera
    private bool isTouching = false; // Tracks if the screen is being touched
    private Vector2 previousTouchPosition; // Stores the previous touch position

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooth movement
        mainCamera = Camera.main;
    }

    // This method is invoked by the Input System when the "Touch" action is triggered
    public void OnTouch(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isTouching = true;
            previousTouchPosition = Pointer.current.position.ReadValue(); // Initialize the previous touch position
        }
        else if (context.canceled)
        {
            isTouching = false;
        }
    }

    // This method is invoked by the Input System to update the touch position
    public void OnTouchPosition(InputAction.CallbackContext context)
    {
        if (isTouching && context.performed)
        {
            Vector2 currentTouchPosition = context.ReadValue<Vector2>();
            Vector2 delta = currentTouchPosition - previousTouchPosition; // Calculate the drag delta

            // Convert the delta to world space and apply it to the player's position
            Vector3 worldDelta = mainCamera.ScreenToWorldPoint(new Vector3(delta.x, delta.y, mainCamera.WorldToScreenPoint(transform.position).z)) 
                                 - mainCamera.ScreenToWorldPoint(new Vector3(0, 0, mainCamera.WorldToScreenPoint(transform.position).z));

            // Clamp the world delta to prevent excessive movement
            worldDelta = Vector3.ClampMagnitude(worldDelta, maxDelta);

            // Apply movement directly
            rb.MovePosition(transform.position + worldDelta * moveSpeed);

            previousTouchPosition = currentTouchPosition; // Update the previous touch position
        }
    }
}
