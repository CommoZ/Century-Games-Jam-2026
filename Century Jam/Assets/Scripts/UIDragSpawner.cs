using UnityEngine;
using UnityEngine.EventSystems;
using static DraggablePipe;

public class UIDragSpawner : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private GameObject worldPrefab; // your pipe prefab
    private GameObject spawnedObject;

    [SerializeField] public PipeType pipeType;
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Spawn pipe when drag starts
        spawnedObject = Instantiate(worldPrefab);
        DraggablePipe pipe = spawnedObject.GetComponent<DraggablePipe>();
        pipe.pipeType = pipeType; // Set type as needed
        UpdateWorldPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (spawnedObject != null)
        {
            UpdateWorldPosition(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (spawnedObject != null)
        {
            UpdateWorldPosition(eventData);

            // Optional: snap / validate placement here
        }
    }

    private void UpdateWorldPosition(PointerEventData eventData)
    {
        Vector3 screenPos = eventData.position;

        // Convert screen into world
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;

        spawnedObject.transform.position = worldPos;
    }
}