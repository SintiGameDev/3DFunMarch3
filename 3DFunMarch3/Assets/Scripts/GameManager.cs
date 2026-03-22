using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    [Header("Timer")]
    [SerializeField] private float rundenDauer = 60f;

    [Header("Neustart")]
    [SerializeField] private float neustartVerzoegerung = 5f;
    [SerializeField] private Transform[] spawnPunkte;

    // Netzwerk-synchronisierte Werte
    public NetworkVariable<float> VerbleibendeSekunden = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> GewinnerId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> RundeAktiv = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public static GameManager Singleton { get; private set; }

    void Awake()
    {
        Singleton = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        VerbleibendeSekunden.Value = rundenDauer;
        GewinnerId.Value           = ulong.MaxValue;
        RundeAktiv.Value           = true;
    }

    void Update()
    {
        if (!IsServer) return;
        if (!RundeAktiv.Value) return;

        VerbleibendeSekunden.Value -= Time.deltaTime;

        if (VerbleibendeSekunden.Value <= 0f)
        {
            VerbleibendeSekunden.Value = 0f;
            RundeBeenden();
        }
    }

    private void RundeBeenden()
    {
        RundeAktiv.Value = false;

        // Spieler mit hoechster Y-Position ermitteln
        ulong gewinnerId    = ulong.MaxValue;
        float hoechsteHoehe = float.MinValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            var spielerObjekt = client.Value.PlayerObject;
            if (spielerObjekt == null) continue;

            float hoehe = spielerObjekt.transform.position.y;
            if (hoehe > hoechsteHoehe)
            {
                hoechsteHoehe = hoehe;
                gewinnerId    = client.Key;
            }
        }

        GewinnerId.Value = gewinnerId;
        Debug.Log("[GameManager] Gewinner: Client " + gewinnerId
                  + " mit Hoehe " + hoechsteHoehe.ToString("F1") + "m");

        StartCoroutine(NeustartCoroutine());
    }

    private IEnumerator NeustartCoroutine()
    {
        yield return new WaitForSeconds(neustartVerzoegerung);
        RundeNeustartenClientRpc();
        RundeNeustarten();
    }

    private void RundeNeustarten()
    {
        // Spieler an Spawn-Punkte versetzen
        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            var spielerObjekt = client.Value.PlayerObject;
            if (spielerObjekt == null) continue;

            if (spawnPunkte != null && spawnPunkte.Length > 0)
            {
                Transform punkt = spawnPunkte[index % spawnPunkte.Length];
                spielerObjekt.transform.position = punkt.position;
                spielerObjekt.transform.rotation = punkt.rotation;
            }

            index++;
        }

        // Alle FOs despawnen
        var alleNetzwerkObjekte = FindObjectsByType<FallingObstacle>(FindObjectsSortMode.None);
        foreach (var fo in alleNetzwerkObjekte)
        {
            if (fo.NetworkObject.IsSpawned)
                fo.NetworkObject.Despawn(true);
        }

        // Timer und Status zuruecksetzen
        VerbleibendeSekunden.Value = rundenDauer;
        GewinnerId.Value           = ulong.MaxValue;
        RundeAktiv.Value           = true;

        Debug.Log("[GameManager] Runde neu gestartet.");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RundeNeustartenClientRpc()
    {
        Debug.Log("[GameManager] Neustart wird vorbereitet...");
    }
}
