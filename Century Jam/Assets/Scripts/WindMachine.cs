using UnityEngine;

public class WindMachine : MonoBehaviour
{
    public enum WindShape { Cone, Rectangle }

    [Header("Wind")]
    [SerializeField] private bool windEnabled = true;
    [SerializeField] private WindShape shape = WindShape.Cone;
    [Tooltip("Force applied to particles.")]
    [SerializeField] private float windStrength = 0.003f;
    [Tooltip("How far the wind reaches from the source.")]
    [SerializeField] private float windRange = 5.0f;

    [Header("Cone Settings")]
    [Tooltip("Half-angle of the wind cone in degrees. Only used when Shape = Cone.")]
    [SerializeField] private float coneHalfAngle = 30.0f;

    [Header("Rectangle Settings")]
    [Tooltip("Width of the rectangle perpendicular to the wind direction. Only used when Shape = Rectangle.")]
    [SerializeField] private float rectWidth = 2.0f;

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
        SetupVisual();
    }

    // ───────────────────── Visual Setup ─────────────────────

    private void SetupVisual()
    {
        if (windVisualMaterial == null)
            return;

        visualQuad = new GameObject("WindVisual");
        visualQuad.transform.SetParent(transform, false);
        visualQuad.transform.localPosition = Vector3.zero;
        visualQuad.transform.localRotation = Quaternion.identity;
        visualQuad.transform.localScale = Vector3.one;

        MeshFilter mf = visualQuad.AddComponent<MeshFilter>();
        mf.mesh = (shape == WindShape.Cone) ? CreateConeMesh() : CreateRectMesh();

        MeshRenderer mr = visualQuad.AddComponent<MeshRenderer>();
        visualMatInstance = new Material(windVisualMaterial);
        mr.material = visualMatInstance;
        mr.sortingOrder = visualSortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private Mesh CreateConeMesh()
    {
        int segments = 24;
        Vector3[] verts = new Vector3[segments + 2];
        Vector2[] uvs = new Vector2[segments + 2];

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
            uvs[i + 1] = new Vector2(1f, t);
        }

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

    private Mesh CreateRectMesh()
    {
        float halfW = rectWidth * 0.5f;

        // Rectangle from origin extending along local +X
        // UV.x = 0 at source, 1 at range end
        // UV.y = 0..1 across the width
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(0f,         -halfW, 0f),
            new Vector3(windRange,  -halfW, 0f),
            new Vector3(windRange,   halfW, 0f),
            new Vector3(0f,          halfW, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // ───────────────────── Physics ─────────────────────

    private void FixedUpdate()
    {
        if (!windEnabled || allFluids == null)
            return;

        if (shape == WindShape.Cone)
            ApplyConWind();
        else
            ApplyRectWind();
    }

    private void ApplyConWind()
    {
        Vector2 origin = transform.position;
        Vector2 forward = GetCurrentWindDirection();
        float cosThreshold = Mathf.Cos(coneHalfAngle * Mathf.Deg2Rad);
        float rangeSqr = windRange * windRange;

        for (int f = 0; f < allFluids.Length; f++)
        {
            SPHFluid2D fluid = allFluids[f];
            if (fluid == null || !fluid.enabled) continue;

            var particles = fluid.Particles;
            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 toParticle = particles[i].position - origin;
                float distSqr = toParticle.sqrMagnitude;
                if (distSqr > rangeSqr || distSqr < 0.001f) continue;

                float dist = Mathf.Sqrt(distSqr);
                Vector2 dir = toParticle / dist;

                float dot = Vector2.Dot(dir, forward);
                if (dot < cosThreshold) continue;

                float distanceFalloff = 1.0f - (dist / windRange);
                float coneFalloff = (dot - cosThreshold) / (1.0f - cosThreshold);
                float force = windStrength * distanceFalloff * coneFalloff;

                fluid.AddForce(i, forward * force);
            }
        }
    }

    private void ApplyRectWind()
    {
        Vector2 origin = transform.position;
        Vector2 forward = GetCurrentWindDirection();
        Vector2 perp = new Vector2(-forward.y, forward.x);
        float halfW = rectWidth * 0.5f;

        for (int f = 0; f < allFluids.Length; f++)
        {
            SPHFluid2D fluid = allFluids[f];
            if (fluid == null || !fluid.enabled) continue;

            var particles = fluid.Particles;
            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 toParticle = particles[i].position - origin;

                // Project onto the wind direction (how far along)
                float alongDist = Vector2.Dot(toParticle, forward);
                if (alongDist < 0f || alongDist > windRange) continue;

                // Project perpendicular (how far across)
                float acrossDist = Vector2.Dot(toParticle, perp);
                if (Mathf.Abs(acrossDist) > halfW) continue;

                // Falloff: weaker at the edges and at range end
                float distanceFalloff = 1.0f - (alongDist / windRange);
                float edgeFalloff = 1.0f - (Mathf.Abs(acrossDist) / halfW);
                float force = windStrength * distanceFalloff * edgeFalloff;

                fluid.AddForce(i, forward * force);
            }
        }
    }

    // ───────────────────── Visual Update ─────────────────────

    private void Update()
    {
        if (visualQuad == null) return;

        visualQuad.SetActive(windEnabled);

        if (visualMatInstance != null)
        {
            Vector2 dir = GetCurrentWindDirection();
            visualMatInstance.SetVector("_WindDir", new Vector4(dir.x, dir.y, 0, 0));
            visualMatInstance.SetFloat("_WindStrength", windEnabled ? windStrength : 0f);
        }
    }

    // ───────────────────── Helpers ─────────────────────

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

    public void SetEnabled(bool enabled) => windEnabled = enabled;
    public void Toggle() => windEnabled = !windEnabled;
    public void SetStrength(float strength) => windStrength = strength;
    public void RefreshFluids() => allFluids = FindObjectsOfType<SPHFluid2D>();

    private void OnDestroy()
    {
        if (visualQuad != null)
            Destroy(visualQuad);
    }

    // ───────────────────── Gizmos ─────────────────────

    private void OnDrawGizmos()
    {
        Vector2 origin = transform.position;
        float angle = transform.eulerAngles.z;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        Gizmos.color = windEnabled ? new Color(0.4f, 0.8f, 1.0f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);

        if (shape == WindShape.Cone)
            DrawConeGizmo(origin, angle, forward);
        else
            DrawRectGizmo(origin, forward);
    }

    private void DrawConeGizmo(Vector2 origin, float angle, Vector2 forward)
    {
        float topAngle = (angle + coneHalfAngle) * Mathf.Deg2Rad;
        float botAngle = (angle - coneHalfAngle) * Mathf.Deg2Rad;
        Vector2 topEdge = new Vector2(Mathf.Cos(topAngle), Mathf.Sin(topAngle)) * windRange;
        Vector2 botEdge = new Vector2(Mathf.Cos(botAngle), Mathf.Sin(botAngle)) * windRange;

        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + topEdge));
        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + botEdge));

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

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + forward * windRange * 0.5f));
    }

    private void DrawRectGizmo(Vector2 origin, Vector2 forward)
    {
        Vector2 perp = new Vector2(-forward.y, forward.x);
        float halfW = rectWidth * 0.5f;

        Vector2 nearL = origin - perp * halfW;
        Vector2 nearR = origin + perp * halfW;
        Vector2 farL = nearL + forward * windRange;
        Vector2 farR = nearR + forward * windRange;

        Gizmos.DrawLine((Vector3)nearL, (Vector3)farL);
        Gizmos.DrawLine((Vector3)nearR, (Vector3)farR);
        Gizmos.DrawLine((Vector3)nearL, (Vector3)nearR);
        Gizmos.DrawLine((Vector3)farL, (Vector3)farR);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine((Vector3)origin, (Vector3)(origin + forward * windRange * 0.5f));
    }
}
