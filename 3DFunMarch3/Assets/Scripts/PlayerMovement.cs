using UnityEngine;
using Unity.Netcode;
using System.Collections; // Wichtig f�r Coroutine

public class PlayerMovement : NetworkBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float bewegungsGeschwindigkeit = 5f;
    [SerializeField] private float sprintGeschwindigkeit = 9f;
    [SerializeField] private float sprungKraft = 5f;
    [SerializeField] private float schwerkraft = -20f;

    [Header("Doppelsprung")]
    [SerializeField] private bool erlaubeDoppelSprung = true;
    [SerializeField] private float doppelSprungKraft = 5f;

    [Header("Schubsen Feedback (Rein per Skript)")]
    [SerializeField] private Color schubFarbe = Color.yellow;
    [SerializeField] private float flashDauer = 0.2f;
    [SerializeField] private float schubDamping = 5f; // Wie schnell die Sto�kraft nachl�sst

    [Header("Visuals")]
    [SerializeField] private Transform visualModel;

    [Header("Netzwerk Visuals")]
    private NetworkVariable<float> netzwerkModellRotationY = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private float letzteGesendeteRotationY = 0f;

    private CharacterController characterController;
    private Camera spielerKamera;
    private Renderer[] spielerRenderers; // F�r visuellen Effekt
    private Color[] originalFarben; // Zum Wiederherstellen

    private float vertikaleGeschwindigkeit = 0f;
    private bool kannDoppelSprung = false;

    // Aktuelle Sto�-Geschwindigkeit, die auf den Spieler wirkt
    private Vector3 aktuelleSchubGeschwindigkeit = Vector3.zero;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();

        // Renderer f�r visuellen Effekt suchen
        spielerRenderers = GetComponentsInChildren<Renderer>();
        OriginalFarbenSpeichern();

        if (visualModel == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name == "HumanCharacterDummy_M")
                {
                    visualModel = child;
                    break;
                }
            }
        }

        if (!IsOwner)
        {
            characterController.enabled = false;
            // 'enabled = false;' was removed here so the script can still run Update() for non-owners to sync the visualModel!
            return;
        }

        spielerKamera = GetComponentInChildren<Camera>();
        if (spielerKamera == null)
            Debug.LogWarning("[PlayerMovement] Keine Kamera im Spieler gefunden.");

        characterController.enabled = true;
    }

    void Update()
    {
        // Nicht-Besitzer (andere Spieler) interpolieren die Rotation vom Server
        if (!IsOwner && visualModel != null)
        {
            Quaternion zielRot = Quaternion.Euler(visualModel.eulerAngles.x, netzwerkModellRotationY.Value, visualModel.eulerAngles.z);
            visualModel.rotation = Quaternion.Slerp(visualModel.rotation, zielRot, Time.deltaTime * 10f);
        }

        if (!IsOwner) return;
        if (characterController == null) return;

        // Sto�geschwindigkeit �ber Zeit abbauen (Damping)
        if (aktuelleSchubGeschwindigkeit.magnitude > 0.1f)
        {
            aktuelleSchubGeschwindigkeit = Vector3.Lerp(aktuelleSchubGeschwindigkeit, Vector3.zero, Time.deltaTime * schubDamping);
        }
        else
        {
            aktuelleSchubGeschwindigkeit = Vector3.zero;
        }

        BewegungenVerarbeiten();
    }

    private void BewegungenVerarbeiten()
    {
        bool istAmBoden = characterController.isGrounded;

        if (istAmBoden && vertikaleGeschwindigkeit < 0)
        {
            vertikaleGeschwindigkeit = -2f;
            kannDoppelSprung = true;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertikal = Input.GetAxisRaw("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        float aktuelleGeschwindigkeit = sprint ? sprintGeschwindigkeit : bewegungsGeschwindigkeit;

        Vector3 richtung = Vector3.zero;
        if (spielerKamera != null)
        {
            Vector3 vorwaerts = spielerKamera.transform.forward;
            Vector3 rechts = spielerKamera.transform.right;
            vorwaerts.y = 0f; rechts.y = 0f;
            vorwaerts.Normalize(); rechts.Normalize();
            richtung = vorwaerts * vertikal + rechts * horizontal;
        }
        else
        {
            richtung = transform.forward * vertikal + transform.right * horizontal;
        }

        if (richtung.magnitude > 1f) richtung.Normalize();

        // Drehe das visuelle Modell in die Blickrichtung der Kamera (wo der Spieler hinschaut)
        if (visualModel != null && spielerKamera != null)
        {
            Vector3 blickRichtung = spielerKamera.transform.forward;
            blickRichtung.y = 0f;
            if (blickRichtung.sqrMagnitude > 0.001f)
            {
                Quaternion zielRotation = Quaternion.LookRotation(blickRichtung.normalized);
                // "Less strong": Langsamer eindrehen (5f statt 15f)
                visualModel.rotation = Quaternion.Slerp(visualModel.rotation, zielRotation, Time.deltaTime * 5f);
                
                // Rotations-Aenderung ans Netzwerk senden
                if (Mathf.Abs(Mathf.DeltaAngle(visualModel.eulerAngles.y, letzteGesendeteRotationY)) > 2f)
                {
                    UpdateModellRotationServerRpc(visualModel.eulerAngles.y);
                    letzteGesendeteRotationY = visualModel.eulerAngles.y;
                }
            }
        }

        // Sprung
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (istAmBoden)
            {
                vertikaleGeschwindigkeit = Mathf.Sqrt(sprungKraft * -2f * schwerkraft);
            }
            else if (erlaubeDoppelSprung && kannDoppelSprung)
            {
                vertikaleGeschwindigkeit = Mathf.Sqrt(doppelSprungKraft * -2f * schwerkraft);
                kannDoppelSprung = false;
            }
        }

        vertikaleGeschwindigkeit += schwerkraft * Time.deltaTime;

        // --- FINALE BERECHNUNG ---
        // Normale Bewegung + Schwerkraft
        Vector3 bewegung = (richtung * aktuelleGeschwindigkeit);
        bewegung.y = vertikaleGeschwindigkeit;

        // WICHTIG: Die Sto�geschwindigkeit wird hier hinzuaddiert!
        Vector3 finaleBewegung = bewegung + aktuelleSchubGeschwindigkeit;

        characterController.Move(finaleBewegung * Time.deltaTime);
    }

    [ServerRpc]
    private void UpdateModellRotationServerRpc(float rotY)
    {
        netzwerkModellRotationY.Value = rotY;
    }

    // --- SCHUBSEN LOGIK (ClientRpc) ---

    [ClientRpc]
    public void EmpfangeSchubClientRpc(Vector3 schubKraftVector)
    {
        // Diese Methode l�uft auf dem Client des Opfers
        Debug.Log($"[Client {OwnerClientId}] Ich wurde geschubst! Kraft: {schubKraftVector}");

        // Die Kraft direkt setzen (wird in Update() verarbeitet)
        // Wir nutzen den CharacterController, daher manipulieren wir die Geschwindigkeit direkt.
        aktuelleSchubGeschwindigkeit = schubKraftVector;

        // Visuellen Effekt starten (rein per Skript)
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FlashColorRoutine());
        }
    }

    private void OriginalFarbenSpeichern()
    {
        if (spielerRenderers == null) return;
        originalFarben = new Color[spielerRenderers.Length];
        for (int i = 0; i < spielerRenderers.Length; i++)
        {
            if (spielerRenderers[i].material.HasProperty("_Color"))
                originalFarben[i] = spielerRenderers[i].material.color;
        }
    }

    private IEnumerator FlashColorRoutine()
    {
        // 1. Farbe auf SchubFarbe setzen
        for (int i = 0; i < spielerRenderers.Length; i++)
        {
            if (spielerRenderers[i].material.HasProperty("_Color"))
                spielerRenderers[i].material.color = schubFarbe;
        }

        // 2. Warten
        yield return new WaitForSeconds(flashDauer);

        // 3. OriginalFarbe wiederherstellen
        for (int i = 0; i < spielerRenderers.Length; i++)
        {
            if (spielerRenderers[i].material.HasProperty("_Color"))
                spielerRenderers[i].material.color = originalFarben[i];
        }
    }
}