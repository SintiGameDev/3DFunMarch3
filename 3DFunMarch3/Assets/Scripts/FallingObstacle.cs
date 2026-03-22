using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class FallingObstacle : NetworkBehaviour
{
    [Header("Physik")]
    [SerializeField] private float masse            = 10f;
    [SerializeField] private float luftwiderstand   = 0.2f;

    [Header("Rotation")]
    [SerializeField] private float minRotationsGeschwindigkeit = 30f;
    [SerializeField] private float maxRotationsGeschwindigkeit = 120f;

    [Header("Aufprall")]
    [SerializeField] private float aufprallSchwelle = 1f;

    // Netzwerk-synchronisierter Landed-Status
    private NetworkVariable<bool> istGelandet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;

    public bool IsLanded => istGelandet.Value;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (IsServer)
        {
            rb.isKinematic  = false;
            rb.mass         = masse;
            rb.linearDamping       = luftwiderstand;

            // Zufaellige Rotationsgeschwindigkeit beim Spawnen setzen
            Vector3 zufaelligeTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * Random.Range(minRotationsGeschwindigkeit, maxRotationsGeschwindigkeit);

            rb.AddTorque(zufaelligeTorque, ForceMode.VelocityChange);
        }
        else
        {
            // Clients simulieren keine Physik
            rb.isKinematic = true;
        }

        // Auf Landungs-Aenderung reagieren
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

        // Pruefen ob Kollisionspartner die Plattform oder ein bereits gelandetes FO ist
        bool trifftPlattform = collision.gameObject.CompareTag("Platform");
        bool trifftGelandetesFO = false;

        var anderesFO = collision.gameObject.GetComponent<FallingObstacle>();
        if (anderesFO != null && anderesFO.IsLanded)
            trifftGelandetesFO = true;

        if ((trifftPlattform || trifftGelandetesFO)
            && collision.relativeVelocity.magnitude >= aufprallSchwelle)
        {
            istGelandet.Value = true;
        }
    }

    private void OnIsGelandetChanged(bool vorher, bool jetzt)
    {
        if (!jetzt) return;

        // Auf allen Clients und Host: Objekt einfrieren
        if (rb != null)
        {
            rb.linearVelocity   = Vector3.zero;
            rb.angularVelocity  = Vector3.zero;
            rb.isKinematic      = true;
        }

        Debug.Log("[FallingObstacle] " + gameObject.name + " gelandet.");
    }
}
