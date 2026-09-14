using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public Transform v16SpawnArea;

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

        foreach (int index in indices)
            SpawnSingleBook(index);

        Debug.Log($"BookSpawner: {indices.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int index)
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

        Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
        float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
        float z = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
        Vector3 pos = area.TransformPoint(new Vector3(x, spawnHeight, z));

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
}
