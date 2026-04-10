using UnityEngine;

public class DraggablePipe : MonoBehaviour
{
    private bool isDragging;
    private Vector3 offset;

    void OnMouseDown()
    {
        // Only allow dragging if the game isn't "pouring"
        if (GameManager.instance.isFlowing) return;

        offset = transform.position - GetMouseWorldPos();
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            Debug.Log("Dragging Pipe...");
            // 1. Get the raw mouse position
            Vector3 newPos = GetMouseWorldPos() + offset;

            // 2. OPTION A: Smooth follow (Ball stays exactly under mouse)
            transform.position = new Vector3(newPos.x, newPos.y, 0);

            // 3. OPTION B: Stutter follow (Ball jumps from grid square to grid square)
            // transform.position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), 0);
        }
    }

    void OnMouseUp()
    {
        isDragging = false;

        // Final Snap: When you let go, it locks into the grid perfectly
        transform.position = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), 0);
    }
    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = 10; // Distance from camera
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}