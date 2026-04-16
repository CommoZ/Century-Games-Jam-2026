using UnityEngine;

public class WindMachine : MonoBehaviour
{
    [Header("Wind")]
    [SerializeField] private bool windEnabled = true;
    [Tooltip("Force applied to particles directly in front of the fan.")]
    [SerializeField] private float windStrength = 0.003f;
    [Tooltip("How far the wind reaches from the fan.")]
    [SerializeField] private float windRange = 5.0f;
    [Tooltip("Half-angle of the wind cone in degrees. 0 = laser, 45 = wide fan, 90 = hemisphere.")]
    [SerializeField] private float coneHalfAngle = 30.0f;

    [Header("Oscillation (optional)")]
    [Tooltip("If > 0, the fan sweeps back and forth by this many degrees.")]
    [SerializeField] private float oscillationAngle = 0f;
    [SerializeField] private float oscillationSpeed = 0.5f;

    [Header("Wind Visual")]
    [Tooltip("Material using Fluid/WindEffect shader. Leave empty to skip visual.")]
    [SerializeField] private Material windVisualMaterial;
    [SerializeField] private int visualSortingOrder = 8;

    private SPHFluid2D[] allFluids;
    private float oscillationTimer;
    private GameObject visualQuad;
    private Material visualMatInstance;

    private void Start()
    {
        allFluids = FindObjectsOfType<SPHFluid2D>();
        SetupVisualQuad();
    }

    private void SetupVisualQuad()
    {
        if (windVisualMaterial == null)
            return;

        visualQuad = new GameObject("WindVisual");
        visualQuad.transform.SetParent(transform, false);
        visualQuad.transform.localPosition = Vector3.zero;
        visualQuad.transform.localRotation = Quaternion.identity;
        visualQuad.transform.localScale = Vector3.one;

        MeshFilter mf = visualQuad.AddComponent<MeshFilter>();
        mf.mesh = CreateConeMesh();

        MeshRenderer mr = visualQuad.AddComponent<MeshRenderer>();
        visualMatInstance = new Material(windVisualMaterial);
        mr.material = visualMatInstance;
        mr.sortingOrder = visualSortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>
    /// Builds a fan/cone mesh that matches the exact physics AOE.
    /// Origin at (0,0), extending along local +X out to windRange.
    /// UV.x = 0..1 from source to range end, UV.y = 0..1 across the cone width.
    /// </summary>
    private Mesh CreateConeMesh()
    {
        int segments = 24;
        // +1 for the origin vertex
        Vector3[] verts = new Vector3[segments + 2];
        Vector2[] uvs = new Vector2[segments + 2];

        // Origin (the fan source)
        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0f, 0.5f);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(-coneHalfAngle, coneHalfAngle, t) * Mathf.Deg2Rad;

            verts[i + 1] = new Vector3(
                Mathf.Cos(angle) * windRange,
                Mathf.Sin(angle) * windRange,
                0f
            );
            // UV.x = 1 at range end, UV.y = 0..1 across the width
            uvs[i + 1] = new Vector2(1f, t);
        }

        // Triangles: fan from origin to each pair of arc vertices
        int[] tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void FixedUpdate()
    {
        if (!windEnabled || allFluids == null)
            return;

        Vector2 origin = transform.position;
        Vector2 forward = GetCurrentWindDirection();
        float cosThreshold = Mathf.Cos(coneHalfAngle * Mathf.Deg2Rad);
        float rangeSqr = windRange * windRange;

        for (int f = 0; f < allFluids.Length; f++)
        {
            SPHFluid2D fluid = allFluids[f];
            if (fluid == null || !fluid.enabled)
                continue;

            var particles = fluid.Particles;
            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 toParticle = particles[i].position - origin;
                float distSqr = toParticle.sqrMagnitude;

                // Outside range?
                if (distSqr > rangeSqr || distSqr < 0.001f)
                    continue;

                float dist = Mathf.Sqrt(distSqr);
                Vector2 dir = toParticle / dist;

                // Outside cone?
                float dot = Vector2.Dot(dir, forward);
                if (dot < cosThreshold)
                    continue;

                // Strength falls off with distance and is stronger at cone center
                float distanceFalloff = 1.0f - (dist / windRange);
                float coneFalloff = (dot - cosThreshold) / (1.0f - cosThreshold);
                float force = windStrength * distanceFalloff * coneFalloff;

                fluid.AddForce(i, forward * force);
            }
        }
    }

    private void Update()
    {
        UpdateVisual();
    }

    private Vector2 GetCurrentWindDirection()
    {
        float angle = transform.eulerAngles.z;

        if (oscillationAngle > 0f)
        {
            oscillationTimer += Time.deltaTime;
            float wave = Mathf.Sin(oscillationTimer * oscillationSpeed * Mathf.PI * 2f);
            angle += wave * oscillationAngle;
        }

        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void UpdateVisual()
    {
        if (visualQuad == null)
            return;

        visualQuad.SetActive(windEnabled);

        if (visualMatInstance != null)
        {
            Vector2 dir = GetCurrentWindDirection();
            visualMatInstance.SetVector("_WindDir", new Vector4(dir.x, dir.y, 0, 0));
            visualMatInstance.SetFloat("_WindStrength", windEnabled ? windStrength : 0f);
        }
    }

    public void SetEnabled(bool enabled) => windEnabled = enabled;
    public void Toggle() => windEnabled = !windEnabled;
    public void SetStrength(float strength) => windStrength = strength;
    public void RefreshFluids() => allFluids = FindObjectsOfType<SPHFluid2D>();

    private void OnDestroy()
    {
        if (visualQuad != null)
            Destroy(visualQuad);
    }

    private void OnDrawGizmos()
    {
        Vector2 origin = transform.position;
        float angle = transform.eulerAngles.z;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // Cone outline
        float topAngle = (angle + coneHalfAngle) * Mathf.Deg2Rad;
        float botAngle = (angle - coneHalfAngle) * Mathf.Deg2Rad;
        Vector2 topEdge = new Vector2(Mathf.Cos(topAngle), Mathf.Sin(topAngle)) * windRange;
        Vector2 botEdge = new Vector2(Mathf.Cos(botAngle), Mathf.Sin(botAngle)) * windRange;

        Gizmos.color = windEnabled ? new Color(0.4f, 0.8f, 1.0f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + topEdge));
        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + botEdge));

        // Arc
        int segments = 16;
        Vector2 prev = origin + botEdge;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(angle - coneHalfAngle, angle + coneHalfAngle, t) * Mathf.Deg2Rad;
            Vector2 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * windRange;
            Gizmos.DrawLine((Vector3)prev, (Vector3)p);
            prev = p;
        }

        // Center direction arrow
        Gizmos.color = Color.cyan;
        Vector3 tip = (Vector3)(origin + forward * windRange * 0.5f);
        Gizmos.DrawLine((Vector3)origin, tip);
    }
}
