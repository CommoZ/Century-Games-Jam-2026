using UnityEngine;
using UnityEngine.InputSystem;

public class DraggablePipe : MonoBehaviour
{
    private bool isDragging;
    private bool isRotating;
    private bool isSelected;

    private Vector3 offset;

    [SerializeField] private GameObject rotateKnob;
    [SerializeField] private GameObject cancelKnob;
    private Collider2D rotateKnobCollider;
    private Collider2D cancelKnobCollider;
    private float startAngle;
    private float startRotation;

    void Start()
    {
        if (rotateKnob != null)
        {
            rotateKnobCollider = rotateKnob.GetComponent<Collider2D>();
            rotateKnob.SetActive(false);
        }
        if (cancelKnob != null)
        {
            cancelKnobCollider = cancelKnob.GetComponent<Collider2D>();
            cancelKnob.SetActive(false);
        }
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector3 mouseWorld = GetMouseWorldPos();

        // Mouse Down
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Collider2D hit = Physics2D.OverlapPoint(mouseWorld);

            // Rotate Mode (only if selected, check by collider reference)
            if (isSelected && rotateKnobCollider != null && hit == rotateKnobCollider)
            {
                isRotating = true;

                Vector2 dir = mouseWorld - transform.position;
                startAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                startRotation = transform.eulerAngles.z;

                return;
            }

            if (isSelected && cancelKnobCollider != null && hit == cancelKnobCollider)
            {
                // Reset rotation and position
                transform.rotation = Quaternion.identity;
                transform.position = Vector3.zero;
                return;
            }

            // Select and drag this object
            if (hit != null && hit.transform == transform)
            {
                isSelected = true;

                if (rotateKnob != null)
                    rotateKnob.SetActive(true);

                if (cancelKnob != null)
                    cancelKnob.SetActive(true);

                offset = transform.position - mouseWorld;
                isDragging = true;

                return;
            }

            // Clicked elsewhere deselect
            isSelected = false;

            if (rotateKnob != null)
                rotateKnob.SetActive(false);
            if (cancelKnob != null)
                cancelKnob.SetActive(false);
        }

        // Rotating
        if (Mouse.current.leftButton.isPressed && isRotating)
        {
            Vector2 dir = mouseWorld - transform.position;
            float currentAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            float deltaAngle = currentAngle - startAngle;

            transform.rotation = Quaternion.Euler(0, 0, startRotation + deltaAngle);
        }

        // Dragging
        if (Mouse.current.leftButton.isPressed && isDragging)
        {
            Vector3 newPos = mouseWorld + offset;
            transform.position = new Vector3(newPos.x, newPos.y, 0);
        }

        // Mouse Up
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            isRotating = false;
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Mouse.current.position.ReadValue();
        mousePoint.z = 10;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}