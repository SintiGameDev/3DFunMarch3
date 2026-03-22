using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class HeightUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI hoehentextPrefab;
    [SerializeField] private Transform listeContainer;

    [Header("Farben")]
    [SerializeField] private Color eigeneHoehefarbe  = new Color(0.3f, 1.0f, 0.5f);
    [SerializeField] private Color andereHoehefarbe  = new Color(1.0f, 1.0f, 1.0f);
    [SerializeField] private Color gewinnertextfarbe = new Color(1.0f, 0.85f, 0.1f);

    private Dictionary<ulong, TextMeshProUGUI> eintraege
        = new Dictionary<ulong, TextMeshProUGUI>();

    void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        var nm         = NetworkManager.Singleton;
        var gm         = GameManager.Singleton;
        ulong gewinner = gm != null ? gm.GewinnerId.Value : ulong.MaxValue;
        ulong eigeneId = nm.LocalClientId;

        // Eintraege aktualisieren oder erstellen
        foreach (var client in nm.ConnectedClients)
        {
            ulong id            = client.Key;
            var spielerObjekt   = client.Value.PlayerObject;
            if (spielerObjekt == null) continue;

            float hoehe = spielerObjekt.transform.position.y;

            if (!eintraege.ContainsKey(id))
                EintragErstellen(id);

            var text = eintraege[id];
            if (text == null) continue;

            bool istEigen    = id == eigeneId;
            bool istGewinner = id == gewinner;

            string spielerLabel = istEigen ? "Du" : "Spieler " + id;
            string hoeheText    = hoehe.ToString("F1") + " m";
            string gewLabel     = istGewinner ? "\n<size=70%><color=#"
                                  + ColorUtility.ToHtmlStringRGB(gewinnertextfarbe)
                                  + ">Gewinner</color></size>" : "";

            text.color = istEigen ? eigeneHoehefarbe : andereHoehefarbe;
            text.text  = "<b>" + spielerLabel + "</b>  " + hoeheText + gewLabel;
        }

        // Eintraege entfernen fuer getrennte Spieler
        var zuEntfernen = new List<ulong>();
        foreach (var eintrag in eintraege)
        {
            if (!nm.ConnectedClients.ContainsKey(eintrag.Key))
                zuEntfernen.Add(eintrag.Key);
        }
        foreach (var id in zuEntfernen)
        {
            if (eintraege[id] != null)
                Destroy(eintraege[id].gameObject);
            eintraege.Remove(id);
        }

        // Timer anzeigen
        if (gm != null)
        {
            float sek    = gm.VerbleibendeSekunden.Value;
            int minuten  = Mathf.FloorToInt(sek / 60f);
            int sekunden = Mathf.FloorToInt(sek % 60f);
            string timerText = minuten + ":" + sekunden.ToString("D2");
            Debug.Log("[HeightUI] Timer: " + timerText); // Wird spaeter in UI-Element geschrieben
        }
    }

    private void EintragErstellen(ulong id)
    {
        if (hoehentextPrefab == null || listeContainer == null) return;

        var neuerText = Instantiate(hoehentextPrefab, listeContainer);
        eintraege[id] = neuerText;
    }
}
