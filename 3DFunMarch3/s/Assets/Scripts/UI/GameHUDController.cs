using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class GameHUDController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument hudDocument;

    // Debug Panel
    private Label debugLabel;

    // Timer
    private Label timerLabel;
    private Label rundeLabel;

    // Hoehen Panel
    private VisualElement hoehenListe;

    // Leben Panel
    private Label lebenLabel;

    // Interner Zustand
    private int letzterRundenwert = 1;
    private int rundenZahl = 1;
    private bool warRundeAktiv = false;
    private float letzteTimerWarnung = 0f;

    // Hoehen-Eintraege Cache
    private readonly Dictionary<ulong, Label> hoehenEintraege = new();

    void Start()
    {
        var root = hudDocument.rootVisualElement;

        debugLabel   = root.Q<Label>("DebugLabel");
        timerLabel   = root.Q<Label>("TimerLabel");
        rundeLabel   = root.Q<Label>("RundeLabel");
        hoehenListe  = root.Q("HoehenListe");
        lebenLabel   = root.Q<Label>("LebenLabel");
    }

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        DebugAktualisieren(nm);
        TimerAktualisieren();
        HoehenAktualisieren(nm);
        LebenAktualisieren(nm);
        RundenEndeErkennen();
    }

    // ============ Debug ============

    private void DebugAktualisieren(NetworkManager nm)
    {
        if (debugLabel == null) return;

        string rolle = nm.IsHost ? "Host" : "Client";
        string farbe = nm.IsHost ? "#34d399" : "#60a5fa";
        int spieler  = nm.ConnectedClients.Count;

        float ping = 0f;
        if (!nm.IsHost)
        {
            var transport = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
                ping = transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        string pingZeile = nm.IsHost ? "" :
            "\nPing: <color=" + PingFarbe(ping) + ">" + ping + " ms</color>";

        string code = !string.IsNullOrEmpty(LobbyManager.AktuellerJoinCode)
            ? "\nCode: <b>" + LobbyManager.AktuellerJoinCode + "</b>"
            : "";

        debugLabel.text =
            "<color=" + farbe + "><b>● " + rolle + "</b></color>" +
            "\nSpieler: " + spieler +
            code +
            pingZeile;
    }

    private string PingFarbe(float ping)
    {
        if (ping < 60)  return "#34d399";
        if (ping < 120) return "#fbbf24";
        return "#f87171";
    }

    // ============ Timer ============

    private void TimerAktualisieren()
    {
        var gm = GameManager.Singleton;
        if (gm == null || timerLabel == null) return;

        float sek    = gm.VerbleibendeSekunden.Value;
        int minuten  = Mathf.FloorToInt(sek / 60f);
        int sekunden = Mathf.FloorToInt(sek % 60f);

        timerLabel.text = minuten + ":" + sekunden.ToString("D2");

        // Farbe und Shake unter 10 Sekunden
        if (sek <= 10f)
        {
            timerLabel.style.color = new StyleColor(new Color(0.94f, 0.27f, 0.27f));

            // Shake alle 1 Sekunde
            if (Time.time - letzteTimerWarnung >= 1f)
            {
                letzteTimerWarnung = Time.time;
                TimerShake();
            }
        }
        else
        {
            timerLabel.style.color = new StyleColor(new Color(0.07f, 0.09f, 0.15f));
        }
    }

    private void TimerShake()
    {
        if (timerLabel == null) return;

        float[] offsets = { -4f, 4f, -3f, 3f, 0f };
        int i = 0;

        timerLabel.schedule.Execute(() =>
        {
            if (i < offsets.Length)
            {
                timerLabel.style.translate = new Translate(offsets[i], 0, 0);
                i++;
            }
        }).Every(40).Until(() => i >= offsets.Length);
    }

    // ============ Hoehen ============

    private void HoehenAktualisieren(NetworkManager nm)
    {
        if (hoehenListe == null) return;

        var spielerDaten = new List<(ulong id, float hoehe, string name)>();

        foreach (var client in nm.ConnectedClients)
        {
            var obj = client.Value.PlayerObject;
            if (obj == null) continue;

            float hoehe  = obj.transform.position.y;
            string name  = client.Key == nm.LocalClientId
                ? LobbyUIController.SpielerName
                : "Spieler " + client.Key;

            spielerDaten.Add((client.Key, hoehe, name));
        }

        // Absteigend nach Hoehe sortieren
        spielerDaten.Sort((a, b) => b.hoehe.CompareTo(a.hoehe));

        // Eintraege aktualisieren oder erstellen
        var geseheneIds = new HashSet<ulong>();

        for (int i = 0; i < spielerDaten.Count; i++)
        {
            var (id, hoehe, name) = spielerDaten[i];
            geseheneIds.Add(id);

            bool istEigen    = id == nm.LocalClientId;
            bool istErster   = i == 0;
            string farbeHex  = istEigen ? "#6366f1" : istErster ? "#fbbf24" : "#6b7280";
            string pfeil     = istEigen ? " ←" : "";

            string inhalt =
                "<color=" + farbeHex + ">" +
                "<b>" + name + pfeil + "</b>  " +
                hoehe.ToString("F1") + " m" +
                "</color>";

            if (!hoehenEintraege.TryGetValue(id, out var label))
            {
                label = new Label();
                label.style.fontSize     = 13;
                label.style.marginBottom = 4;
                label.style.whiteSpace   = WhiteSpace.Normal;
                hoehenListe.Add(label);
                hoehenEintraege[id] = label;
            }

            label.text = inhalt;

            // Reihenfolge sicherstellen
            hoehenListe.Remove(label);
            hoehenListe.Add(label);
        }

        // Entfernte Spieler loeschen
        var zuEntfernen = new List<ulong>();
        foreach (var id in hoehenEintraege.Keys)
        {
            if (!geseheneIds.Contains(id))
                zuEntfernen.Add(id);
        }
        foreach (var id in zuEntfernen)
        {
            hoehenListe.Remove(hoehenEintraege[id]);
            hoehenEintraege.Remove(id);
        }
    }

    // ============ Leben ============

    private void LebenAktualisieren(NetworkManager nm)
    {
        if (lebenLabel == null) return;

        var lokalerSpieler = nm.LocalClient?.PlayerObject;
        if (lokalerSpieler == null) return;

        var health = lokalerSpieler.GetComponent<PlayerHealth>();
        if (health == null) return;

        int leben = health.AktuelleLeben;

        // Herzen als Text
        string herzen = "";
        for (int i = 0; i < leben; i++)
            herzen += "♥ ";

        lebenLabel.text = herzen.TrimEnd();
    }

    // ============ Rundenende ============

    private void RundenEndeErkennen()
    {
        var gm = GameManager.Singleton;
        if (gm == null) return;

        bool rundeAktiv = gm.RundeAktiv.Value;

        if (warRundeAktiv && !rundeAktiv)
        {
            rundenZahl++;
            UIManager.Singleton.ZeigeEndScreen();
        }

        if (!warRundeAktiv && rundeAktiv)
        {
            rundeLabel.text = "Runde " + rundenZahl;
        }

        warRundeAktiv = rundeAktiv;
    }
}
