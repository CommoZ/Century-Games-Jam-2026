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

    [Header("Physics")]
    [SerializeField] private float particleMass = 1.0f;
    [SerializeField] private float smoothingRadius = 0.35f;
    [SerializeField] private float restDensity = 3.0f;
    [SerializeField] private float stiffness = 0.00028f;
    [SerializeField] private float nearPressureMultiplier = 10.0f;
    [SerializeField] private float viscosity = 3.0f;
    [SerializeField] private float viscosityBeta = 4.0f;
    [SerializeField] private Vector2 gravity = new Vector2(0.0f, -0.0007f);
    [SerializeField] private float maxAccel = 0.29f;
    [SerializeField] private int simulationStepsPerFixedUpdate = 1;

    [Header("Bounds")]
    [SerializeField] private Vector2 boundsMin = new Vector2(-4.0f, 0.0f);
    [SerializeField] private Vector2 boundsMax = new Vector2(4.0f, 8.0f);
    [SerializeField] private float boundaryDamping = 0.125f;
    [SerializeField] private bool useTopBounds = false;

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

    private float spawnAccumulator;

    public int ParticleCount => particles.Count;
    public List<FluidParticle> Particles => particles;

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

        UpdateVisuals();
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
            particleRenderers.Add(obj.GetComponent<SpriteRenderer>());
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

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];

            p.position += p.force;
            p.force = gravity;

            p.velocity = p.position - p.positionOld;
            if (maxVelocity > 0.0f && p.velocity.sqrMagnitude > maxVelocitySqr)
                p.velocity *= 0.5f;

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

            // 1) Raycast from old position to new position to catch tunneling
            Vector2 moveDir = p.position - p.positionOld;
            float moveDist = moveDir.magnitude;
            if (moveDist > 0.0001f)
            {
                int rayHits = Physics2D.Raycast(p.positionOld, moveDir / moveDist, filter, rayResults, moveDist);
                if (rayHits > 0)
                {
                    RaycastHit2D rayHit = rayResults[0];
                    p.position = rayHit.point + rayHit.normal * radius;

                    float velIntoWall = Vector2.Dot(p.velocity, -rayHit.normal);
                    if (velIntoWall > 0.0f)
                        p.velocity += rayHit.normal * velIntoWall;

                    p.positionOld = p.position - p.velocity;
                    resolved = true;
                }
            }

            // 2) Overlap check for particles already inside or near a collider
            if (!resolved)
            {
                int hitCount = Physics2D.OverlapCircle(p.position, radius, filter, overlapResults);
                if (hitCount > 0)
                {
                    Collider2D hit = overlapResults[0];
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

        if (spawnInitialBlock)
            SpawnBlock();
    }
}
