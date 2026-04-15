using UnityEngine;
using UnityEngine.InputSystem;

public class DraggablePipe : MonoBehaviour
{
	private bool isDragging;
	private Vector3 offset;

	void Update()
	{
		if (Mouse.current == null) return;

		Vector3 mouseWorld = GetMouseWorldPos();

		// Mouse Down
		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			//if (GameManager.instance.isFlowing) return;

			Collider2D hit = Physics2D.OverlapPoint(mouseWorld);

			if (hit != null && hit.transform == transform)
			{
				offset = transform.position - mouseWorld;
				isDragging = true;
			}
		}

		// Holding
		if (Mouse.current.leftButton.isPressed && isDragging)
		{
			Vector3 newPos = mouseWorld + offset;
			transform.position = new Vector3(newPos.x, newPos.y, 0);
		}

		// Mouse Up
		if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
		{
			isDragging = false;

			//float cellSize = 1f;

			//float x = Mathf.Floor(transform.position.x / cellSize) * cellSize + cellSize / 2f;
			//float y = Mathf.Floor(transform.position.y / cellSize) * cellSize + cellSize / 2f;

			//transform.position = new Vector3(x, y, 0);
		}
	}

	private Vector3 GetMouseWorldPos()
	{
		Vector3 mousePoint = Mouse.current.position.ReadValue();
		mousePoint.z = 10;
		return Camera.main.ScreenToWorldPoint(mousePoint);
	}
}