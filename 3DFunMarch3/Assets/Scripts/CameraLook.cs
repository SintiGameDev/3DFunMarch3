using UnityEngine;
using Unity.Netcode;

public class CameraLook : NetworkBehaviour
{
    [Header("Maus")]
    [SerializeField] private float mausSensitivitaet = 2f;
    [SerializeField] private float maxBlickWinkelOben = 80f;
    [SerializeField] private float maxBlickWinkelUnten = 80f;

    private float vertikaleRotation = 0f;
    private Transform spielerKoerper;
    private Camera kamera;

    void Awake()
    {
        kamera = GetComponent<Camera>();

        if (kamera != null)
            kamera.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        spielerKoerper = transform.parent;

        if (!IsOwner) return;

        if (kamera != null)
            kamera.enabled = true;

        CursorSperren();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Cursor-Zustand je nach aktivem Screen setzen
        bool spielAktiv = UIManager.Singleton != null
            && UIManager.Singleton.AktuellerScreen == UIManager.Screen.HUD;

        if (spielAktiv && Cursor.lockState != CursorLockMode.Locked)
            CursorSperren();
        else if (!spielAktiv && Cursor.lockState != CursorLockMode.None)
            CursorFreigeben();

        // Kamerasteuerung nur wenn Spiel aktiv und Cursor gesperrt
        if (!spielAktiv) return;

        float mausX = Input.GetAxis("Mouse X") * mausSensitivitaet;
        float mausY = Input.GetAxis("Mouse Y") * mausSensitivitaet;

        vertikaleRotation -= mausY;
        vertikaleRotation = Mathf.Clamp(vertikaleRotation,
                            -maxBlickWinkelOben, maxBlickWinkelUnten);
        transform.localRotation = Quaternion.Euler(vertikaleRotation, 0f, 0f);

        spielerKoerper.Rotate(Vector3.up * mausX);
    }

    private void CursorSperren()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void CursorFreigeben()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
