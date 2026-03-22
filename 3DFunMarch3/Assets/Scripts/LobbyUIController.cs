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

    public static string SpielerName { get; private set; } = "Spieler";

    private enum Zustand { Name, Modus, Host, Join }
    private Zustand aktuellerZustand;

    private VisualElement lobbyPanel;
    private VisualElement zustandName;
    private VisualElement zustandModus;
    private VisualElement zustandHost;
    private VisualElement zustandJoin;

    private TextField nameInput;
    private Button    weiterButton;
    private Label     nameFehlerLabel;

    private Label  begrüssungLabel;
    private Label  statusLabel;
    private Button hostButton;
    private Button joinButton;

    private Label  hostCodeLabel;
    private Label  spielerZaehlerLabel;
    private Button abbrechenButton;
    private IVisualElementScheduledItem codePulseItem;

    private TextField joinCodeInput;
    private Label     joinFehlerLabel;
    private Button    verbindenButton;
    private Button    zurueckButton;

    private async void Start()
    {
        BindElements();
        RegisterEvents();
        AlleZustaendeAusblenden();

        // Panel + Titel Slide-In
        UIAnimator.SlideInVonUnten(lobbyPanel, offsetPx: 48f, dauer: 0.45f, verzoegerungMs: 100);

        var titel      = lobbyDocument.rootVisualElement.Q<Label>("TitelLabel");
        var untertitel = lobbyDocument.rootVisualElement.Q<Label>("UntertitelLabel");
        UIAnimator.FadeIn(titel,      dauer: 0.4f, verzoegerungMs: 200);
        UIAnimator.FadeIn(untertitel, dauer: 0.4f, verzoegerungMs: 350);

        UIAnimator.SlideInVonUnten(zustandName, offsetPx: 20f, dauer: 0.3f, verzoegerungMs: 450);
        aktuellerZustand = Zustand.Name;

        await UnityServicesInitialisieren();
    }

    private void BindElements()
    {
        var root = lobbyDocument.rootVisualElement;

        lobbyPanel          = root.Q("LobbyPanel");
        zustandName         = root.Q("ZustandName");
        zustandModus        = root.Q("ZustandModus");
        zustandHost         = root.Q("ZustandHost");
        zustandJoin         = root.Q("ZustandJoin");

        nameInput           = root.Q<TextField>("NameInput");
        weiterButton        = root.Q<Button>("WeiterButton");
        nameFehlerLabel     = root.Q<Label>("NameFehlerLabel");

        begrüssungLabel     = root.Q<Label>("BegrüssungLabel");
        statusLabel         = root.Q<Label>("StatusLabel");
        hostButton          = root.Q<Button>("HostButton");
        joinButton          = root.Q<Button>("JoinButton");

        hostCodeLabel       = root.Q<Label>("HostCodeLabel");
        spielerZaehlerLabel = root.Q<Label>("SpielerZaehlerLabel");
        abbrechenButton     = root.Q<Button>("AbrechenButton");

        joinCodeInput       = root.Q<TextField>("JoinCodeInput");
        joinFehlerLabel     = root.Q<Label>("JoinFehlerLabel");
        verbindenButton     = root.Q<Button>("VerbindenButton");
        zurueckButton       = root.Q<Button>("ZurueckButton");
    }

    private void RegisterEvents()
    {
        nameInput.RegisterValueChangedCallback(e =>
        {
            // Namensfeld darf leer bleiben - Weiter immer aktiv
            weiterButton.RemoveFromClassList("btn--disabled");
            weiterButton.SetEnabled(true);
            UIAnimator.FadeOut(nameFehlerLabel, dauer: 0.15f);
        });

        weiterButton.clicked += () =>
        {
            string name = nameInput.value.Trim();
            SpielerName = string.IsNullOrEmpty(name) ? "Spieler" : name;
            begrüssungLabel.text = "Hallo, " + SpielerName + "!";
            UIAnimator.ScaleBounce(weiterButton);
            ZustandWechseln(Zustand.Modus);
        };

        hostButton.clicked += () =>
        {
            UIAnimator.ScaleBounce(hostButton);
            HostStarten();
        };

        joinButton.clicked += () =>
        {
            UIAnimator.ScaleBounce(joinButton);
            ZustandWechseln(Zustand.Join);
        };

        abbrechenButton.clicked += Beenden;

        verbindenButton.clicked += () =>
        {
            UIAnimator.ScaleBounce(verbindenButton);
            ClientVerbinden();
        };

        zurueckButton.clicked += () => ZustandWechseln(Zustand.Modus);
    }

    private void AlleZustaendeAusblenden()
    {
        zustandName.style.display  = DisplayStyle.None;
        zustandModus.style.display = DisplayStyle.None;
        zustandHost.style.display  = DisplayStyle.None;
        zustandJoin.style.display  = DisplayStyle.None;
    }

    private void ZustandWechseln(Zustand neuerZustand)
    {
        VisualElement alt = Container(aktuellerZustand);
        VisualElement neu = Container(neuerZustand);
        aktuellerZustand  = neuerZustand;
        UIAnimator.ZustandWechseln(alt, neu);
    }

    private VisualElement Container(Zustand z) => z switch
    {
        Zustand.Name  => zustandName,
        Zustand.Modus => zustandModus,
        Zustand.Host  => zustandHost,
        Zustand.Join  => zustandJoin,
        _             => zustandName
    };

    // ============ Services ============

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

    // ============ Host ============

    private async void HostStarten()
    {
        hostButton.SetEnabled(false);
        StatusSetzen("Erstelle Session...");

        try
        {
            Allocation allocation = await RelayService.Instance
                                         .CreateAllocationAsync(maxSpieler);
            string joinCode = await RelayService.Instance
                                    .GetJoinCodeAsync(allocation.AllocationId);

            // Null-Check fuer Transport
            var nm = NetworkManager.Singleton;
            if (nm == null)
                throw new System.Exception("NetworkManager nicht gefunden.");

            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
                throw new System.Exception("UnityTransport nicht gefunden.");

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayData);

            nm.StartHost();
            nm.OnClientConnectedCallback  += _ => SpielerZaehlerAktualisieren();
            nm.OnClientDisconnectCallback += _ => SpielerZaehlerAktualisieren();

            LobbyManager.AktuellerJoinCode = joinCode;

            if (hostCodeLabel != null)
                hostCodeLabel.text = joinCode;

            SpielerZaehlerAktualisieren();
            ZustandWechseln(Zustand.Host);

            // Code Pulse auf ZustandHost Container
            codePulseItem = UIAnimator.Pulse(
                zustandHost, minAlpha: 0.7f, maxAlpha: 1f, halbPeriode: 1.1f);

            UIManager.Singleton.ZeigeHUD();
        }
        catch (System.Exception e)
        {
            hostButton.SetEnabled(true);
            StatusSetzen("Fehler: " + e.Message);
            Debug.LogError("[LobbyUI] Host Fehler: " + e.Message);
        }
    }

    // ============ Client ============

    private async void ClientVerbinden()
    {
        string code = joinCodeInput.value.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            joinFehlerLabel.text = "Bitte einen Code eingeben.";
            UIAnimator.FadeIn(joinFehlerLabel, dauer: 0.2f);
            UIAnimator.Shake(joinCodeInput);
            return;
        }

        verbindenButton.SetEnabled(false);
        UIAnimator.FadeOut(joinFehlerLabel, dauer: 0.15f);

        try
        {
            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(code);

            var nm = NetworkManager.Singleton;
            if (nm == null)
                throw new System.Exception("NetworkManager nicht gefunden.");

            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
                throw new System.Exception("UnityTransport nicht gefunden.");

            var relayData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayData);

            LobbyManager.AktuellerJoinCode = code;
            nm.StartClient();
            UIManager.Singleton.ZeigeHUD();
        }
        catch (System.Exception e)
        {
            verbindenButton.SetEnabled(true);
            joinFehlerLabel.text = "Code nicht gefunden. Bitte pruefen.";
            UIAnimator.FadeIn(joinFehlerLabel, dauer: 0.2f);
            UIAnimator.Shake(joinFehlerLabel);
            Debug.LogError("[LobbyUI] Join Fehler: " + e.Message);
        }
    }

    // ============ Stop ============

    private void Beenden()
    {
        codePulseItem?.Pause();
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback  -= _ => SpielerZaehlerAktualisieren();
            nm.OnClientDisconnectCallback -= _ => SpielerZaehlerAktualisieren();
            nm.Shutdown();
        }
        LobbyManager.AktuellerJoinCode = "";
        ZustandWechseln(Zustand.Modus);
        UIManager.Singleton.ZeigeLobby();
    }

    private void SpielerZaehlerAktualisieren()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || spielerZaehlerLabel == null) return;
        spielerZaehlerLabel.text = "Spieler: " + nm.ConnectedClients.Count + " verbunden";
    }

    private void StatusSetzen(string text)
    {
        if (statusLabel != null)
            statusLabel.text = text;
    }
}
