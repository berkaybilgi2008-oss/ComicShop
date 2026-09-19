using Unity.Netcode;
using System.Collections.Generic;
using System;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public Transform v16SpawnArea;

    [Header("Corridor Spawn Areas")]
    public BoxCollider[] corridorAreas;
    [Min(0f)] public float corridorEdgePadding = 0.35f;

    [Header("Varsayilan Prefab ve Alan")]
    public GameObject bookPrefab;
    public Vector2 areaSize = new Vector2(10f, 10f);
    public float spawnHeight = 1.5f;

    [Header("Kitap Verileri")]
    [Tooltip("Eski sahne uyumlulugu icin kullanilir. Doluysa once catalog yerine bu liste kullanilir.")]
    public BookData[] bookTypes;
    public BookCatalog catalog;

    [Min(1)]
    public int copiesPerBook = 10;

    [Header("Test")]
    [Min(1)]
    public int testBookTypeCount = 15;

    private bool sessionSpawned;

    void Start()
    {
        LoadCatalogIfNeeded();
        if (FindFirstObjectByType<NetworkManager>() != null) return;
        ValidateConfiguration(false);
        InitializeStats();
        SpawnBooks(BookTypeCount);
    }

    private void LoadCatalogIfNeeded()
    {
        if (catalog == null)
            catalog = Resources.Load<BookCatalog>("BookCatalog");

        if (catalog != null && catalog.books != null && catalog.books.Length > 0)
            bookTypes = catalog.books;
    }

    private int BookTypeCount => bookTypes != null ? bookTypes.Length : 0;

    public void PrepareSession(NetworkManager manager)
    {
        sessionSpawned = false;
        LoadCatalogIfNeeded();
        ValidateConfiguration(true);
        InitializeStats();

        for (int index = 0; index < BookTypeCount; index++)
        {
            BookData data = bookTypes[index];
            GameObject prefab = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;
            if (data == null || prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetworkBook>() == null)
                throw new System.InvalidOperationException($"BookSpawner: catalogdaki kitap {index} gecersiz veya NetworkObject/NetworkBook eksik.");

            GameStats.RegisterBookID(data.BookID);

            bool registered = manager.NetworkConfig.Prefabs.Contains(prefab);
            foreach (var list in manager.NetworkConfig.Prefabs.NetworkPrefabsLists)
                if (list != null && list.Contains(prefab)) registered = true;
            if (!registered) manager.AddNetworkPrefab(prefab);
        }
    }

    public void SpawnSession()
    {
        if (sessionSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        sessionSpawned = true;
        LoadCatalogIfNeeded();
        ValidateConfiguration(true);
        InitializeStats();
        SpawnBooks(BookTypeCount);
    }

    private void InitializeStats()
    {
        GameStats.Initialize(BookTypeCount, copiesPerBook);
        if (bookTypes == null) return;
        foreach (BookData data in bookTypes)
            if (data != null) GameStats.RegisterBookID(data.BookID);
    }

    void SpawnBooks(int bookTypeCount)
    {
        List<int> indices = new List<int>(bookTypeCount * copiesPerBook);

        for (int index = 0; index < bookTypeCount; index++)
            for (int copy = 0; copy < copiesPerBook; copy++)
                indices.Add(index);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        Vector3[] positions = CreateSpawnPositions(indices.Count);
        for (int i = 0; i < indices.Count; i++)
            SpawnSingleBook(indices[i], positions[i]);

        Debug.Log($"BookSpawner: {indices.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int index, Vector3 pos)
    {
        BookData data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;
        if (data == null)
        {
            Debug.LogError($"BookSpawner: catalog index {index} icin BookData yok.");
            return;
        }

        int bookID = data.BookID;
        int brandID = data.BrandID;
        GameObject prefabToSpawn = data.bookPrefab != null ? data.bookPrefab : bookPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError($"BookSpawner: BookID {bookID} icin spawn edilecek prefab yok.");
            return;
        }

        GameObject book = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        BookItem bookItem = book.GetComponent<BookItem>();

        if (bookItem == null)
        {
            Debug.LogError($"BookSpawner: '{prefabToSpawn.name}' prefab'inda BookItem bulunamadi. BookID {bookID}.");
            Destroy(book);
            return;
        }

        bookItem.bookID = bookID;
        bookItem.brandID = brandID;
        bookItem.displayName = data.DisplayName;
        BookToonEffect.ApplyToBook(book);

        Vector3 heading = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
        book.transform.rotation = bookItem.GetAlignedRotation(Vector3.up, heading);
        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkBook networkBook = book.GetComponent<NetworkBook>();
            networkBook.Initialize(bookID, brandID);
            networkBook.NetworkObject.Spawn(true);
        }
    }
    public Vector3[] CreateSpawnPositions(int count)
    {
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        int guaranteed = corridorAreas == null ? 0 : corridorAreas.Length;
        if (count < guaranteed) throw new System.ArgumentException("At least one book per corridor is required.");
        var positions = new Vector3[count];
        for (int i = 0; i < count; i++) positions[i] = i < guaranteed ? SampleArea(corridorAreas[i]) : SampleSpawnPosition();
        return positions;
    }
    public Vector3 SampleSpawnPosition()
    {
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            float total = 0f;
            foreach (var zone in corridorAreas) total += ZoneWeight(zone);
            float choice = Random.value * total;
            foreach (var zone in corridorAreas) { float w = ZoneWeight(zone); if (w <= 0f) continue; choice -= w; if (choice <= 0f) return SampleArea(zone); }
            return SampleArea(corridorAreas[corridorAreas.Length - 1]);
        }
        Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
        return area.TransformPoint(new Vector3(Random.Range(-areaSize.x*.5f, areaSize.x*.5f), spawnHeight, Random.Range(-areaSize.y*.5f, areaSize.y*.5f)));
    }
    Vector3 SampleArea(BoxCollider zone)
    {
        Vector3 scale = zone.transform.lossyScale;
        float hx = Mathf.Max(0.001f, zone.size.x*.5f - corridorEdgePadding/Mathf.Max(.0001f,Mathf.Abs(scale.x)));
        float hz = Mathf.Max(0.001f, zone.size.z*.5f - corridorEdgePadding/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
        return zone.transform.TransformPoint(zone.center + new Vector3(Random.Range(-hx,hx),0,Random.Range(-hz,hz))) + Vector3.up*spawnHeight;
    }
    public int DiscoverCorridors()
    {
        var zones = new List<BoxCollider>(corridorAreas ?? Array.Empty<BoxCollider>());
        foreach (var root in gameObject.scene.GetRootGameObjects()) if (root.name == "Book Spawn Corridors") foreach (var zone in root.GetComponentsInChildren<BoxCollider>(true)) if (!zones.Contains(zone)) zones.Add(zone);
        corridorAreas = zones.ToArray(); return corridorAreas.Length;
    }
    public bool ValidateSpawnAreas(out string error)
    {
        if (corridorAreas != null && corridorAreas.Length > 0)
        { foreach (var zone in corridorAreas) if (zone == null || !zone.gameObject.activeInHierarchy || ZoneWeight(zone) <= 0f) { error="Invalid corridor spawn area."; return false; } error=null; return true; }
        bool ok = areaSize.x > 0f && areaSize.y > 0f && spawnHeight >= 0f;
        error = ok ? null : "Legacy spawn area is invalid."; return ok;
    }
    void ValidateConfiguration(bool networked)
    {
        DiscoverCorridors();
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        if (copiesPerBook < 1 || BookTypeCount < 1) throw new System.InvalidOperationException("Book catalogue/count is empty.");
        var ids = new HashSet<int>();
        foreach (var data in bookTypes)
        {
            if (data == null || !ids.Add(data.BookID)) throw new System.InvalidOperationException("Invalid or duplicate BookID in catalogue.");
            var prefab = data.bookPrefab != null ? data.bookPrefab : bookPrefab;
            if (prefab == null || prefab.GetComponent<BookItem>() == null || (networked && (prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetworkBook>() == null))) throw new System.InvalidOperationException("Invalid book prefab in catalogue.");
        }
    }
    float ZoneWeight(BoxCollider zone) { if (!zone || !zone.gameObject.activeInHierarchy) return 0f; Vector3 s=zone.transform.lossyScale; return Mathf.Max(0f,zone.size.x*Mathf.Abs(s.x)-2*corridorEdgePadding)*Mathf.Max(0f,zone.size.z*Mathf.Abs(s.z)-2*corridorEdgePadding); }

}
