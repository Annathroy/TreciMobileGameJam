using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private Transform body;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Camera cam; // assign Main Camera

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Bank")]
    [SerializeField] private float maxBankDeg = 35f;
    [SerializeField] private float bankPerUnitSpeed = 3f;
    [SerializeField] private float bankLerp = 10f;

    float lastPosX, currentBankDeg;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!body) body = transform;
        if (!cam) cam = Camera.main;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        lastPosX = rb.position.x;
    }

    void FixedUpdate()
    {
        Vector2 in2 = joystick ? joystick.InputVector : Vector2.zero;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 moveDir = (right * in2.x + forward * in2.y).normalized;
        rb.linearVelocity = moveDir * (moveSpeed * in2.magnitude); // preserves analog magnitude
    }


    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float xSpeed = (rb.position.x - lastPosX) / dt; lastPosX = rb.position.x;

        float targetBank = Mathf.Clamp(-xSpeed * bankPerUnitSpeed, -maxBankDeg, maxBankDeg);
        float t = 1f - Mathf.Exp(-bankLerp * dt);
        currentBankDeg = Mathf.Lerp(currentBankDeg, targetBank, t);

        var e = body.localEulerAngles;
        body.localRotation = Quaternion.Euler(e.x, e.y, currentBankDeg);
    }
}
