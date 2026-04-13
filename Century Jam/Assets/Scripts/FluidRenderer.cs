using UnityEngine;

[RequireComponent(typeof(SPHFluid2D))]
public class FluidRenderer : MonoBehaviour
{
    [Header("Metaball Rendering")]
    [SerializeField] private Material metaballParticleMaterial;
    [SerializeField] private Material waterThresholdMaterial;
    [SerializeField] private float particleRenderSize = 0.5f;
    [SerializeField] private int renderTextureScale = 1;

    [Header("Display")]
    [SerializeField] private int sortingOrder = 10;

    private SPHFluid2D fluid;
    private Camera metaballCamera;
    private RenderTexture metaballRT;
    private GameObject displayQuad;
    private Mesh particleMesh;
    private MaterialPropertyBlock mpb;
    private Camera mainCam;

    private void Start()
    {
        fluid = GetComponent<SPHFluid2D>();
        mainCam = Camera.main;
        mpb = new MaterialPropertyBlock();

        CreateParticleMesh();
        SetupMetaballCamera();
        SetupDisplayQuad();

        // Hide the sprite visuals — the metaball shader replaces them
        HideSpriteVisuals();
    }

    private void LateUpdate()
    {
        if (fluid.ParticleCount == 0)
            return;

        EnsureRenderTexture();
        RenderMetaballs();
        UpdateDisplayQuad();
    }

    private void CreateParticleMesh()
    {
        particleMesh = new Mesh();
        particleMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0)
        };
        particleMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        particleMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        particleMesh.RecalculateBounds();
    }

    private void SetupMetaballCamera()
    {
        GameObject camObj = new GameObject("MetaballCamera");
        camObj.transform.SetParent(transform);
        metaballCamera = camObj.AddComponent<Camera>();
        metaballCamera.enabled = false; // We render manually
        metaballCamera.clearFlags = CameraClearFlags.SolidColor;
        metaballCamera.backgroundColor = Color.clear;
        metaballCamera.orthographic = true;
        metaballCamera.cullingMask = 0; // We draw manually, not via layers
        metaballCamera.depth = -100;
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
        if (metaballParticleMaterial == null || mainCam == null)
            return;

        // Match the metaball camera to the main camera's view
        metaballCamera.orthographicSize = mainCam.orthographicSize;
        metaballCamera.transform.position = mainCam.transform.position;
        metaballCamera.transform.rotation = mainCam.transform.rotation;
        metaballCamera.aspect = mainCam.aspect;

        // Render to the RT
        metaballCamera.targetTexture = metaballRT;
        metaballCamera.Render();

        // Draw each particle as a soft quad
        RenderTexture prev = RenderTexture.active;
        Graphics.SetRenderTarget(metaballRT);
        GL.Clear(true, true, Color.clear);

        // Set up orthographic projection matching the main camera
        float halfHeight = mainCam.orthographicSize;
        float halfWidth = halfHeight * mainCam.aspect;
        Vector3 camPos = mainCam.transform.position;

        Matrix4x4 view = Matrix4x4.TRS(
            new Vector3(-camPos.x, -camPos.y, 0),
            Quaternion.identity,
            Vector3.one
        );
        Matrix4x4 proj = Matrix4x4.Ortho(
            -halfWidth, halfWidth,
            -halfHeight, halfHeight,
            -1, 1
        );

        metaballParticleMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadProjectionMatrix(proj);
        GL.LoadIdentity();
        GL.MultMatrix(view);

        var particles = fluid.Particles;
        int count = fluid.ParticleCount;
        float half = particleRenderSize * 0.5f;

        GL.Begin(GL.QUADS);
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = particles[i].position;

            GL.TexCoord2(0, 0);
            GL.Vertex3(pos.x - half, pos.y - half, 0);

            GL.TexCoord2(1, 0);
            GL.Vertex3(pos.x + half, pos.y - half, 0);

            GL.TexCoord2(1, 1);
            GL.Vertex3(pos.x + half, pos.y + half, 0);

            GL.TexCoord2(0, 1);
            GL.Vertex3(pos.x - half, pos.y + half, 0);
        }
        GL.End();

        GL.PopMatrix();
        RenderTexture.active = prev;
    }

    private void UpdateDisplayQuad()
    {
        if (waterThresholdMaterial == null || mainCam == null)
            return;

        // Size the quad to cover the camera's visible area
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
        // Disable sprite renderers on particle children — metaball takes over
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

        if (metaballCamera != null)
            Destroy(metaballCamera.gameObject);

        if (displayQuad != null)
            Destroy(displayQuad);
    }
}
