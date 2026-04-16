using UnityEngine;
using TMPro;

public class WaterCollector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Fluid simulations to check. Auto-found if left empty.")]
    [SerializeField] private SPHFluid2D[] fluids;

    [Header("Detection Zone")]
    [Tooltip("Size of the interior detection area where resting particles are counted.")]
    [SerializeField] private Vector2 detectionSize = new Vector2(0.6f, 0.6f);
    [Tooltip("Offset of the detection zone relative to this transform.")]
    [SerializeField] private Vector2 detectionOffset = Vector2.zero;

    [Header("Collection")]
    [Tooltip("How many particles needed to consider the collector full.")]
    [SerializeField] private int capacity = 50;

    [Header("Debug Counter")]
    [SerializeField] private bool showDebugCounter = false;
    [SerializeField] private float counterFontSize = 3f;
    [SerializeField] private Vector2 counterOffset = new Vector2(0f, 0.5f);
    [SerializeField] private Color counterColor = Color.white;
    [SerializeField] private Color counterFullColor = new Color(0.3f, 1f, 0.4f);

    private int currentCount;
    private bool isFull;
    private TextMeshPro counterText;

    public int CollectedCount => currentCount;
    public int Capacity => capacity;
    public bool IsFull => isFull;
    public float FillPercent => capacity > 0 ? (float)currentCount / capacity : 0f;

    private void Awake()
    {
        if (fluids == null || fluids.Length == 0)
            fluids = FindObjectsOfType<SPHFluid2D>();

        if (showDebugCounter)
            SetupCounter();
    }

    private void SetupCounter()
    {
        GameObject counterObj = new GameObject("ParticleCounter");
        counterObj.transform.SetParent(transform, false);
        counterObj.transform.localPosition = new Vector3(counterOffset.x, counterOffset.y, 0f);

        counterText = counterObj.AddComponent<TextMeshPro>();
        counterText.fontSize = counterFontSize;
        counterText.color = counterColor;
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.sortingOrder = 15;
        counterText.text = $"0/{capacity}";

        RectTransform rt = counterObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(3f, 1.5f);
    }

    private void FixedUpdate()
    {
        if (fluids == null)
            return;

        // Count particles currently resting inside the detection zone.
        // This is a LIVE count — if water spills out, the count drops.
        Vector2 center = (Vector2)transform.position + detectionOffset;
        Vector2 halfSize = detectionSize * 0.5f;

        int count = 0;

        for (int f = 0; f < fluids.Length; f++)
        {
            SPHFluid2D fluid = fluids[f];
            if (fluid == null || !fluid.enabled)
                continue;

            var particles = fluid.Particles;
            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 pos = particles[i].position;

                // Inside the detection rectangle?
                if (pos.x < center.x - halfSize.x || pos.x > center.x + halfSize.x)
                    continue;
                if (pos.y < center.y - halfSize.y || pos.y > center.y + halfSize.y)
                    continue;

                count++;
            }
        }

        currentCount = count;

        isFull = currentCount >= capacity;
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText == null)
            return;

        int percent = Mathf.RoundToInt(FillPercent * 100f);
        counterText.text = $"{percent}%";
        counterText.color = isFull ? counterFullColor : counterColor;
    }

    public void ResetCollector()
    {
        currentCount = 0;
        isFull = false;
        UpdateCounter();
    }

    private void OnDrawGizmos()
    {
        // Draw the detection zone so you can see and size it in the Scene view
        Vector2 center = (Vector2)transform.position + detectionOffset;

        Gizmos.color = isFull
            ? new Color(0.2f, 1.0f, 0.3f, 0.5f)
            : new Color(0.2f, 0.6f, 1.0f, 0.4f);
        Gizmos.DrawWireCube(center, detectionSize);

        // Filled semi-transparent interior
        Gizmos.color = isFull
            ? new Color(0.2f, 1.0f, 0.3f, 0.15f)
            : new Color(0.2f, 0.6f, 1.0f, 0.1f);
        Gizmos.DrawCube(center, detectionSize);
    }
}
