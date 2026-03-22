using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Leben & Respawn")]
    [SerializeField] private float todesHoehe = -10f; // Ab dieser Y-Koordinate stirbt der Spieler
    [SerializeField] private int startLeben = 3;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lebenTextUI;

    // Synchronisierte Variable. Server schreibt, alle lesen.
    private NetworkVariable<int> aktuelleLeben = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // UI initialisieren
        UpdateLebenUI(0, aktuelleLeben.Value);

        // Listener für zukünftige Änderungen registrieren
        aktuelleLeben.OnValueChanged += UpdateLebenUI;

        if (IsServer)
        {
            aktuelleLeben.Value = startLeben;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Listener sauber entfernen, um Memory Leaks zu vermeiden
        aktuelleLeben.OnValueChanged -= UpdateLebenUI;
    }

    private void UpdateLebenUI(int alterWert, int neuerWert)
    {
        // UI nur für den Besitzer (Owner) des Spielers aktualisieren
        if (IsOwner && lebenTextUI != null)
        {
            lebenTextUI.text = "Leben: " + neuerWert;
        }
    }

    private void Update()
    {
        // Nur der Server prüft die Todesbedingung, um Cheating zu verhindern
        if (!IsServer) return;

        // Fällt der Spieler unter die definierte Höhe?
        if (transform.position.y < todesHoehe)
        {
            Sterben();
        }
    }

    private void Sterben()
    {
        if (aktuelleLeben.Value > 0)
        {
            aktuelleLeben.Value--; // Server zieht ein Leben ab

            if (aktuelleLeben.Value > 0)
            {
                // RPC an den Client senden, damit dieser sich teleportiert
                RespawnClientRpc();
            }
            else
            {
                // Keine Leben mehr
                GameOverClientRpc();
            }
        }
    }

    [ClientRpc]
    private void RespawnClientRpc()
    {
        if (!IsOwner) return;

        // WICHTIG: Der CharacterController muss für einen manuellen Teleport kurz deaktiviert werden, 
        // da er sonst die transform.position überschreibt.
        if (characterController != null)
            characterController.enabled = false;

        // Fallback-Spawnpunkt (z.B. in der Mitte der Arena leicht erhöht)
        transform.position = new Vector3(0, 5, 0);

        if (characterController != null)
            characterController.enabled = true;
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        if (!IsOwner) return;

        // Hier kann später eine Game-Over-UI aktiviert oder der Client disconnected werden
        Debug.Log("[PlayerHealth] Game Over! Keine Leben mehr.");
        if (lebenTextUI != null) lebenTextUI.text = "GAME OVER";
    }
}