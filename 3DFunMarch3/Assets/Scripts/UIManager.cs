using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    public static UIManager Singleton { get; private set; }

    [Header("UI Documents")]
    [SerializeField] private UIDocument lobbyDocument;
    [SerializeField] private UIDocument hudDocument;
    [SerializeField] private UIDocument endScreenDocument;

    public enum Screen { Lobby, HUD, EndScreen }
    private Screen aktuellerScreen = Screen.Lobby;

    void Awake()
    {
        Singleton = this;
    }

    void Start()
    {
        ZeigeLobby();
    }

    public void ZeigeLobby()
    {
        SetzeAlle(DisplayStyle.None);
        lobbyDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        aktuellerScreen = Screen.Lobby;
        Time.timeScale  = 1f;
    }

    public void ZeigeHUD()
    {
        SetzeAlle(DisplayStyle.None);
        hudDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        aktuellerScreen = Screen.HUD;
        Time.timeScale  = 1f;
    }

    public void ZeigeEndScreen()
    {
        // HUD bleibt sichtbar, EndScreen legt sich darueber
        hudDocument.rootVisualElement.style.display        = DisplayStyle.Flex;
        endScreenDocument.rootVisualElement.style.display  = DisplayStyle.Flex;
        lobbyDocument.rootVisualElement.style.display      = DisplayStyle.None;
        aktuellerScreen = Screen.EndScreen;
        Time.timeScale  = 0f;
    }

    private void SetzeAlle(DisplayStyle style)
    {
        lobbyDocument.rootVisualElement.style.display     = style;
        hudDocument.rootVisualElement.style.display       = style;
        endScreenDocument.rootVisualElement.style.display = style;
    }

    public Screen AktuellerScreen => aktuellerScreen;
}
