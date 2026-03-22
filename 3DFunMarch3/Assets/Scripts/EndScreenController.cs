using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class EndScreenController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument endScreenDocument;

    // UI Elemente
    private VisualElement endPanel;
    private Label         titelLabel;
    private Label         gewinnertextLabel;
    private VisualElement rankingListe;
    private Button        neustartButton;
    private Label         warteLabel;

    // Zustand
    private bool warRundeAktiv  = false;
    private bool panelSichtbar  = false;

    void Start()
    {
        var root           = endScreenDocument.rootVisualElement;
        endPanel           = root.Q("EndPanel");
        titelLabel         = root.Q<Label>("TitelLabel");
        gewinnertextLabel  = root.Q<Label>("GewinnertextLabel");
        rankingListe       = root.Q("RankingListe");
        neustartButton     = root.Q<Button>("NeustartButton");
        warteLabel         = root.Q<Label>("WarteLabel");

        neustartButton.clicked += NeustartKlick;

        // Startzustand
        endPanel.style.opacity   = 0f;
        endPanel.style.translate = new Translate(0, 30, 0);
    }

    void Update()
    {
        var gm = GameManager.Singleton;
        if (gm == null) return;

        bool rundeAktiv = gm.RundeAktiv.Value;

        if (warRundeAktiv && !rundeAktiv && !panelSichtbar)
            PanelEinblenden();

        if (!warRundeAktiv && rundeAktiv && panelSichtbar)
            PanelAusblenden();

        warRundeAktiv = rundeAktiv;
    }

    // ============ Panel Animationen ============

    private void PanelEinblenden()
    {
        panelSichtbar = true;

        var nm = NetworkManager.Singleton;
        bool istHost = nm != null && nm.IsHost;

        // Neustart / Warte Label
        neustartButton.style.display = istHost
            ? DisplayStyle.Flex : DisplayStyle.None;
        warteLabel.style.display = !istHost
            ? DisplayStyle.Flex : DisplayStyle.None;

        // Gewinner ermitteln und Text setzen
        GewinnertextSetzen();

        // Ranking aufbauen
        StartCoroutine(RankingAufbauenMitDelay());

        // Panel Slide-Up + Fade-In
        endPanel.style.transitionProperty =
            new StyleList<StylePropertyName>(
                new List<StylePropertyName>
                { new StylePropertyName("opacity"), new StylePropertyName("translate") }
            );
        endPanel.style.transitionDuration =
            new StyleList<TimeValue>(
                new List<TimeValue>
                { new TimeValue(0.35f, TimeUnit.Second), new TimeValue(0.35f, TimeUnit.Second) }
            );
        endPanel.style.transitionTimingFunction =
            new StyleList<EasingFunction>(
                new List<EasingFunction>
                { new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOut) }
            );

        endPanel.schedule.Execute(() =>
        {
            endPanel.style.opacity   = 1f;
            endPanel.style.translate = new Translate(0, 0, 0);
        }).ExecuteLater(30);

        // Neustart-Button Pulse (nur Host)
        if (istHost)
            StartCoroutine(NeustartPulse());
    }

    private void PanelAusblenden()
    {
        panelSichtbar = false;
        StopAllCoroutines();

        endPanel.style.opacity   = 0f;
        endPanel.style.translate = new Translate(0, 30, 0);
    }

    // ============ Gewinner Text ============

    private void GewinnertextSetzen()
    {
        var gm = GameManager.Singleton;
        var nm = NetworkManager.Singleton;
        if (gm == null || nm == null) return;

        ulong gewinnerId = gm.GewinnerId.Value;

        if (gewinnerId == ulong.MaxValue)
        {
            gewinnertextLabel.text = "";
            return;
        }

        bool ichBinGewinner = gewinnerId == nm.LocalClientId;

        string name = ichBinGewinner
            ? LobbyUIController.SpielerName
            : "Spieler " + gewinnerId;

        gewinnertextLabel.text = ichBinGewinner
            ? "Du gewinnst diese Runde!"
            : name + " gewinnt diese Runde!";

        gewinnertextLabel.style.color = ichBinGewinner
            ? new StyleColor(new Color(0.99f, 0.80f, 0.10f))
            : new StyleColor(new Color(0.39f, 0.40f, 0.95f));
    }

    // ============ Ranking ============

    private IEnumerator RankingAufbauenMitDelay()
    {
        rankingListe.Clear();

        var nm = NetworkManager.Singleton;
        var gm = GameManager.Singleton;
        if (nm == null || gm == null) yield break;

        ulong gewinnerId = gm.GewinnerId.Value;
        ulong eigeneId   = nm.LocalClientId;

        // Spielerdaten sammeln
        var spielerDaten = new List<(ulong id, float hoehe, int leben)>();

        foreach (var client in nm.ConnectedClients)
        {
            ulong id        = client.Key;
            var obj         = client.Value.PlayerObject;
            if (obj == null) continue;

            float hoehe     = obj.transform.position.y;
            int leben       = 0;
            var health      = obj.GetComponent<PlayerHealth>();
            if (health != null) leben = health.AktuelleLeben;

            spielerDaten.Add((id, hoehe, leben));
        }

        // Absteigend nach Hoehe sortieren
        spielerDaten.Sort((a, b) => b.hoehe.CompareTo(a.hoehe));

        // Eintraege versetzt einblenden
        for (int i = 0; i < spielerDaten.Count; i++)
        {
            var (id, hoehe, leben) = spielerDaten[i];

            bool istGewinner = id == gewinnerId;
            bool istEigen    = id == eigeneId;
            bool istGameOver = leben <= 0;

            string name = istEigen
                ? LobbyUIController.SpielerName
                : "Spieler " + id;

            var zeile = RankingZeileErstellen(
                rang:       i + 1,
                name:       name,
                hoehe:      hoehe,
                leben:      leben,
                istGewinner: istGewinner,
                istEigen:   istEigen,
                istGameOver: istGameOver
            );

            // Zeile startet unsichtbar und rechts versetzt
            zeile.style.opacity   = 0f;
            zeile.style.translate = new Translate(20, 0, 0);
            rankingListe.Add(zeile);

            // Versetztes Einblenden
            int index = i;
            zeile.schedule.Execute(() =>
            {
                zeile.style.transitionProperty =
                    new StyleList<StylePropertyName>(
                        new List<StylePropertyName>
                        { new StylePropertyName("opacity"), new StylePropertyName("translate") }
                    );
                zeile.style.transitionDuration =
                    new StyleList<TimeValue>(
                        new List<TimeValue>
                        { new TimeValue(0.3f, TimeUnit.Second), new TimeValue(0.3f, TimeUnit.Second) }
                    );
                zeile.style.transitionTimingFunction =
                    new StyleList<EasingFunction>(
                        new List<EasingFunction>
                        { new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOut) }
                    );
                zeile.style.opacity   = 1f;
                zeile.style.translate = new Translate(0, 0, 0);
            }).ExecuteLater(400 + index * 120);

            yield return null;
        }
    }

    private VisualElement RankingZeileErstellen(
        int rang, string name, float hoehe, int leben,
        bool istGewinner, bool istEigen, bool istGameOver)
    {
        var zeile = new VisualElement();
        zeile.AddToClassList("ranking-row");
        if (istGewinner)
            zeile.AddToClassList("ranking-row--winner");

        // Rang
        var rangLabel = new Label(istGewinner ? "★" : rang.ToString());
        rangLabel.AddToClassList("ranking-rank");
        if (istGewinner)
            rangLabel.AddToClassList("ranking-rank--winner");

        // Name
        var nameLabel = new Label(name + (istEigen ? "  (Du)" : ""));
        nameLabel.AddToClassList("ranking-name");
        if (istGewinner)
            nameLabel.style.color = new StyleColor(new Color(0.92f, 0.70f, 0.08f));
        else if (istEigen)
            nameLabel.style.color = new StyleColor(new Color(0.39f, 0.40f, 0.95f));

        // Hoehe
        var hoeheLabel = new Label(Mathf.RoundToInt(hoehe) + " m");
        hoeheLabel.AddToClassList("ranking-value");

        // Leben
        string lebenText = istGameOver ? "—" : "";
        for (int i = 0; i < leben; i++) lebenText += "♥";
        var lebenLabel = new Label(lebenText);
        lebenLabel.AddToClassList("ranking-lives");
        if (istGameOver)
            lebenLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));

        zeile.Add(rangLabel);
        zeile.Add(nameLabel);
        zeile.Add(hoeheLabel);
        zeile.Add(lebenLabel);

        return zeile;
    }

    // ============ Neustart ============

    private void NeustartKlick()
    {
        var gm = GameManager.Singleton;
        if (gm == null || !gm.IsHost) return;

        Time.timeScale = 1f;
        gm.RundeNeustartenOeffentlich();
        UIManager.Singleton.ZeigeHUD();
    }

    private IEnumerator NeustartPulse()
    {
        while (true)
        {
            neustartButton.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(0.9f);
            neustartButton.style.opacity = 0.6f;
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
