using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Leben & Respawn")]
    [SerializeField] private float todesHoehe = -10f;
    [SerializeField] private int startLeben = 3;
    [SerializeField] private float immunitaetsDauer = 3f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lebenTextUI;

    [Header("Damage Flash (Wird per Code generiert)")]
    [SerializeField] private Color flashFarbe = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private float fadeOutDauer = 0.5f;

    private Image damageFlashImage;

    private NetworkVariable<int> aktuelleLeben = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> istImmun = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private CharacterController characterController;
    private float immunTimer = 0f;
    private Coroutine flashCoroutine;

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
            // Generiert das komplette Flash-UI dynamisch zur Laufzeit
            DamageFlashUIGenerieren();

            UpdateLebenUI(aktuelleLeben.Value, aktuelleLeben.Value);
            aktuelleLeben.OnValueChanged += UpdateLebenUI;
        }
        else
        {
            if (lebenTextUI != null)
            {
                lebenTextUI.canvas.gameObject.SetActive(false);
            }
        }

        istImmun.OnValueChanged += OnImmunitaetGeaendert;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            aktuelleLeben.OnValueChanged -= UpdateLebenUI;

            // Cleanup: Dynamisch erstelltes Canvas sauber entfernen
            if (damageFlashImage != null && damageFlashImage.canvas != null)
            {
                Destroy(damageFlashImage.canvas.gameObject);
            }
        }
        istImmun.OnValueChanged -= OnImmunitaetGeaendert;
    }

    /// <summary>
    /// Baut das UI-Canvas und das Image per Code auf, um manuelle Editor-Arbeit zu sparen.
    /// </summary>
    private void DamageFlashUIGenerieren()
    {
        // 1. Neues Canvas-Objekt erstellen
        GameObject canvasObj = new GameObject("DamageFlashCanvas_Dynamisch");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Hohe Prioritaet, damit es ueber dem HUD liegt

        // 2. Image-Objekt als Child des Canvas erstellen
        GameObject imageObj = new GameObject("FlashImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        damageFlashImage = imageObj.AddComponent<Image>();
        damageFlashImage.color = new Color(flashFarbe.r, flashFarbe.g, flashFarbe.b, 0f);

        // WICHTIG: Verhindert, dass das unsichtbare Bild UI-Klicks blockiert
        damageFlashImage.raycastTarget = false;

        // 3. RectTransform so einstellen, dass es den gesamten Bildschirm ausfuellt
        RectTransform rect = damageFlashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void UpdateLebenUI(int alterWert, int neuerWert)
    {
        if (!IsOwner) return;

        if (lebenTextUI != null)
        {
            lebenTextUI.text = "Leben: " + neuerWert;
        }

        // Screenflash nur auslösen, wenn tatsächlich Leben verloren wurde
        if (neuerWert < alterWert && damageFlashImage != null)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        damageFlashImage.color = flashFarbe;
        float timer = 0f;

        while (timer < fadeOutDauer)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(flashFarbe.a, 0f, timer / fadeOutDauer);

            Color aktuelleFarbe = damageFlashImage.color;
            aktuelleFarbe.a = alpha;
            damageFlashImage.color = aktuelleFarbe;

            yield return null;
        }

        damageFlashImage.color = new Color(flashFarbe.r, flashFarbe.g, flashFarbe.b, 0f);
    }

    private void OnImmunitaetGeaendert(bool vorher, bool jetzt)
    {
        if (jetzt)
            Debug.Log($"[PlayerHealth] Spieler {OwnerClientId} ist nun immun.");
        else
            Debug.Log($"[PlayerHealth] Spieler {OwnerClientId} ist wieder verwundbar.");
    }

    private void Update()
    {
        if (!IsServer) return;

        if (istImmun.Value)
        {
            immunTimer -= Time.deltaTime;
            if (immunTimer <= 0f)
            {
                istImmun.Value = false;
            }
        }

        if (transform.position.y < todesHoehe)
        {
            Sterben();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        KollisionMitHindernisPruefen(collision.gameObject);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        KollisionMitHindernisPruefen(hit.gameObject);
    }

    private void KollisionMitHindernisPruefen(GameObject hitObject)
    {
        if (!IsServer) return;

        if (hitObject.TryGetComponent<FallingObstacle>(out var obstacle))
        {
            if (!obstacle.IsLanded)
            {
                Sterben();
            }
        }
    }

    private void Sterben()
    {
        if (istImmun.Value) return;

        if (aktuelleLeben.Value > 0)
        {
            aktuelleLeben.Value--;

            if (aktuelleLeben.Value > 0)
            {
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