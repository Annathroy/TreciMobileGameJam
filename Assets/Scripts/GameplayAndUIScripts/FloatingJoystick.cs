using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Refs")]
    [SerializeField] private RectTransform joystickBase;
    [SerializeField] private RectTransform joystickHandle;

    [Header("Tuning")]
    [SerializeField] private float handleLimit = 140f; // px radius
    [SerializeField] private float deadZone = 0.05f;    // 0..1

    private RectTransform parentRect;
    private int activePointerId = -1;
    private Vector2 inputVector = Vector2.zero;

    public Vector2 InputVector => inputVector;

    void Awake()
    {
        parentRect = (RectTransform)joystickBase.parent;
        Show(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -1) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out localPos);

        joystickBase.anchoredPosition = localPos;
        joystickHandle.anchoredPosition = Vector2.zero;
        inputVector = Vector2.zero;
        activePointerId = eventData.pointerId;
        Show(true);

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBase, eventData.position, eventData.pressEventCamera, out localPoint);

        localPoint = Vector2.ClampMagnitude(localPoint, handleLimit);
        joystickHandle.anchoredPosition = localPoint;

        // non-linear scaling: small drags give finer control
        Vector2 v = localPoint / handleLimit;
        float mag = v.magnitude;
        if (mag < deadZone)
            inputVector = Vector2.zero;
        else
        {
            // curve factor (0.5–1 = slower near center)
            float curvedMag = Mathf.Pow((mag - deadZone) / (1f - deadZone), 0.6f);
            inputVector = v.normalized * curvedMag;
        }

    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        activePointerId = -1;
        inputVector = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;
        Show(false);
    }

    private void Show(bool state) => joystickBase.gameObject.SetActive(state);
}
