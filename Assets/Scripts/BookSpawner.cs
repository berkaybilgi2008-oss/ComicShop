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

    [Header("Elle Duzenlenebilir Spawn Alanlari")]
    [Tooltip("Bos ise eski alan kullanilir. Alanlari Scene ekraninda duzenleyin.")]
    public BookSpawnArea[] spawnAreas;

    private bool sessionSpawned;

    private Vector3 SampleSpawnPosition()
    {
        float total = 0f;
        if (spawnAreas != null)
            foreach (var region in spawnAreas)
                if (region != null && region.isActiveAndEnabled) total += region.Area;
        if (total > 0f)
        {
            float pick = Random.value * total;
            BookSpawnArea last = null;
            foreach (var region in spawnAreas)
            {
                if (region == null || !region.isActiveAndEnabled || region.Area <= 0f) continue;
                last = region;
                pick -= region.Area;
                if (pick <= 0f) return region.Sample();
            }
            return last.Sample();
        }
        if (spawnAreas != null && spawnAreas.Length > 0)
            throw new System.InvalidOperationException("BookSpawner: Spawn Areas listesinde aktif, gecerli alan yok.");
        Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
        return area.TransformPoint(new Vector3(Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
            spawnHeight, Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f)));
    }

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
        // Validate before instantiating any books, including the all-disabled case.
        if (spawnAreas != null && spawnAreas.Length > 0)
        {
            bool valid = false;
            foreach (var region in spawnAreas)
                if (region != null && region.isActiveAndEnabled && region.Area > 0f) valid = true;
            if (!valid)
            {
                Debug.LogError("BookSpawner: Aktif spawn alani yok. En az bir alani acin.");
                return;
            }
        }
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

        foreach (int index in ids)
            SpawnSingleBook(index);

        Debug.Log($"BookSpawner: {ids.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int index)
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

        Vector3 pos = SampleSpawnPosition();

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
