using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Wird automatisch auf gespawnten Falling Obstacle Prefabs hinzugefuegt.
/// Kuemmert sich um Physik-Setup und Landungs-Erkennung.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FallingObstacle : NetworkBehaviour
{
    [Header("Physik")]
    [SerializeField] private float masse          = 10f;
    [SerializeField] private float luftwiderstand = 0.5f;

    [Header("Aufprall")]
    [SerializeField] private float aufprallSchwelle = 2f;

    private Rigidbody rb;
    private bool istGelandet = false;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        // Physik nur auf dem Host simulieren
        // Clients empfangen Position per NetworkTransform
        rb.isKinematic = !IsServer;
        rb.mass = masse;
        rb.linearDamping = luftwiderstand;

        // Collider automatisch hinzufuegen falls keiner vorhanden
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            Debug.Log("[FallingObstacle] BoxCollider automatisch hinzugefuegt.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (istGelandet) return;

        // Nur bei genuegend Aufprallkraft als gelandet zaehlen
        if (collision.relativeVelocity.magnitude >= aufprallSchwelle)
        {
            istGelandet = true;
            LandungVerarbeitenClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void LandungVerarbeitenClientRpc()
    {
        // Rigidbody einfrieren - Objekt bleibt als Teil der Landschaft liegen
        if (rb != null)
        {
            rb.linearVelocity        = Vector3.zero;
            rb.angularVelocity  = Vector3.zero;
            rb.isKinematic      = true;
        }

        Debug.Log("[FallingObstacle] " + gameObject.name + " gelandet.");
    }
}
