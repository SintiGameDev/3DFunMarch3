using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class LobbyUIController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument lobbyDocument;

    [Header("Einstellungen")]
    [SerializeField] private int maxSpieler = 4;

    // Zustaende
    private enum Zustand { Name, Modus, Host, Join }
    private Zustand aktuellerZustand = Zustand.Name;

    // Root Elemente
    private VisualElement root;
    private VisualElement lobbyPanel;

    // Zustand-Container
    private VisualElement zustandName;
    private VisualElement zustandModus;
    private VisualElement zustandHost;
    private VisualElement zustandJoin;

    // Zustand Name
    private TextField   nameInput;
    private Button      weiterButton;
    private Label       nameFehlerLabel;

    // Zustand Modus
    private Label  begrüssungLabel;
    private Label  statusLabel;
    private Button hostButton;
    private Button joinButton;

    // Zustand Host
    private Label hostCodeLabel;
    private Label spielerZaehlerLabel;
    private Button abbrechenButton;

    // Zustand Join
    private TextField joinCodeInput;
    private Label     joinFehlerLabel;
    private Button    verbindenButton;
    private Button    zurueckButton;

    // Titel
    private VisualElement panel;

    public static string SpielerName { get; private set; } = "Spieler";

    private async void Start()
    {
        root = lobbyDocument.rootVisualElement;
        BindElements();
        RegisterEvents();
        PanelEinblenden();
        ZustandWechseln(Zustand.Name);

        await UnityServicesInitialisieren();
    }

    private void BindElements()
    {
        lobbyPanel         = root.Q("LobbyPanel");

        zustandName        = root.Q("ZustandName");
        zustandModus       = root.Q("ZustandModus");
        zustandHost        = root.Q("ZustandHost");
        zustandJoin        = root.Q("ZustandJoin");

        nameInput          = root.Q<TextField>("NameInput");
        weiterButton       = root.Q<Button>("WeiterButton");
        nameFehlerLabel    = root.Q<Label>("NameFehlerLabel");

        begrüssungLabel    = root.Q<Label>("BegrüssungLabel");
        statusLabel        = root.Q<Label>("StatusLabel");
        hostButton         = root.Q<Button>("HostButton");
        joinButton         = root.Q<Button>("JoinButton");

        hostCodeLabel      = root.Q<Label>("HostCodeLabel");
        spielerZaehlerLabel = root.Q<Label>("SpielerZaehlerLabel");
        abbrechenButton    = root.Q<Button>("AbrechenButton");

        joinCodeInput      = root.Q<TextField>("JoinCodeInput");
        joinFehlerLabel    = root.Q<Label>("JoinFehlerLabel");
        verbindenButton    = root.Q<Button>("VerbindenButton");
        zurueckButton      = root.Q<Button>("ZurueckButton");
    }

    private void RegisterEvents()
    {
        // Name Input - Weiter-Button aktivieren wenn Name nicht leer
        nameInput.RegisterValueChangedCallback(e =>
        {
            bool hatName = e.newValue.Trim().Length > 0;
            if (hatName)
                weiterButton.RemoveFromClassList("btn--disabled");
            else
                weiterButton.AddToClassList("btn--disabled");

            nameFehlerLabel.style.display = DisplayStyle.None;
        });

        weiterButton.clicked += () =>
        {
            string name = nameInput.value.Trim();
            if (string.IsNullOrEmpty(name))
            {
                nameFehlerLabel.style.display = DisplayStyle.Flex;
                StartCoroutine(ShakeAnimation(nameFehlerLabel));
                return;
            }

            SpielerName = string.IsNullOrEmpty(name) ? "Spieler" : name;
            begrüssungLabel.text = "Hallo, " + name + "!";
            ZustandWechseln(Zustand.Modus);
        };

        hostButton.clicked  += HostStarten;
        joinButton.clicked  += () => ZustandWechseln(Zustand.Join);
        abbrechenButton.clicked += Beenden;
        verbindenButton.clicked += ClientVerbinden;
        zurueckButton.clicked   += () => ZustandWechseln(Zustand.Modus);
    }

    // ============ Animationen ============

    private void PanelEinblenden()
    {
        // Panel startet transparent und unten versetzt
        lobbyPanel.style.opacity   = 0f;
        lobbyPanel.style.translate = new Translate(0, 40, 0);

        // Nach einem Frame animieren (UI muss erst gerendert sein)
        lobbyPanel.schedule.Execute(() =>
        {
            lobbyPanel.style.transitionProperty  =
                new StyleList<StylePropertyName>(
                    new System.Collections.Generic.List<StylePropertyName>
                    { new StylePropertyName("opacity"), new StylePropertyName("translate") }
                );
            lobbyPanel.style.transitionDuration  =
                new StyleList<TimeValue>(
                    new System.Collections.Generic.List<TimeValue>
                    { new TimeValue(0.4f, TimeUnit.Second), new TimeValue(0.4f, TimeUnit.Second) }
                );
            lobbyPanel.style.transitionTimingFunction =
                new StyleList<EasingFunction>(
                    new System.Collections.Generic.List<EasingFunction>
                    { new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOut) }
                );

            lobbyPanel.style.opacity   = 1f;
            lobbyPanel.style.translate = new Translate(0, 0, 0);
        }).ExecuteLater(50);
    }

    private void ZustandWechseln(Zustand neuerZustand)
    {
        // Alle ausblenden
        zustandName.style.display  = DisplayStyle.None;
        zustandModus.style.display = DisplayStyle.None;
        zustandHost.style.display  = DisplayStyle.None;
        zustandJoin.style.display  = DisplayStyle.None;

        // Ziel-Container einblenden mit Fade
        VisualElement ziel = neuerZustand switch
        {
            Zustand.Name  => zustandName,
            Zustand.Modus => zustandModus,
            Zustand.Host  => zustandHost,
            Zustand.Join  => zustandJoin,
            _             => zustandName
        };

        ziel.style.display = DisplayStyle.Flex;
        ziel.style.opacity = 0f;

        ziel.schedule.Execute(() =>
        {
            ziel.style.transitionProperty =
                new StyleList<StylePropertyName>(
                    new System.Collections.Generic.List<StylePropertyName>
                    { new StylePropertyName("opacity") }
                );
            ziel.style.transitionDuration =
                new StyleList<TimeValue>(
                    new System.Collections.Generic.List<TimeValue>
                    { new TimeValue(0.25f, TimeUnit.Second) }
                );
            ziel.style.opacity = 1f;
        }).ExecuteLater(20);

        aktuellerZustand = neuerZustand;
    }

    private IEnumerator ShakeAnimation(VisualElement element)
    {
        float[] offsets = { -6f, 6f, -4f, 4f, -2f, 2f, 0f };
        foreach (float offset in offsets)
        {
            element.style.translate = new Translate(offset, 0, 0);
            yield return new WaitForSeconds(0.04f);
        }
        element.style.translate = new Translate(0, 0, 0);
    }

    // ============ Netzwerk Logik ============

    private async Task UnityServicesInitialisieren()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            StatusSetzen("Bereit.");
        }
        catch (System.Exception e)
        {
            StatusSetzen("Dienst nicht erreichbar.");
            Debug.LogError("[LobbyUI] Services Fehler: " + e.Message);
        }
    }

    private async void HostStarten()
    {
        try
        {
            hostButton.SetEnabled(false);
            StatusSetzen("Erstelle Session...");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxSpieler);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>()
                          .SetRelayServerData(relayData);

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.OnClientConnectedCallback    += OnSpielerVerbunden;
            NetworkManager.Singleton.OnClientDisconnectCallback   += OnSpielerGetrennt;

            hostCodeLabel.text = joinCode;
            LobbyManager.AktuellerJoinCode = joinCode;
            ZustandWechseln(Zustand.Host);
            SpielerZaehlerAktualisieren();

            UIManager.Singleton.ZeigeHUD();
        }
        catch (System.Exception e)
        {
            hostButton.SetEnabled(true);
            StatusSetzen("Fehler. Bitte erneut versuchen.");
            Debug.LogError("[LobbyUI] Host Fehler: " + e.Message);
        }
    }

    private async void ClientVerbinden()
    {
        string code = joinCodeInput.value.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            joinFehlerLabel.text = "Bitte einen Code eingeben.";
            joinFehlerLabel.style.display = DisplayStyle.Flex;
            StartCoroutine(ShakeAnimation(joinFehlerLabel));
            return;
        }

        try
        {
            verbindenButton.SetEnabled(false);
            joinFehlerLabel.style.display = DisplayStyle.None;

            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(code);

            var relayData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>()
                          .SetRelayServerData(relayData);

            LobbyManager.AktuellerJoinCode = code;
            NetworkManager.Singleton.StartClient();

            UIManager.Singleton.ZeigeHUD();
        }
        catch (System.Exception e)
        {
            verbindenButton.SetEnabled(true);
            joinFehlerLabel.text = "Code nicht gefunden. Bitte pruefen.";
            joinFehlerLabel.style.display = DisplayStyle.Flex;
            StartCoroutine(ShakeAnimation(joinFehlerLabel));
            Debug.LogError("[LobbyUI] Join Fehler: " + e.Message);
        }
    }

    private void Beenden()
    {
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnSpielerVerbunden;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnSpielerGetrennt;
        NetworkManager.Singleton.Shutdown();
        LobbyManager.AktuellerJoinCode = "";
        ZustandWechseln(Zustand.Modus);
        UIManager.Singleton.ZeigeLobby();
    }

    private void OnSpielerVerbunden(ulong id)   => SpielerZaehlerAktualisieren();
    private void OnSpielerGetrennt(ulong id)    => SpielerZaehlerAktualisieren();

    private void SpielerZaehlerAktualisieren()
    {
        int anzahl = NetworkManager.Singleton.ConnectedClients.Count;
        if (spielerZaehlerLabel != null)
            spielerZaehlerLabel.text = "Spieler: " + anzahl + " verbunden";
    }

    private void StatusSetzen(string text)
    {
        if (statusLabel != null)
            statusLabel.text = text;
    }
}
