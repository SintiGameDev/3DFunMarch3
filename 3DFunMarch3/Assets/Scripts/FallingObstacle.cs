using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class FallingObstacle : NetworkBehaviour
{
    [Header("Physik")]
    [SerializeField] private float masse            = 10f;
    [SerializeField] private float luftwiderstand   = 0.2f;

    [Header("Rotation")]
    [SerializeField] private float minRotationsGeschwindigkeit = 5f;
    [SerializeField] private float maxRotationsGeschwindigkeit = 20f;

    [Header("Aufprall")]
    [SerializeField] private float aufprallSchwelle = 1f;

    private NetworkVariable<bool> istGelandet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;
    private Vector3 eingefrorenePosition;
    private Quaternion eingefroreneRotation;

    public bool IsLanded => istGelandet.Value;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (IsServer)
        {
            rb.isKinematic = false;
            rb.mass        = masse;
            rb.linearDamping      = luftwiderstand;

            Vector3 zufaelligeTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * Random.Range(minRotationsGeschwindigkeit, maxRotationsGeschwindigkeit);

            rb.AddTorque(zufaelligeTorque, ForceMode.VelocityChange);
        }
        else
        {
            rb.isKinematic = true;
        }

        istGelandet.OnValueChanged += OnIsGelandetChanged;
    }

    public override void OnNetworkDespawn()
    {
        istGelandet.OnValueChanged -= OnIsGelandetChanged;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (istGelandet.Value) return;

        bool trifftPlattform    = collision.gameObject.CompareTag("Platform");
        bool trifftGelandetesFO = false;

        var anderesFO = collision.gameObject.GetComponent<FallingObstacle>();
        if (anderesFO != null && anderesFO.IsLanded)
            trifftGelandetesFO = true;

        if ((trifftPlattform || trifftGelandetesFO)
            && collision.relativeVelocity.magnitude >= aufprallSchwelle)
        {
            // Position und Rotation zum Einfrierzeitpunkt merken
            eingefrorenePosition = transform.position;
            eingefroreneRotation = transform.rotation;
            istGelandet.Value    = true;
        }
    }

    private void OnIsGelandetChanged(bool vorher, bool jetzt)
    {
        if (!jetzt) return;

        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
            rb.constraints     = RigidbodyConstraints.FreezeAll;
        }

        // Position und Rotation hart fixieren
        if (eingefrorenePosition != Vector3.zero)
        {
            transform.position = eingefrorenePosition;
            transform.rotation = eingefroreneRotation;
        }

        Debug.Log("[FallingObstacle] " + gameObject.name + " gelandet und eingefroren.");
    }

    // LateUpdate sicherstellt dass Objekt nicht wegrutscht
    void LateUpdate()
    {
        if (!istGelandet.Value) return;

        if (eingefrorenePosition != Vector3.zero)
        {
            transform.position = eingefrorenePosition;
            transform.rotation = eingefroreneRotation;
        }
    }
}
