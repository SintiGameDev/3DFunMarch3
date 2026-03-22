using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class GameHUDController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument hudDocument;

    [Header("Hoehenbereich")]
    [SerializeField] private float hudYMin = 0.15f;
    [SerializeField] private float hudYMax = 0.80f;
    [SerializeField] private float weltHoeheMin = 0f;
    [SerializeField] private float weltHoeheMax = 50f;

    private Label          timerLabel;
    private Label          rundeLabel;
    private Label          debugLabel;
    private Label          lebenLabel;
    private VisualElement  hoehenContainer;

    private class SpielerEintrag
    {
        public VisualElement container;
        public VisualElement indikator;
        public Label         nameLabel;
        public Label         hoeheLabel;
    }
    private readonly Dictionary<ulong, SpielerEintrag> eintraege = new();

    private bool  warRundeAktiv  = false;
    private int   rundenZahl     = 1;
    private float letzteTimerSek = -1f;

    void Start()
    {
        var root        = hudDocument.rootVisualElement;
        timerLabel      = root.Q<Label>("TimerLabel");
        rundeLabel      = root.Q<Label>("RundeLabel");
        debugLabel      = root.Q<Label>("DebugLabel");
        lebenLabel      = root.Q<Label>("LebenLabel");
        hoehenContainer = root.Q("HoehenContainer");
    }

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        TimerAktualisieren();
        DebugAktualisieren(nm);
        SpielerEintraegeAktualisieren(nm);
        LebenAktualisieren(nm);
        RundenEndeErkennen();
    }

    // ============ Timer ============

    private void TimerAktualisieren()
    {
        var gm = GameManager.Singleton;
        if (gm == null || timerLabel == null) return;

        float sek  = gm.VerbleibendeSekunden.Value;
        int aktSek = Mathf.CeilToInt(sek);
        int min    = Mathf.FloorToInt(sek / 60f);
        int s      = Mathf.FloorToInt(sek % 60f);

        timerLabel.text = min + ":" + s.ToString("D2");

        bool warnung = sek <= 10f;
        if (warnung) timerLabel.AddToClassList("hud-timer--warnung");
        else         timerLabel.RemoveFromClassList("hud-timer--warnung");

        if (aktSek != Mathf.CeilToInt(letzteTimerSek))
        {
            letzteTimerSek = sek;
            TimerPulse();
        }
    }

    private void TimerPulse()
    {
        if (timerLabel == null) return;
        timerLabel.style.scale = new StyleScale(new Scale(new Vector2(1.12f, 1.12f)));
        timerLabel.schedule.Execute(() =>
            timerLabel.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)))
        ).ExecuteLater(80);
    }

    // ============ Debug ============

    private void DebugAktualisieren(NetworkManager nm)
    {
        if (debugLabel == null) return;

        string rolle = nm.IsHost ? "HOST" : "CLIENT";
        int spieler  = nm.ConnectedClients.Count;

        float ping = 0f;
        if (!nm.IsHost)
        {
            var t = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (t != null) ping = t.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        string pingZeile = nm.IsHost ? "" : "\n" + Mathf.RoundToInt(ping) + " ms";
        string code = !string.IsNullOrEmpty(LobbyManager.AktuellerJoinCode)
            ? "\n" + LobbyManager.AktuellerJoinCode : "";

        debugLabel.text = rolle + "  " + spieler + "P" + code + pingZeile;
    }

    // ============ Spieler Hoehenranking ============

    private void SpielerEintraegeAktualisieren(NetworkManager nm)
    {
        if (hoehenContainer == null) return;

        float containerHoehe = hoehenContainer.resolvedStyle.height;
        if (containerHoehe <= 0) return;

        var spielerDaten = new List<(ulong id, float hoehe, string name, Color farbe)>();

        foreach (var client in nm.ConnectedClients)
        {
            var obj = client.Value.PlayerObject;
            if (obj == null) continue;

            float  hoehe = obj.transform.position.y;
            string name  = "Spieler " + client.Key;
            Color  farbe = Color.white;

            // Name und Farbe aus PlayerData lesen
            var pd = obj.GetComponent<PlayerData>();
            if (pd != null)
            {
                name  = pd.GetName();
                farbe = pd.GetFarbe();
            }

            spielerDaten.Add((client.Key, hoehe, name, farbe));
        }

        spielerDaten.Sort((a, b) => b.hoehe.CompareTo(a.hoehe));

        var geseheneIds = new HashSet<ulong>();

        for (int i = 0; i < spielerDaten.Count; i++)
        {
            var (id, hoehe, name, farbe) = spielerDaten[i];
            geseheneIds.Add(id);

            bool istEigen = id == nm.LocalClientId;

            if (!eintraege.TryGetValue(id, out var eintrag))
            {
                eintrag = EintragErstellen(id, istEigen);
                eintraege[id] = eintrag;
                hoehenContainer.Add(eintrag.container);
            }

            // Y-Position aus Welthoehe berechnen
            float t      = Mathf.InverseLerp(weltHoeheMin, weltHoeheMax, hoehe);
            float yFaktor = Mathf.Lerp(hudYMax, hudYMin, t);
            float yPixel  = containerHoehe * yFaktor;

            eintrag.container.style.top = yPixel;

            // Farbe setzen
            eintrag.indikator.style.backgroundColor = new StyleColor(farbe);

            // Name und Hoehe
            eintrag.nameLabel.text  = name;
            eintrag.hoeheLabel.text = Mathf.RoundToInt(hoehe) + "m";

            // Eigener Spieler volle Deckkraft, andere leicht transparent
            eintrag.container.style.opacity = istEigen ? 1f : 0.55f;

            // Eigene Spielerfarbe fuer den Namen
            eintrag.nameLabel.style.color = istEigen
                ? new StyleColor(farbe)
                : new StyleColor(new Color(1f, 1f, 1f, 0.85f));
        }

        // Getrennte Spieler entfernen
        var zuEntfernen = new List<ulong>();
        foreach (var id in eintraege.Keys)
            if (!geseheneIds.Contains(id))
                zuEntfernen.Add(id);

        foreach (var id in zuEntfernen)
        {
            hoehenContainer.Remove(eintraege[id].container);
            eintraege.Remove(id);
        }
    }

    private SpielerEintrag EintragErstellen(ulong id, bool istEigen)
    {
        var container = new VisualElement();
        container.AddToClassList("hud-spieler-eintrag");

        var indikator = new VisualElement();
        indikator.AddToClassList("hud-spieler-indikator");

        var nameLabel = new Label();
        nameLabel.AddToClassList("hud-spieler-name");
        if (istEigen)
            nameLabel.AddToClassList("hud-spieler-name--eigen");

        var hoeheLabel = new Label();
        hoeheLabel.AddToClassList("hud-spieler-hoehe");

        container.Add(indikator);
        container.Add(nameLabel);
        container.Add(hoeheLabel);

        return new SpielerEintrag
        {
            container  = container,
            indikator  = indikator,
            nameLabel  = nameLabel,
            hoeheLabel = hoeheLabel
        };
    }

    // ============ Leben ============

    private void LebenAktualisieren(NetworkManager nm)
    {
        if (lebenLabel == null) return;

        var lokalerSpieler = nm.LocalClient?.PlayerObject;
        if (lokalerSpieler == null) return;

        var health = lokalerSpieler.GetComponent<PlayerHealth>();
        if (health == null) return;

        string herzen = "";
        for (int i = 0; i < health.AktuelleLeben; i++)
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

        if (!warRundeAktiv && rundeAktiv && rundeLabel != null)
            rundeLabel.text = "RUNDE " + rundenZahl;

        warRundeAktiv = rundeAktiv;
    }
}
