using UnityEngine;
using UnityEngine.InputSystem;

public class DraggablePipe : MonoBehaviour
{
    private bool isDragging;
    private Vector3 offset;
    private Vector2Int lastGridPos;

    void Update()
    {
        if (Mouse.current == null) return;

        Vector3 mouseWorld = GetMouseWorldPos();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == Physics2D.OverlapPoint(mouseWorld))
            {
                offset = transform.position - mouseWorld;
                isDragging = true;

                Pipe pipe = GetComponent<Pipe>();
                lastGridPos = pipe.gridPos;
                GridManager.instance.Remove(lastGridPos);
            }
        }

        if (Mouse.current.leftButton.isPressed && isDragging)
        {
            transform.position = mouseWorld + offset;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;

            Vector2Int gridPos = GridManager.instance.WorldToGrid(transform.position);

            if (!GridManager.instance.IsOccupied(gridPos))
            {
                GridManager.instance.Place(GetComponent<Pipe>(), gridPos);
            }
            else
            {
                // Return back
                GridManager.instance.Place(GetComponent<Pipe>(), lastGridPos);
            }
        }
    }

    Vector3 GetMouseWorldPos()
    {
        Vector3 m = Mouse.current.position.ReadValue();
        m.z = 10;
        return Camera.main.ScreenToWorldPoint(m);
    }
}