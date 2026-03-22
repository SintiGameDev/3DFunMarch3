using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class RoundEndPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button neustartButton;

    [Header("Spielerliste")]
    [SerializeField] private Transform listeContainer;
    [SerializeField] private TextMeshProUGUI eintragPrefab;

    [Header("Farben")]
    [SerializeField] private Color farbeGewinner = new Color(1.0f, 0.85f, 0.1f);
    [SerializeField] private Color farbeEigen    = new Color(0.3f, 1.0f, 0.5f);
    [SerializeField] private Color farbeAndere   = new Color(1.0f, 1.0f, 1.0f);
    [SerializeField] private Color farbeGameOver = new Color(0.8f, 0.3f, 0.3f);

    private bool warRundeAktiv = false;
    private List<TextMeshProUGUI> eintraege = new List<TextMeshProUGUI>();

    void Start()
    {
        panel.SetActive(false);

        neustartButton.onClick.AddListener(NeustartKlick);

        // Neustart-Button nur fuer Host sichtbar
        neustartButton.gameObject.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.Singleton;
        if (gm == null) return;

        bool rundeAktiv = gm.RundeAktiv.Value;

        // Rundenende erkennen
        if (warRundeAktiv && !rundeAktiv)
            PanelAnzeigen();

        // Neustart erkennen
        if (!warRundeAktiv && rundeAktiv)
            PanelAusblenden();

        warRundeAktiv = rundeAktiv;
    }

    private void PanelAnzeigen()
    {
        panel.SetActive(true);
        Time.timeScale = 0f;

        // Neustart-Button nur fuer Host
        var nm = NetworkManager.Singleton;
        neustartButton.gameObject.SetActive(nm != null && nm.IsHost);

        ListeAufbauen();
    }

    private void PanelAusblenden()
    {
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void ListeAufbauen()
    {
        // Alte Eintraege loeschen
        foreach (var e in eintraege)
            if (e != null) Destroy(e.gameObject);
        eintraege.Clear();

        var nm = NetworkManager.Singleton;
        var gm = GameManager.Singleton;
        if (nm == null || gm == null) return;

        ulong gewinnerId = gm.GewinnerId.Value;
        ulong eigeneId   = nm.LocalClientId;

        // Spielerdaten sammeln und nach Hoehe sortieren
        var spielerDaten = new List<(ulong id, float hoehe, int leben)>();

        foreach (var client in nm.ConnectedClients)
        {
            ulong id              = client.Key;
            var spielerObjekt     = client.Value.PlayerObject;
            if (spielerObjekt == null) continue;

            float hoehe = spielerObjekt.transform.position.y;
            int leben   = 0;

            var health = spielerObjekt.GetComponent<PlayerHealth>();
            if (health != null)
                leben = health.AktuelleLeben;

            spielerDaten.Add((id, hoehe, leben));
        }

        // Absteigend nach Hoehe sortieren
        spielerDaten.Sort((a, b) => b.hoehe.CompareTo(a.hoehe));

        // Eintraege erstellen
        for (int i = 0; i < spielerDaten.Count; i++)
        {
            var (id, hoehe, leben) = spielerDaten[i];

            bool istGewinner = id == gewinnerId;
            bool istEigen    = id == eigeneId;
            bool istGameOver = leben <= 0;

            Color farbe = istGewinner ? farbeGewinner
                        : istEigen    ? farbeEigen
                        : istGameOver ? farbeGameOver
                        : farbeAndere;

            string rang         = (i + 1) + ".";
            string spielerName  = istEigen ? "Du" : "Spieler " + id;
            string hoeheText    = hoehe.ToString("F1") + " m";
            string lebenText    = istGameOver ? "Game Over" : leben + " Leben";
            string gewLabel     = istGewinner ? "  <size=80%>Gewinner</size>" : "";

            string inhalt =
                "<color=#" + ColorUtility.ToHtmlStringRGB(farbe) + ">" +
                "<b>" + rang + " " + spielerName + gewLabel + "</b>\n" +
                "<size=80%>" + hoeheText + "   " + lebenText + "</size>" +
                "</color>";

            var eintrag = Instantiate(eintragPrefab, listeContainer);
            eintrag.text = inhalt;
            eintraege.Add(eintrag);
        }
    }

    private void NeustartKlick()
    {
        var gm = GameManager.Singleton;
        if (gm == null || !gm.IsHost) return;

        // timeScale zuruecksetzen bevor Neustart
        Time.timeScale = 1f;
        gm.RundeNeustartenOeffentlich();
    }
}
