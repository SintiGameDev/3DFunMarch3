using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerAttack : NetworkBehaviour
{
    [Header("Schubsen Einstellungen")]
    [SerializeField] private float schubReichweite = 3.5f;
    [SerializeField] private float schubKraft = 15f;
    [SerializeField] private float angriffCooldown = 0.5f;
    [SerializeField] private LayerMask spielerLayer; // Layer für Spieler setzen!

    private Camera spielerKamera;
    private float naechsterAngriffZeit = 0f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        spielerKamera = GetComponentInChildren<Camera>();
        if (spielerKamera == null)
            Debug.LogError("[PlayerAttack] Keine Kamera im Spieler gefunden.");
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButtonDown(0) && Time.time >= naechsterAngriffZeit)
        {
            naechsterAngriffZeit = Time.time + angriffCooldown;
            SchubsenVersuchen();
        }
    }

    private void SchubsenVersuchen()
    {
        if (spielerKamera == null) return;

        Ray ray = spielerKamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // Mitte des Bildschirms
        RaycastHit hit;

        // Visuelles Feedback für den Angreifer (nur im Editor sichtbar)
        Debug.DrawRay(ray.origin, ray.direction * schubReichweite, Color.green, 0.5f);

        if (Physics.Raycast(ray, out hit, schubReichweite, spielerLayer))
        {
            // Haben wir einen NetworkObject getroffen?
            if (hit.collider.TryGetComponent<NetworkObject>(out var targetNetObj))
            {
                // Sicherstellen, dass wir uns nicht selbst schubsen
                if (targetNetObj.OwnerClientId == OwnerClientId) return;

                Debug.Log($"[PlayerAttack] Getroffen: {targetNetObj.name} (ID: {targetNetObj.OwnerClientId})");

                // Berechnung der Stoßrichtung (vom Angreifer zum Opfer)
                Vector3 schubRichtung = hit.collider.transform.position - transform.position;
                schubRichtung.y = 0; // Kein Stoß nach oben/unten
                schubRichtung.Normalize();

                // Server informieren
                SchubsenServerRpc(targetNetObj.OwnerClientId, schubRichtung);
            }
        }
    }

    [ServerRpc]
    private void SchubsenServerRpc(ulong targetClientId, Vector3 richtung)
    {
        // 1. Validierung auf dem Server (Optional: Distanz nochmal prüfen)
        Debug.Log($"[Server] Schubsen Anfrage von {OwnerClientId} gegen {targetClientId}");

        // 2. Das Opfer-Objekt in der Welt finden
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var targetClient))
        {
            var targetPlayer = targetClient.PlayerObject;
            if (targetPlayer != null && targetPlayer.TryGetComponent<PlayerMovement>(out var movement))
            {
                // 3. Den Stoßbefehl an das Opfer senden
                // Wir übergeben die berechnete Kraft direkt
                movement.EmpfangeSchubClientRpc(richtung * schubKraft);
            }
        }
    }
}