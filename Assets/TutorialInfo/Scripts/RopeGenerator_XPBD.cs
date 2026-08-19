// using UnityEngine;

// public class XPBDRopeSimulation : MonoBehaviour
// {
//     [Header("Geometria & Massa")]
//     public int segmentCount = 10;
//     public float segmentSpacing = 0.5f;
//     public float sphereRadius = 0.15f;
//     public float particleMass = 1.0f;

//     [Header("Solutore XPBD")]
//     [Tooltip("Numero di sotto-passi per FixedUpdate. XPBD converge meglio con più substep a bassa iterazione che con poche substep e molte iterazioni.")]
//     [Range(1, 20)]
//     public int substeps = 4;
//     [Tooltip("Iterazioni di risoluzione vincoli per ogni substep.")]
//     [Range(1, 10)]
//     public int iterationsPerSubstep = 1;
//     [Tooltip("Compliance del vincolo di distanza (inverso della rigidità, m/N). 0 = vincolo perfettamente rigido/inestensibile, indipendentemente da iterazioni e timestep. Valori > 0 rendono la corda elastica.")]
//     [Min(0f)]
//     public float compliance = 0f;
//     public Vector3 gravity = new Vector3(0, -9.81f, 0);
//     [Range(0f, 0.1f)]
//     public float damping = 0.01f;

//     [Header("Carico all'Estremità")]
//     public float downwardForce = 50f;

//     [Header("Visualizzazione")]
//     public Color gizmoColor = Color.magenta;
//     public bool showPreview = true;
//     public bool renderMeshes = true;

//     // Struttura dati particelle con flag isKinematic
//     private struct Particle
//     {
//         public Vector3 position;
//         public Vector3 predictedPosition;
//         public Vector3 velocity;
//         public float invMass;
//         public bool isKinematic; // Se true, la particella non risponde a forze/vincoli
//     }

//     private struct DistanceConstraint
//     {
//         public int indexA;
//         public int indexB;
//         public float restLength;
//     }

//     private Particle[] particles;
//     private DistanceConstraint[] constraints;
//     private float[] lambdas; // Moltiplicatori di Lagrange XPBD, uno per vincolo
//     private Transform[] visualSpheres;
//     private LineRenderer lineRenderer;

//     // Posizione dell'ancora congelata all'inizializzazione: la corda non risente
//     // di eventuali movimenti successivi del Transform (es. per un Rigidbody non kinematico).
//     private Vector3 anchorWorldPosition;

//     private void Start()
//     {
//         InitializeSimulation();
//     }

//     private void InitializeSimulation()
//     {
//         anchorWorldPosition = transform.position;

//         int particleCount = segmentCount + 1;
//         particles = new Particle[particleCount];
//         constraints = new DistanceConstraint[segmentCount];
//         lambdas = new float[segmentCount];

//         // 1. Inizializzazione delle particelle
//         for (int i = 0; i < particleCount; i++)
//         {
//             Vector3 pos = anchorWorldPosition - new Vector3(0, i * segmentSpacing, 0);
//             bool isFixed = (i == 0);

//             particles[i] = new Particle
//             {
//                 position = pos,
//                 predictedPosition = pos,
//                 velocity = Vector3.zero,
//                 isKinematic = isFixed,
//                 invMass = isFixed ? 0f : (1.0f / particleMass) // Massa infinita se cinematica
//             };
//         }

//         // 2. Inizializzazione vincoli di distanza
//         for (int i = 0; i < segmentCount; i++)
//         {
//             constraints[i] = new DistanceConstraint
//             {
//                 indexA = i,
//                 indexB = i + 1,
//                 restLength = segmentSpacing
//             };
//         }

//         // 3. Setup Visivo
//         // NOTA: parte da i = 1, non da 0. La particella 0 e' l'ancora (equivalente
//         // al Rigidbody kinematico invisibile della versione PhysX) e non viene renderizzata.
//         if (renderMeshes)
//         {
//             visualSpheres = new Transform[particleCount];
//             for (int i = 1; i < particleCount; i++)
//             {
//                 GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                 sphere.name = $"XPBD_Particle_{i}";
//                 sphere.transform.localScale = Vector3.one * (sphereRadius * 2f);
//                 sphere.transform.parent = transform;
//                 Destroy(sphere.GetComponent<Collider>());
//                 visualSpheres[i] = sphere.transform;
//             }
//         }

//         lineRenderer = gameObject.AddComponent<LineRenderer>();
//         lineRenderer.startWidth = 0.05f;
//         lineRenderer.endWidth = 0.05f;
//         lineRenderer.positionCount = particleCount;
//         lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//         lineRenderer.startColor = gizmoColor;
//         lineRenderer.endColor = gizmoColor;
//     }

//     private void FixedUpdate()
//     {
//         if (particles == null) return;

//         float dt = Time.fixedDeltaTime;
//         if (dt <= 0f) return;

//         int sub = Mathf.Max(1, substeps);
//         float subDt = dt / sub;

//         for (int s = 0; s < sub; s++)
//         {
//             XPBDSubStep(subDt);
//         }

//         UpdateVisuals();
//     }

//     private void XPBDSubStep(float dt)
//     {
//         // Step 1: Integrazione cinematica e predizione
//         for (int i = 0; i < particles.Length; i++)
//         {
//             if (particles[i].invMass == 0f)
//             {
//                 particles[i].predictedPosition = anchorWorldPosition;
//                 particles[i].velocity = Vector3.zero;
//                 continue;
//             }

//             Vector3 acceleration = gravity;
//             if (i == particles.Length - 1 && downwardForce > 0f)
//             {
//                 acceleration += (Vector3.down * downwardForce) * particles[i].invMass;
//             }

//             particles[i].velocity += acceleration * dt;
//             particles[i].velocity *= (1.0f - damping);
//             particles[i].predictedPosition = particles[i].position + particles[i].velocity * dt;
//         }

//         // Step 2: Reset dei moltiplicatori di Lagrange (obbligatorio ad ogni substep, per definizione XPBD)
//         for (int i = 0; i < lambdas.Length; i++)
//         {
//             lambdas[i] = 0f;
//         }

//         // Step 3: Risoluzione vincoli XPBD (Gauss-Seidel con compliance)
//         // alphaTilde = compliance / dt^2, come da Müller et al. 2016, "XPBD: Position-Based
//         // Simulation of Compliant Constrained Dynamics"
//         float alphaTilde = compliance / (dt * dt);

//         for (int iter = 0; iter < iterationsPerSubstep; iter++)
//         {
//             for (int i = 0; i < constraints.Length; i++)
//             {
//                 DistanceConstraint c = constraints[i];
//                 ref Particle pA = ref particles[c.indexA];
//                 ref Particle pB = ref particles[c.indexB];

//                 Vector3 delta = pA.predictedPosition - pB.predictedPosition;
//                 float currentDistance = delta.magnitude;

//                 if (currentDistance <= 1e-6f) continue;

//                 float wSum = pA.invMass + pB.invMass;
//                 float denom = wSum + alphaTilde;
//                 if (denom <= 1e-9f) continue;

//                 float constraintValue = currentDistance - c.restLength; // C(x)
//                 Vector3 gradientDir = delta / currentDistance;          // n

//                 // Delta lambda secondo la formula XPBD
//                 float deltaLambda = (-constraintValue - alphaTilde * lambdas[i]) / denom;
//                 lambdas[i] += deltaLambda;

//                 Vector3 correction = deltaLambda * gradientDir;

//                 // Se pA e' cinematica (invMass = 0), correction * invMass sara' zero,
//                 // quindi l'intera correzione gravera' su pB (e viceversa).
//                 pA.predictedPosition += pA.invMass * correction;
//                 pB.predictedPosition -= pB.invMass * correction;
//             }
//         }

//         // Step 4: Aggiornamento finale posizioni e velocità
//         for (int i = 0; i < particles.Length; i++)
//         {
//             if (particles[i].invMass == 0f)
//             {
//                 particles[i].position = anchorWorldPosition;
//                 particles[i].velocity = Vector3.zero;
//                 continue;
//             }

//             particles[i].velocity = (particles[i].predictedPosition - particles[i].position) / dt;
//             particles[i].position = particles[i].predictedPosition;
//         }
//     }

//     private void UpdateVisuals()
//     {
//         for (int i = 0; i < particles.Length; i++)
//         {
//             if (visualSpheres != null && visualSpheres[i] != null)
//             {
//                 visualSpheres[i].position = particles[i].position;
//             }
//             if (lineRenderer != null)
//             {
//                 lineRenderer.SetPosition(i, particles[i].position);
//             }
//         }
//     }

//     private void OnDrawGizmos()
//     {
//         if (!showPreview || Application.isPlaying) return;

//         Gizmos.color = gizmoColor;
//         Vector3 startPos = transform.position;
//         Gizmos.DrawSphere(startPos, sphereRadius * 0.5f);

//         Vector3 prevPos = startPos;
//         for (int i = 1; i <= segmentCount; i++)
//         {
//             Vector3 currentPos = startPos - new Vector3(0, i * segmentSpacing, 0);
//             Gizmos.DrawWireSphere(currentPos, sphereRadius);
//             Gizmos.DrawLine(prevPos, currentPos);
//             prevPos = currentPos;
//         }

//         if (downwardForce > 0f)
//         {
//             Gizmos.color = Color.red;
//             Gizmos.DrawRay(prevPos, Vector3.down * Mathf.Clamp(downwardForce * 0.05f, 0.5f, 3f));
//         }
//     }
// }

using UnityEngine;

public class XPBDRopeSimulation : MonoBehaviour
{
    [Header("Geometria & Massa")]
    public int segmentCount = 10;
    public float segmentSpacing = 0.5f;
    public float sphereRadius = 0.15f;
    public float particleMass = 1.0f;

    [Header("Solutore XPBD")]
    [Tooltip("Numero di sotto-passi per FixedUpdate. XPBD converge meglio con più substep a bassa iterazione che con poche substep e molte iterazioni.")]
    [Range(1, 20)]
    public int substeps = 4;
    [Tooltip("Iterazioni di risoluzione vincoli per ogni substep.")]
    [Range(1, 10)]
    public int iterationsPerSubstep = 1;
    [Tooltip("Compliance del vincolo di distanza (inverso della rigidità, m/N). 0 = vincolo perfettamente rigido/inestensibile, indipendentemente da iterazioni e timestep. Valori > 0 rendono la corda elastica.")]
    [Min(0f)]
    public float compliance = 0f;
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
    private float[] lambdas; // Moltiplicatori di Lagrange XPBD, uno per vincolo
    private Transform[] visualSpheres;
    private LineRenderer lineRenderer;

    // Posizione dell'ancora congelata all'inizializzazione: la corda non risente
    // di eventuali movimenti successivi del Transform (es. per un Rigidbody non kinematico).
    private Vector3 anchorWorldPosition;

    private void Start()
    {
        InitializeSimulation();
    }

    private void InitializeSimulation()
    {
        anchorWorldPosition = transform.position;

        int particleCount = segmentCount + 1;
        particles = new Particle[particleCount];
        constraints = new DistanceConstraint[segmentCount];
        lambdas = new float[segmentCount];

        // 1. Inizializzazione delle particelle
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 pos = anchorWorldPosition - new Vector3(0, i * segmentSpacing, 0);
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
        // NOTA: parte da i = 1, non da 0. La particella 0 e' l'ancora (equivalente
        // al Rigidbody kinematico invisibile della versione PhysX) e non viene renderizzata.
        if (renderMeshes)
        {
            visualSpheres = new Transform[particleCount];
            for (int i = 1; i < particleCount; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"XPBD_Particle_{i}";
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

        int sub = Mathf.Max(1, substeps);
        float subDt = dt / sub;

        for (int s = 0; s < sub; s++)
        {
            XPBDSubStep(subDt);
        }

        UpdateVisuals();
    }

    private void XPBDSubStep(float dt)
    {
        // Step 1: Integrazione cinematica e predizione
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].invMass == 0f)
            {
                particles[i].predictedPosition = anchorWorldPosition;
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

        // Step 2: Reset dei moltiplicatori di Lagrange (obbligatorio ad ogni substep, per definizione XPBD)
        for (int i = 0; i < lambdas.Length; i++)
        {
            lambdas[i] = 0f;
        }

        // Step 3: Risoluzione vincoli XPBD (Gauss-Seidel con compliance)
        // alphaTilde = compliance / dt^2, come da Müller et al. 2016, "XPBD: Position-Based
        // Simulation of Compliant Constrained Dynamics"
        float alphaTilde = compliance / (dt * dt);

        // Forward-backward sweep: le iterazioni pari scandiscono la catena dall'ancora
        // verso la punta (0 -> N), quelle dispari in senso inverso (N -> 0). Questo permette
        // alla correzione di propagarsi in entrambe le direzioni nello stesso ciclo di iterazioni,
        // facendo convergere la catena molto più rapidamente a parità di costo computazionale
        // rispetto a uno scan sempre nella stessa direzione (il motivo per cui, con una sola
        // direzione, l'ultimo vincolo della catena è quello che converge peggio).
        for (int iter = 0; iter < iterationsPerSubstep; iter++)
        {
            bool forward = (iter % 2 == 0);
            int start = forward ? 0 : constraints.Length - 1;
            int end = forward ? constraints.Length : -1;
            int step = forward ? 1 : -1;

            for (int i = start; i != end; i += step)
            {
                SolveDistanceConstraint(i, alphaTilde);
            }
        }

        // Step 4: Aggiornamento finale posizioni e velocità
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].invMass == 0f)
            {
                particles[i].position = anchorWorldPosition;
                particles[i].velocity = Vector3.zero;
                continue;
            }

            particles[i].velocity = (particles[i].predictedPosition - particles[i].position) / dt;
            particles[i].position = particles[i].predictedPosition;
        }
    }

    // Risolve un singolo vincolo di distanza secondo la formulazione XPBD.
    // Estratto in un metodo a parte cosi' puo' essere richiamato sia in avanti che
    // all'indietro dal forward-backward sweep senza duplicare la logica.
    private void SolveDistanceConstraint(int constraintIndex, float alphaTilde)
    {
        DistanceConstraint c = constraints[constraintIndex];
        ref Particle pA = ref particles[c.indexA];
        ref Particle pB = ref particles[c.indexB];

        Vector3 delta = pA.predictedPosition - pB.predictedPosition;
        float currentDistance = delta.magnitude;

        if (currentDistance <= 1e-6f) return;

        float wSum = pA.invMass + pB.invMass;
        float denom = wSum + alphaTilde;
        if (denom <= 1e-9f) return;

        float constraintValue = currentDistance - c.restLength; // C(x)
        Vector3 gradientDir = delta / currentDistance;          // n

        // Delta lambda secondo la formula XPBD
        float deltaLambda = (-constraintValue - alphaTilde * lambdas[constraintIndex]) / denom;
        lambdas[constraintIndex] += deltaLambda;

        Vector3 correction = deltaLambda * gradientDir;

        // Se pA e' cinematica (invMass = 0), correction * invMass sara' zero,
        // quindi l'intera correzione gravera' su pB (e viceversa).
        pA.predictedPosition += pA.invMass * correction;
        pB.predictedPosition -= pB.invMass * correction;
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