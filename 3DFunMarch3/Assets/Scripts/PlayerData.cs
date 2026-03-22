using UnityEngine;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Synchronisiert Spielername und Farbe ueber das Netzwerk.
/// Wird auf dem Player Prefab platziert.
/// </summary>
public class PlayerData : NetworkBehaviour
{
    [Header("World Space Label")]
    [SerializeField] private TextMeshPro nameLabel;
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 2.4f, 0f);

    // Netzwerk-synchronisierte Werte
    public NetworkVariable<Color> SpielerFarbe = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Name wird als FixedString uebertragen (max 64 Zeichen)
    public NetworkVariable<Unity.Collections.FixedString64Bytes> SpielerName =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            "Spieler",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Vordefinierte Farben (Primaer und Sekundaer)
    private static readonly Color[] verfuegbareFarben = new Color[]
    {
        new Color(0.38f, 0.40f, 0.95f),  // Indigo
        new Color(0.94f, 0.27f, 0.27f),  // Rot
        new Color(0.13f, 0.77f, 0.37f),  // Gruen
        new Color(0.99f, 0.76f, 0.08f),  // Gelb
        new Color(0.91f, 0.33f, 0.73f),  // Pink
        new Color(0.07f, 0.72f, 0.91f),  // Cyan
        new Color(0.98f, 0.50f, 0.15f),  // Orange
        new Color(0.60f, 0.40f, 0.95f),  // Violett
    };

    private static int naechsteFarbeIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Farbe zuweisen
            Color farbe = verfuegbareFarben[naechsteFarbeIndex % verfuegbareFarben.Length];
            naechsteFarbeIndex++;
            SpielerFarbe.Value = farbe;
        }

        // Auf Aenderungen reagieren
        SpielerName.OnValueChanged  += (_, _) => LabelAktualisieren();
        SpielerFarbe.OnValueChanged += (_, _) => LabelAktualisieren();

        LabelAktualisieren();

        // Eigenen Namen an Server senden
        if (IsOwner)
            NamenSetzenServerRpc(LobbyUIController.SpielerName);
    }

    public override void OnNetworkDespawn()
    {
        SpielerName.OnValueChanged  -= (_, _) => LabelAktualisieren();
        SpielerFarbe.OnValueChanged -= (_, _) => LabelAktualisieren();

        if (IsServer)
            naechsteFarbeIndex = Mathf.Max(0, naechsteFarbeIndex - 1);
    }

    [Rpc(SendTo.Server)]
    private void NamenSetzenServerRpc(string name)
    {
        string bereinigt = string.IsNullOrWhiteSpace(name) ? "Spieler" : name.Trim();
        SpielerName.Value = new Unity.Collections.FixedString64Bytes(bereinigt);
    }

    private void LabelAktualisieren()
    {
        if (nameLabel == null) return;

        nameLabel.text  = SpielerName.Value.ToString();
        nameLabel.color = SpielerFarbe.Value;

        // Eigenes Label ausblenden
        nameLabel.gameObject.SetActive(!IsOwner);
    }

    // Kamera-Referenz wird einmalig gefunden und gecacht
    private Camera lokalKamera;

    void LateUpdate()
    {
        if (nameLabel == null) return;

        if (lokalKamera == null || !lokalKamera.isActiveAndEnabled)
            lokalKamera = FindLokaleKamera();

        if (lokalKamera == null) return;

        // Nur Rotation anpassen - Position wird durch den Transform-Offset gehalten
        // LookRotation mit Kamera-Forward damit das Label flach zur Kamera zeigt
        nameLabel.transform.rotation = Quaternion.LookRotation(lokalKamera.transform.forward);
    }

    /// <summary>
    /// Sucht die aktive lokale Kamera des eigenen Spielers.
    /// Bevorzugt die Kamera die dem lokalen Owner-Spieler gehoert.
    /// </summary>
    private Camera FindLokaleKamera()
    {
        // Alle aktiven Kameras durchsuchen
        foreach (var cam in Camera.allCameras)
        {
            if (!cam.isActiveAndEnabled) continue;

            // Kamera die ein Kind eines NetworkObject-Owners ist bevorzugen
            var networkObj = cam.GetComponentInParent<Unity.Netcode.NetworkObject>();
            if (networkObj != null && networkObj.IsOwner)
                return cam;
        }

        // Fallback: erste aktive Kamera
        foreach (var cam in Camera.allCameras)
            if (cam.isActiveAndEnabled) return cam;

        return null;
    }

    // Oeffentliche Hilfsmethoden fuer andere Scripts
    public string GetName()  => SpielerName.Value.ToString();
    public Color  GetFarbe() => SpielerFarbe.Value;
}
