using UnityEngine;

public class PBDRopeSimulation : MonoBehaviour
{
    [Header("Geometria & Massa")]
    public int segmentCount = 10;
    public float segmentSpacing = 0.5f;
    public float sphereRadius = 0.15f;
    public float particleMass = 1.0f;

    [Header("Solutore PBD")]
    [Range(1, 50)]
    public int solverIterations = 10;
    [Range(0f, 1f)]
    public float stiffness = 1.0f;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    [Range(0f, 0.1f)]
    public float damping = 0.01f;

    [Header("Carico all'Estremità")]
    public float downwardForce = 50f;

    [Header("Visualizzazione")]
    public Color gizmoColor = Color.green;
    public bool showPreview = true;
    public bool renderMeshes = true;

    // Struttura dati particelle con flag isKinematic
    private struct Particle
    {
        public Vector3 position;
        public Vector3 predictedPosition;
        public Vector3 velocity;
        public float invMass;
        public bool isKinematic; // Se true, la particella non risponde a forze/vincoli
    }

    private struct DistanceConstraint
    {
        public int indexA;
        public int indexB;
        public float restLength;
    }

    private Particle[] particles;
    private DistanceConstraint[] constraints;
    private Transform[] visualSpheres;
    private LineRenderer lineRenderer;

    private void Start()
    {
        InitializeSimulation();
    }

    private void InitializeSimulation()
    {
        int particleCount = segmentCount + 1;
        particles = new Particle[particleCount];
        constraints = new DistanceConstraint[segmentCount];

        Vector3 origin = transform.position;

        // 1. Inizializzazione delle particelle
        for (int i = 0; i < particleCount; i++) // NOTA: parte da 1, non da 0, per evitare che la prima sfera venga generata nell'anchor point,
                                                // per allineare visivamente la catena alla versione RopeGenerator con PhysX di Unity
        {
            Vector3 pos = origin - new Vector3(0, i * segmentSpacing, 0);
            bool isFixed = (i == 0);

            particles[i] = new Particle
            {
                position = pos,
                predictedPosition = pos,
                velocity = Vector3.zero,
                isKinematic = isFixed,
                invMass = isFixed ? 0f : (1.0f / particleMass) // Massa infinita se cinematica
            };
        }

        // 2. Inizializzazione vincoli di distanza
        for (int i = 0; i < segmentCount; i++)
        {
            constraints[i] = new DistanceConstraint
            {
                indexA = i,
                indexB = i + 1,
                restLength = segmentSpacing
            };
        }

        // 3. Setup Visivo
        if (renderMeshes)
        {
            visualSpheres = new Transform[particleCount];
            for (int i = 1; i < particleCount; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"PBD_Particle_{i}";
                sphere.transform.localScale = Vector3.one * (sphereRadius * 2f);
                sphere.transform.parent = transform;
                Destroy(sphere.GetComponent<Collider>());
                visualSpheres[i] = sphere.transform;
            }
        }

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = particleCount;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = gizmoColor;
        lineRenderer.endColor = gizmoColor;
    }

    private void FixedUpdate()
    {
        if (particles == null) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        // Step 1: Integrazione cinematica e predizione
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].invMass == 0f) 
            {
                // Rimane incollata alla posizione iniziale o a quella dell'ancora
                particles[i].predictedPosition = transform.position;
                particles[i].velocity = Vector3.zero;
                continue;
            }

            Vector3 acceleration = gravity;
            if (i == particles.Length - 1 && downwardForce > 0f)
            {
                acceleration += (Vector3.down * downwardForce) * particles[i].invMass;
            }

            particles[i].velocity += acceleration * dt;
            particles[i].velocity *= (1.0f - damping);
            particles[i].predictedPosition = particles[i].position + particles[i].velocity * dt;
        }

        // Step 2: Risoluzione vincoli geometrici (Gauss-Seidel)
        for (int iter = 0; iter < solverIterations; iter++)
        {
            for (int i = 0; i < constraints.Length; i++)
            {
                DistanceConstraint c = constraints[i];
                ref Particle pA = ref particles[c.indexA];
                ref Particle pB = ref particles[c.indexB];

                Vector3 delta = pA.predictedPosition - pB.predictedPosition;
                float currentDistance = delta.magnitude;

                if (currentDistance > 1e-6f)
                {
                    float error = currentDistance - c.restLength;
                    Vector3 direction = delta / currentDistance;

                    float wSum = pA.invMass + pB.invMass;
                    if (wSum > 0f)
                    {
                        Vector3 correction = (error / wSum) * direction * stiffness;

                        // Se pA è cinematica (invMass = 0), correction * invMass sarà zero,
                        // quindi l'intera correzione graverà su pB (e viceversa).
                        pA.predictedPosition -= correction * pA.invMass;
                        pB.predictedPosition += correction * pB.invMass;
                    }
                }
            }
        }

        // Step 3: Aggiornamento finale posizioni e velocità
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].invMass == 0f)
            {
                particles[i].position = transform.position;
                particles[i].velocity = Vector3.zero;
                continue;
            }

            particles[i].velocity = (particles[i].predictedPosition - particles[i].position) / dt;
            particles[i].position = particles[i].predictedPosition;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (visualSpheres != null && visualSpheres[i] != null)
            {
                visualSpheres[i].position = particles[i].position;
            }
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(i, particles[i].position);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showPreview || Application.isPlaying) return;

        Gizmos.color = gizmoColor;
        Vector3 startPos = transform.position;
        Gizmos.DrawSphere(startPos, sphereRadius * 0.5f);

        Vector3 prevPos = startPos;
        for (int i = 1; i <= segmentCount; i++)
        {
            Vector3 currentPos = startPos - new Vector3(0, i * segmentSpacing, 0);
            Gizmos.DrawWireSphere(currentPos, sphereRadius);
            Gizmos.DrawLine(prevPos, currentPos);
            prevPos = currentPos;
        }

        if (downwardForce > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(prevPos, Vector3.down * Mathf.Clamp(downwardForce * 0.05f, 0.5f, 3f));
        }
    }
}