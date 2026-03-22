using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Spawnt Falling Obstacles serverseitig in regelmaessigen Abstaenden.
/// Clients sehen alle Objekte automatisch per NGO-Synchronisation.
/// </summary>
public class FObstacleSpawnManager : NetworkBehaviour
{
    [Header("Spawn Prefabs")]
    [Tooltip("Liste aller moeglichen Falling Obstacle Prefabs")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("Spawn Bereich")]
    [Tooltip("Zentrum des Spawn-Bereichs (normalerweise Mitte der Plattform)")]
    [SerializeField] private Transform plattformZentrum;
    [Tooltip("Halbe Breite des Spawn-Bereichs in X-Richtung")]
    [SerializeField] private float spawnBereichX = 10f;
    [Tooltip("Halbe Breite des Spawn-Bereichs in Z-Richtung")]
    [SerializeField] private float spawnBereichZ = 10f;

    [Header("Spawn Hoehe")]
    [Tooltip("Minimale Spawn-Hoehe ueber der Plattform")]
    [SerializeField] private float minSpawnHoehe = 15f;
    [Tooltip("Maximale Spawn-Hoehe ueber der Plattform")]
    [SerializeField] private float maxSpawnHoehe = 30f;
    [Tooltip("Hoehe der Plattform-Oberflaeche in Weltkoordinaten")]
    [SerializeField] private float plattformHoehe = 0f;

    [Header("Spawn Timing")]
    [Tooltip("Sekunden zwischen den Spawns")]
    [SerializeField] private float spawnIntervall = 2f;
    [Tooltip("Maximale Anzahl gleichzeitiger Obstacles")]
    [SerializeField] private int maxObstacles = 30;

    [Header("Groesse")]
    [SerializeField] private float minSkalierung = 0.5f;
    [SerializeField] private float maxSkalierung = 2.5f;

    private float spawnTimer = 0f;
    private int aktuelleAnzahl = 0;

    public override void OnNetworkSpawn()
    {
        // Nur der Host spawnt Objekte
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            Debug.LogWarning("[FObstacleSpawnManager] Keine Prefabs zugewiesen.");
    }

    void Update()
    {
        if (!IsServer) return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnIntervall && aktuelleAnzahl < maxObstacles)
        {
            spawnTimer = 0f;
            ObstacleSpawnen();
        }
    }

    private void ObstacleSpawnen()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;

        // Zufaelliges Prefab auswaehlen
        int index = Random.Range(0, obstaclePrefabs.Length);
        GameObject prefab = obstaclePrefabs[index];

        if (prefab == null) return;

        // Spawn-Position berechnen
        Vector3 zentrum = plattformZentrum != null
            ? plattformZentrum.position
            : Vector3.zero;

        float x = zentrum.x + Random.Range(-spawnBereichX, spawnBereichX);
        float z = zentrum.z + Random.Range(-spawnBereichZ, spawnBereichZ);

        // Spawn-Hoehe dynamisch anpassen basierend auf gestapelten Objekten
        float spawnHoehe = PlattformHoeheErmitteln(x, z);
        float y = spawnHoehe + Random.Range(minSpawnHoehe, maxSpawnHoehe);

        Vector3 spawnPosition = new Vector3(x, y, z);
        Quaternion spawnRotation = Random.rotation;

        // Objekt instantiieren und im Netzwerk spawnen
        GameObject obj = Instantiate(prefab, spawnPosition, spawnRotation);

        // FallingObstacle Komponente automatisch hinzufuegen falls nicht vorhanden
        if (obj.GetComponent<FallingObstacle>() == null)
            obj.AddComponent<FallingObstacle>();

        // NetworkObject Komponente automatisch hinzufuegen falls nicht vorhanden
        if (obj.GetComponent<NetworkObject>() == null)
            obj.AddComponent<NetworkObject>();

        // Zufaellige Skalierung
        float skalierung = Random.Range(minSkalierung, maxSkalierung);
        obj.transform.localScale = Vector3.one * skalierung;

        obj.GetComponent<NetworkObject>().Spawn(true);
        aktuelleAnzahl++;

        Debug.Log("[FObstacleSpawnManager] Gespawnt: " + prefab.name
                  + " bei " + spawnPosition);
    }

    /// <summary>
    /// Ermittelt die hoechste Oberflaeche an einer Position per Raycast.
    /// So spawnen neue Objekte immer ueber der aktuellen Landschaft.
    /// </summary>
    private float PlattformHoeheErmitteln(float x, float z)
    {
        Vector3 rayStart = new Vector3(x, plattformHoehe + maxSpawnHoehe + 10f, z);
        RaycastHit hit;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, Mathf.Infinity))
            return hit.point.y;

        return plattformHoehe;
    }

    // Oeffentliche Methode um Anzahl zu verringern (z.B. wenn Objekte entfernt werden)
    public void ObstacleEntfernt()
    {
        aktuelleAnzahl = Mathf.Max(0, aktuelleAnzahl - 1);
    }
}
