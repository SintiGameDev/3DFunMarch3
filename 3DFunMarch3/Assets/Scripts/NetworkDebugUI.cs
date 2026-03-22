using UnityEngine;
using Unity.Netcode;
using TMPro;

public class NetworkDebugUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Farben")]
    [SerializeField] private Color farbeHost     = new Color(0.2f, 0.9f, 0.4f);
    [SerializeField] private Color farbeClient   = new Color(0.3f, 0.7f, 1.0f);
    [SerializeField] private Color farbeGetrennt = new Color(0.8f, 0.3f, 0.3f);
    [SerializeField] private Color farbeWarnung  = new Color(1.0f, 0.5f, 0.1f);

    private int rundenZahl = 1;
    private bool warRundeAktiv = false;

    private string FarbeAlsHex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    void Update()
    {
        if (debugText == null) return;

        var nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsListening)
        {
            debugText.text =
                "<color=#" + FarbeAlsHex(farbeGetrennt) + ">" +
                "● Nicht verbunden" +
                "</color>";
            return;
        }

        bool istHost   = nm.IsHost;
        bool istClient = nm.IsClient && !nm.IsHost;

        string rolle      = istHost ? "Host" : "Client";
        string rollenHex  = FarbeAlsHex(istHost ? farbeHost : farbeClient);

        int   spielerAnzahl = nm.ConnectedClients.Count;
        ulong clientId      = nm.LocalClientId;

        float ping = 0f;
        if (istClient)
        {
            var transport = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
                ping = transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        // Timer und Runde aus GameManager
        string timerZeile = "";
        string rundeZeile = "";

        var gm = GameManager.Singleton;
        if (gm != null)
        {
            // Rundenzaehler erhoehen wenn Runde neu startet
            bool rundeAktiv = gm.RundeAktiv.Value;
            if (warRundeAktiv && !rundeAktiv)
                rundenZahl++;
            warRundeAktiv = rundeAktiv;

            float sek     = gm.VerbleibendeSekunden.Value;
            int minuten   = Mathf.FloorToInt(sek / 60f);
            int sekunden  = Mathf.FloorToInt(sek % 60f);
            string timer  = minuten + ":" + sekunden.ToString("D2");

            // Timer-Farbe: orange unter 10 Sekunden
            string timerFarbe = sek <= 10f
                ? FarbeAlsHex(farbeWarnung)
                : "ffffff";

            string rundeStatus = rundeAktiv
                ? "<color=#" + timerFarbe + "><b>" + timer + "</b></color>"
                : "<color=#" + FarbeAlsHex(farbeWarnung) + ">Rundenende</color>";

            timerZeile = Zeile("Timer", rundeStatus);
            rundeZeile = Zeile("Runde", rundenZahl.ToString());
        }

        string codeZeile = "";
        if (!string.IsNullOrEmpty(LobbyManager.AktuellerJoinCode))
        {
            string codeLabel = istHost ? "Host Code" : "Session";
            codeZeile = Zeile(codeLabel, "<b>" + LobbyManager.AktuellerJoinCode + "</b>");
        }

        string pingZeile = "";
        if (istClient)
        {
            string pingFarbe = ping < 60 ? "00cc66" : ping < 120 ? "ffaa00" : "ff4444";
            pingZeile = Zeile("Ping", "<color=#" + pingFarbe + ">" + ping + " ms</color>");
        }

        string trennlinie = "<color=#555555>─────────────────</color>\n";

        debugText.text =
            "<color=#" + rollenHex + "><b>● " + rolle + "</b></color>\n" +
            trennlinie +
            Zeile("Client ID", clientId.ToString()) +
            Zeile("Spieler",   spielerAnzahl.ToString()) +
            codeZeile +
            pingZeile +
            trennlinie +
            rundeZeile +
            timerZeile;
    }

    private string Zeile(string label, string wert)
    {
        return "<color=#aaaaaa>" + label + ":</color>  " + wert + "\n";
    }
}
