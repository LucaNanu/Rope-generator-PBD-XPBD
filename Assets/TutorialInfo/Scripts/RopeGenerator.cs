using UnityEngine;

public class RopeGenerator : MonoBehaviour
{
    [Header("Parametri Corda")]
    public int segmentCount = 100;
    public float segmentSpacing = 0.05f;
    public float sphereRadius = 0.1f;
    public float segmentMass = 0.1f;

    [Header("Carico all'Estremità")]
    public float downwardForce = 50f; // Forza continua applicata verso il basso (N)

    private Rigidbody lastSegmentRb;

    [Header("Prefab o Primitiva")]
    public GameObject spherePrefab; // Opzionale: se null, crea primitive procedurali

    [Header("Visualizzazione Editor")]
    public Color gizmoColor = Color.cyan;
    public bool showPreview = true;

    private void Start()
    {
        GenerateRope();
    }

    private void FixedUpdate()
    {
        // Applica la forza continua verso il basso all'ultimo anello
        if (lastSegmentRb != null && downwardForce > 0f)
        {
            lastSegmentRb.AddForce(Vector3.down * downwardForce, ForceMode.Force);
        }
    }

    void GenerateRope()
    {
        // 1. Assicurati che l'Anchor abbia un Rigidbody cinematico
        Rigidbody anchorRb = GetComponent<Rigidbody>();
        if (anchorRb == null)
        {
            anchorRb = gameObject.AddComponent<Rigidbody>();
        }
        anchorRb.isKinematic = true;

        Rigidbody previousBody = anchorRb;

        // 2. Genera i segmenti in sequenza verso il basso
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 spawnPos = transform.position - new Vector3(0, (i + 1) * segmentSpacing, 0);

            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            segment.name = $"Sphere_{i}";
            segment.transform.position = spawnPos;
            segment.transform.localScale = Vector3.one * (sphereRadius * 2f);
            segment.transform.parent = transform;

            // Configurazione Rigidbody
            Rigidbody rb = segment.GetComponent<Rigidbody>();
            if (rb == null)
                rb = segment.AddComponent<Rigidbody>();
            rb.mass = segmentMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Configurazione ConfigurableJoint
            ConfigurableJoint joint = segment.AddComponent<ConfigurableJoint>();
            joint.connectedBody = previousBody;
            joint.autoConfigureConnectedAnchor = true;

            // Blocca la traslazione (mantiene costante la distanza nominale)
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Sblocca la rotazione 3D sferica completa
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            // Assegna il riferimento per il ciclo successivo
            previousBody = rb;

            if (i == segmentCount - 1)
            {
                lastSegmentRb = rb;
            }
        }
    }

    // Disegna l'anteprima nella Scene View senza avviare la simulazione
    private void OnDrawGizmos()
    {
        if (!showPreview) return;

        Gizmos.color = gizmoColor;
        Vector3 startPos = transform.position;

        // Punto di ancoraggio
        Gizmos.DrawSphere(startPos, sphereRadius * 0.5f);

        Vector3 prevPos = startPos;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 currentPos = startPos - new Vector3(0, (i + 1) * segmentSpacing, 0);

            // Disegna il segmento/sfera e la linea di collegamento
            Gizmos.DrawWireSphere(currentPos, sphereRadius);
            Gizmos.DrawLine(prevPos, currentPos);

            prevPos = currentPos;
        }

        // Freccia direzionale per indicare la forza applicata in fondo
        if (downwardForce > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(prevPos, Vector3.down * Mathf.Clamp(downwardForce * 0.05f, 0.5f, 3f));
        }
    }
}
