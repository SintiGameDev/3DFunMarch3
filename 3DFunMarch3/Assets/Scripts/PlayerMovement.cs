using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float bewegungsGeschwindigkeit = 5f;
    [SerializeField] private float sprintGeschwindigkeit = 9f;
    [SerializeField] private float sprungKraft = 5f;
    [SerializeField] private float schwerkraft = -20f;

    [Header("Doppelsprung")]
    [SerializeField] private bool erlaubeDoppelSprung = true;
    [SerializeField] private float doppelSprungKraft = 5f; // Erlaubt separate Justierung der Kraft des zweiten Sprungs

    [Header("Boden-Erkennung")]
    [SerializeField] private Transform bodenPunkt;
    [SerializeField] private float bodenRadius = 0.25f;
    [SerializeField] private LayerMask bodenLayer;

    private CharacterController characterController;
    private Camera spielerKamera;
    private float vertikaleGeschwindigkeit = 0f;

    // Status-Flag für den Doppelsprung
    private bool kannDoppelSprung = false;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();

        if (!IsOwner)
        {
            characterController.enabled = false;
            enabled = false;
            return;
        }

        // Kamera im eigenen Spieler-Objekt suchen
        spielerKamera = GetComponentInChildren<Camera>();

        if (spielerKamera == null)
            Debug.LogWarning("[PlayerMovement] Keine Kamera im Spieler gefunden.");

        characterController.enabled = true;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (characterController == null) return;

        BewegungenVerarbeiten();
    }

    private void BewegungenVerarbeiten()
    {
        // Boden-Erkennung durch den CharacterController
        bool istAmBoden = characterController.isGrounded;

        // Wenn der Spieler auf dem Boden steht, Zustand zurücksetzen
        if (istAmBoden && vertikaleGeschwindigkeit < 0)
        {
            // Verhindert endloses Ansammeln negativer Fallgeschwindigkeit
            vertikaleGeschwindigkeit = -2f;
            kannDoppelSprung = true; // Doppelsprung wieder aufladen
        }

        // Eingaben lesen
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertikal = Input.GetAxisRaw("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        float aktuelleGeschwindigkeit = sprint ? sprintGeschwindigkeit : bewegungsGeschwindigkeit;

        // Bewegungsrichtung relativ zur Kamera berechnen
        Vector3 richtung = Vector3.zero;

        if (spielerKamera != null)
        {
            Vector3 vorwaerts = spielerKamera.transform.forward;
            Vector3 rechts = spielerKamera.transform.right;

            vorwaerts.y = 0f;
            rechts.y = 0f;

            vorwaerts.Normalize();
            rechts.Normalize();

            richtung = vorwaerts * vertikal + rechts * horizontal;
        }
        else
        {
            // Fallback: Spieler-Transform verwenden
            richtung = transform.forward * vertikal + transform.right * horizontal;
        }

        // Diagonale Bewegung normalisieren
        if (richtung.magnitude > 1f)
            richtung.Normalize();

        // --- SPRUNG-LOGIK ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (istAmBoden)
            {
                // Regulärer Absprung vom Boden
                vertikaleGeschwindigkeit = Mathf.Sqrt(sprungKraft * -2f * schwerkraft);
            }
            else if (erlaubeDoppelSprung && kannDoppelSprung)
            {
                // Doppelsprung in der Luft ausführen
                vertikaleGeschwindigkeit = Mathf.Sqrt(doppelSprungKraft * -2f * schwerkraft);
                kannDoppelSprung = false; // Flag verbrauchen, damit kein Triple-Jump möglich ist
            }
        }

        // Schwerkraft akkumulieren
        vertikaleGeschwindigkeit += schwerkraft * Time.deltaTime;

        // Finale Bewegung zusammensetzen
        Vector3 bewegung = richtung * aktuelleGeschwindigkeit;
        bewegung.y = vertikaleGeschwindigkeit;

        // Bewegung auf den Controller anwenden
        characterController.Move(bewegung * Time.deltaTime);
    }
}