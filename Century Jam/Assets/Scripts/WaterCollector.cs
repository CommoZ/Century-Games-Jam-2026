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
        // OBB detection: transform particle positions into local space
        // so the check works correctly with rotation and scale
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
                // Convert particle world position to collector's local space
                Vector2 local = transform.InverseTransformPoint(particles[i].position);

                // Offset by detection offset (in local space)
                local -= detectionOffset;

                // Inside the local-space rectangle?
                if (local.x < -halfSize.x || local.x > halfSize.x)
                    continue;
                if (local.y < -halfSize.y || local.y > halfSize.y)
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
        // Draw the OBB detection zone respecting rotation and scale
        Vector2 halfSize = detectionSize * 0.5f;

        // Four local-space corners offset by detectionOffset
        Vector3 tl = new Vector3(detectionOffset.x - halfSize.x, detectionOffset.y + halfSize.y, 0f);
        Vector3 tr = new Vector3(detectionOffset.x + halfSize.x, detectionOffset.y + halfSize.y, 0f);
        Vector3 br = new Vector3(detectionOffset.x + halfSize.x, detectionOffset.y - halfSize.y, 0f);
        Vector3 bl = new Vector3(detectionOffset.x - halfSize.x, detectionOffset.y - halfSize.y, 0f);

        // Transform to world space (includes position, rotation, scale)
        tl = transform.TransformPoint(tl);
        tr = transform.TransformPoint(tr);
        br = transform.TransformPoint(br);
        bl = transform.TransformPoint(bl);

        Gizmos.color = isFull
            ? new Color(0.2f, 1.0f, 0.3f, 0.5f)
            : new Color(0.2f, 0.6f, 1.0f, 0.4f);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, tl);
    }
}
