using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Leben & Respawn")]
    [SerializeField] private float todesHoehe = -10f;
    [SerializeField] private int startLeben = 3;
    [SerializeField] private float immunitaetsDauer = 3f; // 3 Sekunden Immunität

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lebenTextUI;

    // Synchronisierte Variablen. Server schreibt, alle lesen.
    private NetworkVariable<int> aktuelleLeben = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Synchronisierter Status für die Immunität
    private NetworkVariable<bool> istImmun = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private CharacterController characterController;
    private float immunTimer = 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            aktuelleLeben.Value = startLeben;
            istImmun.Value = false;
        }

        if (IsOwner)
        {
            UpdateLebenUI(0, aktuelleLeben.Value);
            aktuelleLeben.OnValueChanged += UpdateLebenUI;
        }
        else
        {
            if (lebenTextUI != null)
            {
                lebenTextUI.canvas.gameObject.SetActive(false);
            }
        }

        // Listener für visuelles Feedback bei Immunitäts-Statuswechsel
        istImmun.OnValueChanged += OnImmunitaetGeaendert;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            aktuelleLeben.OnValueChanged -= UpdateLebenUI;
        }
        istImmun.OnValueChanged -= OnImmunitaetGeaendert;
    }

    private void UpdateLebenUI(int alterWert, int neuerWert)
    {
        if (IsOwner && lebenTextUI != null)
        {
            lebenTextUI.text = "Leben: " + neuerWert;
        }
    }

    private void OnImmunitaetGeaendert(bool vorher, bool jetzt)
    {
        // Hier greift die visuelle Repräsentation (Client-Side Prediction / Feedback).
        // z.B. MeshRenderer auf transparent setzen, Skript für Blinken aktivieren etc.
        if (jetzt)
        {
            Debug.Log($"[PlayerHealth] Spieler {OwnerClientId} ist nun immun.");
            // Beispiel: GetComponentInChildren<Renderer>().material.color = Color.gray;
        }
        else
        {
            Debug.Log($"[PlayerHealth] Spieler {OwnerClientId} ist wieder verwundbar.");
            // Beispiel: GetComponentInChildren<Renderer>().material.color = Color.white;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        // Immunitäts-Timer verwalten (Nur der Server steuert die Zeit)
        if (istImmun.Value)
        {
            immunTimer -= Time.deltaTime;
            if (immunTimer <= 0f)
            {
                istImmun.Value = false; // Immunität aufheben
            }
        }

        // Fall in den Abgrund prüfen
        if (transform.position.y < todesHoehe)
        {
            Sterben();
        }
    }

    // Variante 1: Wenn das fallende Rigidbody den Spieler trifft
    private void OnCollisionEnter(Collision collision)
    {
        KollisionMitHindernisPruefen(collision.gameObject);
    }

    // Variante 2: Wenn der Spieler in das fallende Objekt läuft (CharacterController Logik)
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        KollisionMitHindernisPruefen(hit.gameObject);
    }

    private void KollisionMitHindernisPruefen(GameObject hitObject)
    {
        if (!IsServer) return;

        if (hitObject.TryGetComponent<FallingObstacle>(out var obstacle))
        {
            // Spieler stirbt nur, wenn das Objekt noch fällt (!IsLanded)
            if (!obstacle.IsLanded)
            {
                Sterben();
            }
        }
    }

    private void Sterben()
    {
        // Abbruchbedingung: Wenn bereits immun, wird der Tod verhindert
        if (istImmun.Value) return;

        if (aktuelleLeben.Value > 0)
        {
            aktuelleLeben.Value--; // Server zieht ein Leben ab

            if (aktuelleLeben.Value > 0)
            {
                // Immunität serverseitig aktivieren
                istImmun.Value = true;
                immunTimer = immunitaetsDauer;

                RespawnClientRpc();
            }
            else
            {
                GameOverClientRpc();
            }
        }
    }

    [ClientRpc]
    private void RespawnClientRpc()
    {
        if (!IsOwner) return;

        if (characterController != null)
            characterController.enabled = false;

        transform.position = new Vector3(0, 5, 0);

        if (characterController != null)
            characterController.enabled = true;
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        if (!IsOwner) return;

        Debug.Log("[PlayerHealth] Game Over! Keine Leben mehr.");
        if (lebenTextUI != null) lebenTextUI.text = "GAME OVER";
    }
}