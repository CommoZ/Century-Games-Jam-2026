using System.Collections.Generic;
using UnityEngine;

public class SPHFluid2D : MonoBehaviour
{
    [System.Serializable]
    public struct FluidParticle
    {
        public Vector2 position;
        public Vector2 positionOld;
        public Vector2 velocity;
        public Vector2 force;
        public float density;
        public float densityNear;
        public float pressure;
        public float pressureNear;
        public float sigma;
        public float beta;
    }

    private struct Neighbor
    {
        public readonly int index;
        public readonly float q;
        public readonly float q2;

        public Neighbor(int index, float q, float q2)
        {
            this.index = index;
            this.q = q;
            this.q2 = q2;
        }
    }

    [Header("Initial Spawn")]
    [SerializeField] private int particlesX = 20;
    [SerializeField] private int particlesY = 12;
    [SerializeField] private float particleSpacing = 0.18f;
    [SerializeField] private Vector2 spawnOrigin = new Vector2(-1.5f, 4.0f);
    [SerializeField] private float initialJitter = 0.0001f;
    [SerializeField] private bool spawnInitialBlock = true;

    [Header("Stream Spawning")]
    [SerializeField] private bool streamEnabled = false;
    [SerializeField] private Transform streamSource;
    [SerializeField] private Vector2 streamDirection = Vector2.down;
    [SerializeField] private float streamSpeed = 0.05f;
    [SerializeField] private float streamWidth = 0.5f;
    [SerializeField] private int streamParticlesPerSecond = 60;
    [SerializeField] private int maxParticles = 1000;
    [Tooltip("How long the stream spawns particles (seconds). 0 = unlimited.")]
    [SerializeField] private float streamDuration = 0f;

    [Header("Physics")]
    [SerializeField] private float particleMass = 1.0f;
    [SerializeField] private float smoothingRadius = 0.35f;
    [SerializeField] private float restDensity = 3.0f;
    [SerializeField] private float stiffness = 0.00028f;
    [SerializeField] private float nearPressureMultiplier = 10.0f;
    [SerializeField] private float viscosity = 3.0f;
    [SerializeField] private float viscosityBeta = 4.0f;
    [SerializeField] private float surfaceTension = 0.005f;
    [SerializeField] private Vector2 gravity = new Vector2(0.0f, -0.0007f);
    [SerializeField] private float maxAccel = 0.29f;
    [SerializeField] private int simulationStepsPerFixedUpdate = 1;

    [Header("Bounds")]
    [SerializeField] private Vector2 boundsMin = new Vector2(-4.0f, 0.0f);
    [SerializeField] private Vector2 boundsMax = new Vector2(4.0f, 8.0f);
    [SerializeField] private float boundaryDamping = 0.125f;
    [SerializeField] private bool useTopBounds = false;

    [Header("Cull Offscreen Particles")]
    [Tooltip("If true, particles outside the main camera view are removed from the simulation.")]
    [SerializeField] private bool cullOffscreen = true;
    [Tooltip("Extra margin (in world units) outside the camera view before a particle is killed.")]
    [SerializeField] private float cullMargin = 1.0f;
    [Tooltip("How often (seconds) to check for offscreen particles. 0 = every physics step.")]
    [SerializeField] private float cullCheckInterval = 0.1f;

    [Header("Collider Interaction")]
    [SerializeField] private LayerMask wallLayerMask = 0;
    [SerializeField] private float wallCollisionRadius = 0.05f;
    [SerializeField] private float wallSpringForce = 0.5f;

    [Header("Rendering")]
    [SerializeField] private GameObject particlePrefab;

    [Header("Debug")]
    [SerializeField] private float gizmoParticleRadius = 0.07f;
    [SerializeField] private bool drawBounds = true;
    [SerializeField] private bool drawParticles = true;

    private const float MinRadius = 0.0001f;
    private const float MinDistanceSqr = 0.00000001f;

    private readonly List<FluidParticle> particles = new List<FluidParticle>();
    private readonly List<Transform> particleVisuals = new List<Transform>();
    private readonly List<SpriteRenderer> particleRenderers = new List<SpriteRenderer>();
    private List<Neighbor>[] particleNeighbors = new List<Neighbor>[0];

    private readonly Dictionary<Vector2Int, List<int>> spatialHash = new Dictionary<Vector2Int, List<int>>();
    private readonly List<int> neighborCandidates = new List<int>(64);

    private readonly List<Vector2> preStepPositions = new List<Vector2>();
    private float spawnAccumulator;
    private float streamTimer;
    private float cullTimer;
    private Camera cullCamera;

    public int ParticleCount => particles.Count;
    public List<FluidParticle> Particles => particles;
    public bool StreamFinished => streamEnabled && streamDuration > 0f && streamTimer >= streamDuration;

    private float SupportRadius => Mathf.Max(MinRadius, smoothingRadius);
    private float SupportRadiusSqr => SupportRadius * SupportRadius;
    private float NearPressureWeight => stiffness * nearPressureMultiplier;


    private void Start()
    {
        if (spawnInitialBlock)
            SpawnBlock();
    }

    private void FixedUpdate()
    {
        if (streamEnabled)
            SpawnStream();

        if (particles.Count == 0)
            return;

        int steps = Mathf.Max(1, simulationStepsPerFixedUpdate);
        for (int step = 0; step < steps; step++)
            StepSimulation();

        if (cullOffscreen)
            CullOffscreenParticles();

        UpdateVisuals();
    }

    public void startGame()
    {
        if (streamEnabled == false)
        {
            streamDuration = 5.0f; // Example: stream for 5 seconds when the button is pressed
            streamTimer = 0.0f; // Reset the stream timer
            streamEnabled = true;
        }
    }

    public void stopGame()
    {
        streamEnabled = false;
        streamDuration = 0.0f; // Stop the stream immediately
    }

    private void CullOffscreenParticles()
    {
        cullTimer += Time.fixedDeltaTime;
        if (cullTimer < cullCheckInterval)
            return;
        cullTimer = 0f;

        if (cullCamera == null)
            cullCamera = Camera.main;
        if (cullCamera == null)
            return;

        // Compute camera world-space bounds (orthographic)
        float halfH = cullCamera.orthographicSize + cullMargin;
        float halfW = cullCamera.orthographicSize * cullCamera.aspect + cullMargin;
        Vector2 camPos = cullCamera.transform.position;
        float minX = camPos.x - halfW;
        float maxX = camPos.x + halfW;
        float minY = camPos.y - halfH;
        float maxY = camPos.y + halfH;

        // Iterate backwards because RemoveParticle uses swap-remove
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Vector2 pos = particles[i].position;
            if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY)
                RemoveParticle(i);
        }
    }

    private void StepSimulation()
    {
        EnsureNeighborStorage();
        AdvanceParticles();
        ResolveColliderInteraction();
        BuildSpatialHash();
        ComputeDensities();
        ComputePressures();
        ComputePressureForces();
        ApplySurfaceTension();
        ApplyViscosity();
    }

    private void SpawnBlock()
    {
        if (particlesX <= 0 || particlesY <= 0)
            return;

        for (int y = 0; y < particlesY; y++)
        {
            for (int x = 0; x < particlesX; x++)
            {
                if (particles.Count >= maxParticles)
                    return;

                Vector2 position = spawnOrigin + new Vector2(x * particleSpacing, y * particleSpacing);
                Vector2 jitter = initialJitter * new Vector2(Random.value, Random.value);
                AddParticle(position, jitter);
            }
        }
    }

    private void SpawnStream()
    {
        if (particles.Count >= maxParticles || streamParticlesPerSecond <= 0)
            return;

        if (streamDuration > 0f)
        {
            streamTimer += Time.fixedDeltaTime;
            if (streamTimer >= streamDuration)
                return;
        }

        spawnAccumulator += Time.fixedDeltaTime * streamParticlesPerSecond;

        Vector2 origin = streamSource != null ? (Vector2)streamSource.position : spawnOrigin;
        Vector2 dir = streamDirection.normalized;
        // Perpendicular direction for spreading particles across the stream width
        Vector2 perp = new Vector2(-dir.y, dir.x);

        while (spawnAccumulator >= 1.0f && particles.Count < maxParticles)
        {
            spawnAccumulator -= 1.0f;

            float offset = Random.Range(-0.5f, 0.5f) * streamWidth;
            Vector2 position = origin + perp * offset;
            Vector2 initialVelocity = dir * streamSpeed + new Vector2(Random.Range(-0.001f, 0.001f), Random.Range(-0.001f, 0.001f));

            AddParticle(position, -initialVelocity);
        }
    }

    private void AddParticle(Vector2 position, Vector2 jitter)
    {
        FluidParticle p = new FluidParticle
        {
            position = position,
            positionOld = position + jitter,
            velocity = Vector2.zero,
            force = Vector2.zero,
            density = 0.0f,
            densityNear = 0.0f,
            pressure = 0.0f,
            pressureNear = 0.0f,
            sigma = viscosity,
            beta = viscosityBeta
        };

        particles.Add(p);

        if (particlePrefab != null)
        {
            GameObject obj = Instantiate(particlePrefab, position, Quaternion.identity, transform);
            particleVisuals.Add(obj.transform);
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            particleRenderers.Add(sr);
        }
        else
        {
            particleVisuals.Add(null);
            particleRenderers.Add(null);
        }
    }

    private void EnsureNeighborStorage()
    {
        if (particleNeighbors.Length >= particles.Count)
            return;

        int oldLength = particleNeighbors.Length;
        System.Array.Resize(ref particleNeighbors, particles.Count);
        for (int i = oldLength; i < particleNeighbors.Length; i++)
            particleNeighbors[i] = new List<Neighbor>(64);
    }

    private void AdvanceParticles()
    {
        float maxVelocity = Mathf.Max(0.0f, maxAccel);
        float maxVelocitySqr = maxVelocity * maxVelocity;
        float boundarySpring = Mathf.Max(0.0f, boundaryDamping);

        // Grow pre-step list if needed
        while (preStepPositions.Count < particles.Count)
            preStepPositions.Add(Vector2.zero);

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];

            // Save position before any movement for collision raycasts
            preStepPositions[i] = p.position;

            // Clamp force so no single step creates an explosion
            if (maxVelocity > 0.0f && p.force.sqrMagnitude > maxVelocitySqr)
                p.force = p.force.normalized * maxVelocity;

            p.position += p.force;
            p.force = gravity;

            p.velocity = p.position - p.positionOld;
            if (maxVelocity > 0.0f && p.velocity.sqrMagnitude > maxVelocitySqr)
                p.velocity = p.velocity.normalized * maxVelocity;

            p.positionOld = p.position;
            p.position += p.velocity;

            ApplyBoundarySpring(ref p, boundarySpring);

            p.density = 0.0f;
            p.densityNear = 0.0f;
            p.pressure = 0.0f;
            p.pressureNear = 0.0f;
            p.sigma = viscosity;
            p.beta = viscosityBeta;

            particleNeighbors[i].Clear();
            particles[i] = p;
        }
    }

    private void ApplyBoundarySpring(ref FluidParticle p, float boundarySpring)
    {
        if (p.position.x < boundsMin.x)
            p.force.x -= (p.position.x - boundsMin.x) * boundarySpring;

        if (p.position.x > boundsMax.x)
            p.force.x -= (p.position.x - boundsMax.x) * boundarySpring;

        if (p.position.y < boundsMin.y)
            p.force.y -= (p.position.y - boundsMin.y) * boundarySpring;

        if (useTopBounds && p.position.y > boundsMax.y)
            p.force.y -= (p.position.y - boundsMax.y) * boundarySpring;
    }

    private void ResolveColliderInteraction()
    {
        if (wallLayerMask == 0)
            return;

        float radius = Mathf.Max(0.001f, wallCollisionRadius);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(wallLayerMask);
        filter.useTriggers = false;

        Collider2D[] overlapResults = new Collider2D[4];
        RaycastHit2D[] rayResults = new RaycastHit2D[4];

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            bool resolved = false;

            // 1) Raycast from pre-step position to catch full movement (force + velocity)
            Vector2 origin = i < preStepPositions.Count ? preStepPositions[i] : p.positionOld;
            Vector2 moveDir = p.position - origin;
            float moveDist = moveDir.magnitude;
            if (moveDist > 0.0001f)
            {
                int rayHits = Physics2D.Raycast(origin, moveDir / moveDist, filter, rayResults, moveDist);
                if (rayHits > 0)
                {
                    RaycastHit2D rayHit = rayResults[0];
                    // Tangent jitter so particles don't stack in a perfect line
                    Vector2 tangent = new Vector2(-rayHit.normal.y, rayHit.normal.x);
                    float jitter = Random.Range(-0.02f, 0.02f);
                    p.position = rayHit.point + rayHit.normal * radius + tangent * jitter;

                    float velIntoWall = Vector2.Dot(p.velocity, -rayHit.normal);
                    if (velIntoWall > 0.0f)
                        p.velocity += rayHit.normal * velIntoWall;

                    p.positionOld = p.position - p.velocity;
                    resolved = true;
                }
            }

            // 2) Overlap check — resolve ALL overlapping colliders (handles corners)
            for (int pass = 0; pass < 3; pass++)
            {
                int hitCount = Physics2D.OverlapCircle(p.position, radius, filter, overlapResults);
                if (hitCount == 0)
                    break;

                for (int h = 0; h < hitCount; h++)
                {
                    Collider2D hit = overlapResults[h];
                    Vector2 closest = hit.ClosestPoint(p.position);
                    Vector2 diff = p.position - closest;
                    float dist = diff.magnitude;

                    if (dist < 0.0001f)
                    {
                        Vector2 pushDir = (p.position - (Vector2)hit.bounds.center).normalized;
                        if (pushDir.sqrMagnitude < 0.001f)
                            pushDir = Vector2.up;

                        p.position = closest + pushDir * radius;
                        p.velocity = Vector2.zero;
                        p.positionOld = p.position;
                    }
                    else if (dist < radius)
                    {
                        Vector2 normal = diff / dist;
                        float penetration = radius - dist;

                        p.position += normal * penetration;

                        float velIntoWall = Vector2.Dot(p.velocity, -normal);
                        if (velIntoWall > 0.0f)
                            p.velocity += normal * velIntoWall;

                        p.positionOld = p.position - p.velocity;
                        p.force += normal * penetration * wallSpringForce;
                    }
                }
            }

            // 3) Final containment check — if particle is still inside a collider, it escaped
            Collider2D inside = Physics2D.OverlapPoint(p.position, wallLayerMask);
            if (inside != null)
            {
                Vector2 closest = inside.ClosestPoint(p.position);
                Vector2 toSurface = closest - p.position;
                float surfaceDist = toSurface.magnitude;

                if (surfaceDist > 0.0001f)
                {
                    // Push to surface + buffer along the wall normal
                    Vector2 normal = toSurface / surfaceDist;
                    p.position = closest + normal * radius;
                }
                else
                {
                    // Exactly on or at center — push away from collider center
                    Vector2 escape = (p.position - (Vector2)inside.bounds.center).normalized;
                    if (escape.sqrMagnitude < 0.001f)
                        escape = Vector2.up;
                    p.position += escape * radius;
                }

                p.velocity = Vector2.zero;
                p.positionOld = p.position;
            }

            particles[i] = p;
        }
    }

    private void BuildSpatialHash()
    {
        spatialHash.Clear();
        float invCellSize = 1.0f / SupportRadius;

        for (int i = 0; i < particles.Count; i++)
        {
            Vector2Int cell = Discretize(particles[i].position, invCellSize);
            if (!spatialHash.TryGetValue(cell, out List<int> bucket))
            {
                bucket = new List<int>();
                spatialHash.Add(cell, bucket);
            }

            bucket.Add(i);
        }
    }

    private void ComputeDensities()
    {
        float supportRadius = SupportRadius;
        float radiusSqr = SupportRadiusSqr;

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            List<Neighbor> neighbors = particleNeighbors[i];

            float density = 0.0f;
            float densityNear = 0.0f;

            CollectNeighborCandidates(p.position, neighborCandidates);

            for (int c = 0; c < neighborCandidates.Count; c++)
            {
                int j = neighborCandidates[c];
                if (j == i)
                    continue;

                Vector2 rij = particles[j].position - p.position;
                float distanceSqr = rij.sqrMagnitude;
                if (distanceSqr >= radiusSqr)
                    continue;

                float distance = Mathf.Sqrt(distanceSqr);
                float q = 1.0f - (distance / supportRadius);
                float q2 = q * q;

                density += particleMass * q2;
                densityNear += particleMass * q2 * q;
                neighbors.Add(new Neighbor(j, q, q2));
            }

            p.density = density;
            p.densityNear = densityNear;
            particles[i] = p;
        }
    }

    private void ComputePressures()
    {
        float nearPressureWeight = NearPressureWeight;

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            p.pressure = stiffness * (p.density - restDensity);
            p.pressureNear = nearPressureWeight * p.densityNear;
            particles[i] = p;
        }
    }

    private void ComputePressureForces()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            Vector2 pressureDisplacement = Vector2.zero;

            for (int n = 0; n < particleNeighbors[i].Count; n++)
            {
                Neighbor neighbor = particleNeighbors[i][n];
                FluidParticle other = particles[neighbor.index];

                Vector2 rij = other.position - p.position;
                float distanceSqr = rij.sqrMagnitude;
                if (distanceSqr <= MinDistanceSqr)
                    continue;

                float displacementAmount =
                    neighbor.q * (p.pressure + other.pressure) +
                    neighbor.q2 * (p.pressureNear + other.pressureNear);

                pressureDisplacement += rij / Mathf.Sqrt(distanceSqr) * displacementAmount;
            }

            p.force -= pressureDisplacement;
            particles[i] = p;
        }
    }

    private void ApplySurfaceTension()
    {
        if (surfaceTension <= 0.0f)
            return;

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            List<Neighbor> neighbors = particleNeighbors[i];

            if (neighbors.Count == 0)
                continue;

            // Compute weighted average neighbor position (cohesion target)
            Vector2 cohesionCenter = Vector2.zero;
            float weightSum = 0.0f;

            for (int n = 0; n < neighbors.Count; n++)
            {
                Neighbor neighbor = neighbors[n];
                float weight = neighbor.q2; // Closer neighbors pull stronger
                cohesionCenter += particles[neighbor.index].position * weight;
                weightSum += weight;
            }

            if (weightSum > 0.0001f)
            {
                cohesionCenter /= weightSum;

                // Pull particle toward the average — this is the cohesion force
                Vector2 toCenter = cohesionCenter - p.position;
                p.force += toCenter * surfaceTension;
            }

            particles[i] = p;
        }
    }

    private void ApplyViscosity()
    {
        if (viscosity <= 0.0f && viscosityBeta <= 0.0f)
            return;

        float supportRadius = SupportRadius;

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];

            for (int n = 0; n < particleNeighbors[i].Count; n++)
            {
                Neighbor neighbor = particleNeighbors[i][n];
                FluidParticle other = particles[neighbor.index];

                Vector2 rij = other.position - p.position;
                float distanceSqr = rij.sqrMagnitude;
                if (distanceSqr <= MinDistanceSqr)
                    continue;

                float distance = Mathf.Sqrt(distanceSqr);
                float q = distance / supportRadius;
                Vector2 direction = rij / distance;
                float velocityTowardNeighbor = Vector2.Dot(p.velocity - other.velocity, direction);

                if (velocityTowardNeighbor > 0.0f)
                {
                    float impulseAmount =
                        (1.0f - q) *
                        (other.sigma * velocityTowardNeighbor + other.beta * velocityTowardNeighbor * velocityTowardNeighbor);

                    p.velocity -= direction * impulseAmount * 0.5f;
                }
            }

            particles[i] = p;
        }
    }

    private void CollectNeighborCandidates(Vector2 position, List<int> results)
    {
        results.Clear();

        float invCellSize = 1.0f / SupportRadius;
        Vector2Int cell = Discretize(position, invCellSize);

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector2Int neighborCell = new Vector2Int(cell.x + x, cell.y + y);
                if (spatialHash.TryGetValue(neighborCell, out List<int> bucket))
                    results.AddRange(bucket);
            }
        }
    }

    private static Vector2Int Discretize(Vector2 position, float invCellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x * invCellSize),
            Mathf.FloorToInt(position.y * invCellSize));
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (i < particleVisuals.Count && particleVisuals[i] != null)
                particleVisuals[i].position = particles[i].position;

            if (i < particleRenderers.Count && particleRenderers[i] != null)
            {
                float t = Mathf.InverseLerp(restDensity * 0.5f, restDensity * 2.0f, particles[i].density);
                particleRenderers[i].color = Color.Lerp(Color.cyan, Color.blue, t);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (drawBounds)
        {
            Gizmos.color = Color.white;
            Vector2 center = (boundsMin + boundsMax) * 0.5f;
            Vector2 size = boundsMax - boundsMin;
            Gizmos.DrawWireCube(center, size);
        }

        if (!drawParticles || particles == null)
            return;

        for (int i = 0; i < particles.Count; i++)
        {
            float t = Mathf.InverseLerp(restDensity * 0.5f, restDensity * 2.0f, particles[i].density);
            Gizmos.color = Color.Lerp(Color.cyan, Color.blue, t);
            Gizmos.DrawSphere(particles[i].position, gizmoParticleRadius);
        }
    }

    public void AddForce(int index, Vector2 force)
    {
        if (index < 0 || index >= particles.Count)
            return;

        FluidParticle p = particles[index];
        p.force += force;
        particles[index] = p;
    }

    public void RemoveParticle(int index)
    {
        if (index < 0 || index >= particles.Count)
            return;

        if (index < particleVisuals.Count && particleVisuals[index] != null)
            Destroy(particleVisuals[index].gameObject);

        int last = particles.Count - 1;

        // Swap-remove for O(1) deletion
        particles[index] = particles[last];
        particles.RemoveAt(last);

        if (particleVisuals.Count > 0)
        {
            particleVisuals[index] = particleVisuals[particleVisuals.Count - 1];
            particleVisuals.RemoveAt(particleVisuals.Count - 1);
        }
        if (particleRenderers.Count > 0)
        {
            particleRenderers[index] = particleRenderers[particleRenderers.Count - 1];
            particleRenderers.RemoveAt(particleRenderers.Count - 1);
        }
        if (index < preStepPositions.Count && preStepPositions.Count > 0)
        {
            preStepPositions[index] = preStepPositions[preStepPositions.Count - 1];
            preStepPositions.RemoveAt(preStepPositions.Count - 1);
        }

        // Force neighbor storage rebuild next step
        particleNeighbors = new List<Neighbor>[0];
    }

    public void ResetStreamTimer()
    {
        streamTimer = 0.0f;
        spawnAccumulator = 0.0f;
    }

    public void RespawnParticles()
    {
        for (int i = 0; i < particleVisuals.Count; i++)
        {
            if (particleVisuals[i] != null)
                Destroy(particleVisuals[i].gameObject);
        }

        particles.Clear();
        particleVisuals.Clear();
        particleRenderers.Clear();
        particleNeighbors = new List<Neighbor>[0];
        spawnAccumulator = 0.0f;
        streamTimer = 0.0f;

        if (spawnInitialBlock)
            SpawnBlock();
    }

    public void ClearParticles()
    {
        // Stop spawning new particles
        streamEnabled = false;

        // Remove every particle via the same swap-remove path that
        // CullOffscreenParticles uses. Iterate backwards because
        // RemoveParticle shrinks the list from the end.
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            RemoveParticle(i);
        }

        // Safety: force-clear any stragglers in case lists got desynced
        particles.Clear();
        particleVisuals.Clear();
        particleRenderers.Clear();
        particleNeighbors = new List<Neighbor>[0];

        // Reset simulation state
        spawnAccumulator = 0.0f;
        streamTimer = 0.0f;
        preStepPositions.Clear();
    }
}
