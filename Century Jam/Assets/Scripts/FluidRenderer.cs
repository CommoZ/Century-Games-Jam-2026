using UnityEngine;

[RequireComponent(typeof(SPHFluid2D))]
public class FluidRenderer : MonoBehaviour
{
    [Header("Metaball Rendering")]
    [SerializeField] private Material metaballParticleMaterial;
    [SerializeField] private Material waterThresholdMaterial;
    [SerializeField] private float particleRenderSize = 0.5f;
    [SerializeField] private float velocityStretch = 3.0f;
    [SerializeField] private float maxStretch = 2.0f;
    [SerializeField] private int renderTextureScale = 1;

    [Header("Display")]
    [SerializeField] private int sortingOrder = 10;

    private SPHFluid2D fluid;
    private RenderTexture metaballRT;
    private GameObject displayQuad;
    private Camera mainCam;

    private void Start()
    {
        fluid = GetComponent<SPHFluid2D>();
        mainCam = Camera.main;

        SetupDisplayQuad();
        HideSpriteVisuals();
    }

    private void LateUpdate()
    {
        mainCam = Camera.main;
        if (mainCam == null)
            return;

        // When there are no particles, hide the display quad so the last
        // rendered frame doesn't stay stuck on screen after a clear.
        if (fluid.ParticleCount == 0)
        {
            if (displayQuad != null)
            {
                MeshRenderer mr = displayQuad.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                    mr.enabled = false;
            }
            return;
        }

        if (displayQuad != null)
        {
            MeshRenderer mr = displayQuad.GetComponent<MeshRenderer>();
            if (mr != null && !mr.enabled)
                mr.enabled = true;
        }

        EnsureRenderTexture();
        RenderMetaballs();
        UpdateDisplayQuad();
    }

    private void SetupDisplayQuad()
    {
        displayQuad = new GameObject("WaterDisplay");
        displayQuad.transform.SetParent(transform);

        MeshFilter mf = displayQuad.AddComponent<MeshFilter>();
        mf.mesh = CreateQuadMesh();

        MeshRenderer mr = displayQuad.AddComponent<MeshRenderer>();
        mr.material = waterThresholdMaterial;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void EnsureRenderTexture()
    {
        int w = Screen.width / Mathf.Max(1, renderTextureScale);
        int h = Screen.height / Mathf.Max(1, renderTextureScale);

        if (metaballRT != null && metaballRT.width == w && metaballRT.height == h)
            return;

        if (metaballRT != null)
            metaballRT.Release();

        metaballRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
        metaballRT.filterMode = FilterMode.Bilinear;
        metaballRT.Create();
    }

    private void RenderMetaballs()
    {
        if (metaballParticleMaterial == null)
            return;

        // Save current RT and switch to our metaball RT
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = metaballRT;
        GL.Clear(true, true, Color.clear);

        // Set up orthographic projection matching the main camera
        float halfHeight = mainCam.orthographicSize;
        float halfWidth = halfHeight * mainCam.aspect;
        Vector3 camPos = mainCam.transform.position;

        Matrix4x4 proj = Matrix4x4.Ortho(
            camPos.x - halfWidth, camPos.x + halfWidth,
            camPos.y - halfHeight, camPos.y + halfHeight,
            -1, 1
        );

        metaballParticleMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadProjectionMatrix(proj);
        GL.LoadIdentity();

        var particles = fluid.Particles;
        int count = fluid.ParticleCount;
        float half = particleRenderSize * 0.5f;

        GL.Begin(GL.QUADS);
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = particles[i].position;
            Vector2 vel = particles[i].velocity;
            float speed = vel.magnitude;

            float stretch = Mathf.Min(speed * velocityStretch, maxStretch);

            float speedNorm = Mathf.Clamp01(speed * 20.0f);
            float teardropAmount = Mathf.Clamp01(stretch / Mathf.Max(0.01f, maxStretch));

            if (stretch > 0.01f)
            {
                Vector2 dir = vel / speed;
                Vector2 perp = new Vector2(-dir.y, dir.x);

                Vector2 forward = dir * (half + stretch);
                Vector2 side = perp * half;

                Vector2 bl = pos - forward - side;
                Vector2 br = pos - forward + side;
                Vector2 tr = pos + forward + side;
                Vector2 tl = pos + forward - side;

                GL.MultiTexCoord2(0, 0, 0); GL.MultiTexCoord2(1, speedNorm, teardropAmount); GL.Vertex3(bl.x, bl.y, 0);
                GL.MultiTexCoord2(0, 1, 0); GL.MultiTexCoord2(1, speedNorm, teardropAmount); GL.Vertex3(br.x, br.y, 0);
                GL.MultiTexCoord2(0, 1, 1); GL.MultiTexCoord2(1, speedNorm, teardropAmount); GL.Vertex3(tr.x, tr.y, 0);
                GL.MultiTexCoord2(0, 0, 1); GL.MultiTexCoord2(1, speedNorm, teardropAmount); GL.Vertex3(tl.x, tl.y, 0);
            }
            else
            {
                GL.MultiTexCoord2(0, 0, 0); GL.MultiTexCoord2(1, 0, 0); GL.Vertex3(pos.x - half, pos.y - half, 0);
                GL.MultiTexCoord2(0, 1, 0); GL.MultiTexCoord2(1, 0, 0); GL.Vertex3(pos.x + half, pos.y - half, 0);
                GL.MultiTexCoord2(0, 1, 1); GL.MultiTexCoord2(1, 0, 0); GL.Vertex3(pos.x + half, pos.y + half, 0);
                GL.MultiTexCoord2(0, 0, 1); GL.MultiTexCoord2(1, 0, 0); GL.Vertex3(pos.x - half, pos.y + half, 0);
            }
        }
        GL.End();

        GL.PopMatrix();
        RenderTexture.active = prev;
    }

    private void UpdateDisplayQuad()
    {
        if (waterThresholdMaterial == null || mainCam == null)
            return;

        float height = mainCam.orthographicSize * 2.0f;
        float width = height * mainCam.aspect;

        displayQuad.transform.position = new Vector3(
            mainCam.transform.position.x,
            mainCam.transform.position.y,
            0
        );
        displayQuad.transform.localScale = new Vector3(width, height, 1);

        waterThresholdMaterial.SetTexture("_MainTex", metaballRT);
    }

    private void HideSpriteVisuals()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in sprites)
            sr.enabled = false;
    }

    private void OnDestroy()
    {
        if (metaballRT != null)
        {
            metaballRT.Release();
            metaballRT = null;
        }

        if (displayQuad != null)
            Destroy(displayQuad);
    }
}
