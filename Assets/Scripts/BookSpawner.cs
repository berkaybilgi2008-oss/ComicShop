using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public Transform v16SpawnArea; // V16 scaled scene spawn area

    [Header("Varsayilan Prefab ve Alan")]
    [Tooltip("BookData icinde ozel prefab verilmezse kullanilacak fiziksel kitap prefab'i.")]
    public GameObject bookPrefab;
    public Vector2 areaSize = new Vector2(10f, 10f);
    public float spawnHeight = 1.5f;

    [Header("Kitap Verileri")]
    [Tooltip("BookID sirasina gore BookData assetlerini koy. Her BookData kendi model prefab'ini kullanabilir.")]
    public BookData[] bookTypes;

    [Min(1)]
    public int copiesPerBook = 10;

    [Header("Test")]
    [Tooltip("BookData listesi bosken kullanilacak kitap turu sayisi. Hazir 15 kitap icin 15 birak.")]
    [Min(1)]
    public int testBookTypeCount = 15;

    private bool sessionSpawned;

    void Start()
    {
        if (FindFirstObjectByType<NetworkManager>() != null) return;
        InitializeStats();
        SpawnBooks(BookTypeCount);
    }

    private int BookTypeCount => bookTypes != null && bookTypes.Length > 0
        ? bookTypes.Length : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);

    public void PrepareSession(NetworkManager manager)
    {
        sessionSpawned = false;
        InitializeStats();
        for (int index = 0; index < BookTypeCount; index++)
        {
            var data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;
            var prefab = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetworkBook>() == null)
                throw new System.InvalidOperationException($"BookSpawner: kitap {index} prefabinda NetworkObject/NetworkBook eksik.");
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
        SpawnBooks(BookTypeCount);
    }

    private void InitializeStats()
    {
        int bookTypeCount = bookTypes != null && bookTypes.Length > 0
            ? bookTypes.Length
            : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);

        GameStats.Initialize(bookTypeCount, copiesPerBook);
    }

    void SpawnBooks(int bookTypeCount)
    {
        List<int> ids = new List<int>(bookTypeCount * copiesPerBook);

        for (int index = 0; index < bookTypeCount; index++)
        {
            for (int copy = 0; copy < copiesPerBook; copy++)
                ids.Add(index);
        }

        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        // Reserve one cell per book instead of sampling the same point repeatedly.
        // IDs are shuffled above, so the scatter still mixes all book types.
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(ids.Count *
            Mathf.Max(0.01f, areaSize.x) / Mathf.Max(0.01f, areaSize.y))));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)ids.Count / columns));
        Vector2 cellSize = new Vector2(areaSize.x / columns, areaSize.y / rows);
        for (int i = 0; i < ids.Count; i++)
        {
            Vector2 cell = new Vector2(-areaSize.x * 0.5f + (i % columns + 0.5f) * cellSize.x,
                -areaSize.y * 0.5f + (i / columns + 0.5f) * cellSize.y);
            SpawnSingleBook(ids[i], cell, cellSize);
        }

        Debug.Log($"BookSpawner: {ids.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int index, Vector2 cell, Vector2 cellSize)
    {
        BookData data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;

        int bookID = data != null ? data.BookID : index;
        int brandID = data != null ? data.BrandID : GetBrandID(bookID);
        GameObject prefabToSpawn = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError($"BookSpawner: BookID {bookID} icin spawn edilecek prefab yok.");
            return;
        }

        Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
        Vector3 pos = area.TransformPoint(new Vector3(cell.x, spawnHeight, cell.y));

        // Prefab'in root rotasyonunu Instantiate ile ezme.
        // Once kitabi olustur, sonra rastgele dunya rotasyonunu native/base rotasyonun ustune uygula.
        GameObject book = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        BookItem bookItem = book.GetComponent<BookItem>();

        if (bookItem == null)
        {
            Debug.LogError($"BookSpawner: '{prefabToSpawn.name}' prefab'inda BookItem bulunamadi. BookData BookID {bookID}.");
            Destroy(book);
            return;
        }

        bookItem.bookID = bookID;
        bookItem.brandID = brandID;

        // Kitaplar runtime'da burada olusturuldugu icin AfterSceneLoad callback'i
        // bu nesneleri henuz goremez. Toon efektini spawn aninda uyguluyoruz.
        BookToonEffect.ApplyToBook(book);

        // Start broad-face down with a random heading. An edge-first spawn plus
        // an impulse was making every book spin violently on session startup.
        Vector3 heading = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
        book.transform.rotation = bookItem.GetAlignedRotation(Vector3.up, heading);
        Physics.SyncTransforms();
        Bounds footprint = new Bounds(area.InverseTransformPoint(book.transform.position), Vector3.zero);
        foreach (var collider in book.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            Bounds bounds = collider.bounds;
            for (int corner = 0; corner < 8; corner++)
                footprint.Encapsulate(area.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1))));
        }
        float roomX = Mathf.Max(0f, cellSize.x * 0.5f - footprint.extents.x - 0.02f);
        float roomZ = Mathf.Max(0f, cellSize.y * 0.5f - footprint.extents.z - 0.02f);
        Vector3 offset = new Vector3(cell.x - footprint.center.x + Random.Range(-roomX, roomX), 0f,
            cell.y - footprint.center.z + Random.Range(-roomZ, roomZ));
        book.transform.position += area.TransformVector(offset);
        if (footprint.size.x > cellSize.x || footprint.size.z > cellSize.y)
            Debug.LogWarning("BookSpawner: spawn area is too small for separated books; enlarge Area Size.", this);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            var networkBook = book.GetComponent<NetworkBook>();
            networkBook.Initialize(bookID, brandID);
            networkBook.NetworkObject.Spawn(true);
        }
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
