using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
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

    [Header("Spawn Cakisma Koruması")]
    [Tooltip("Kitap fizik sistemine girmeden once baska bir kitapla cakismadigi konumu bulmak icin denenecek maksimum konum sayisi.")]
    [Min(1)]
    public int maxSpawnAttempts = 40;

    void Start()
    {
        int bookTypeCount = bookTypes != null && bookTypes.Length > 0
            ? bookTypes.Length
            : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);

        GameStats.Initialize(bookTypeCount, copiesPerBook);
        SpawnBooks(bookTypeCount);
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

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float x = Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
            float z = Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
            Vector3 pos = transform.position + new Vector3(x, spawnHeight, z);
            Quaternion randomSpawnRotation = Random.rotation;

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
            book.transform.rotation = randomSpawnRotation * bookItem.NativeRotation;

            Rigidbody rb = book.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            Physics.SyncTransforms();

            if (!OverlapsExistingBook(book))
            {
                if (rb != null)
                {
                    rb.detectCollisions = true;
                    // Unity'nin sweep CCD'sinde hizli kitap ContinuousDynamic,
                    // onun carpacagi diger dynamic kitaplar Continuous olmalidir.
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.isKinematic = false;
                    rb.maxDepenetrationVelocity = 10f;
                    rb.solverIterations = 12;
                    rb.solverVelocityIterations = 12;
                    rb.WakeUp();
                    rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                }

                return;
            }

            Destroy(book);
        }

        Debug.LogWarning($"BookSpawner: BookID {bookID} icin {maxSpawnAttempts} denemede bos spawn noktasi bulunamadi. Kitap spawn edilmedi.");
    }

    bool OverlapsExistingBook(GameObject candidate)
    {
        if (candidate == null)
            return true;

        Collider[] candidateColliders = candidate.GetComponentsInChildren<Collider>(true);
        int bookLayerMask = 1 << 8;

        foreach (Collider candidateCollider in candidateColliders)
        {
            if (candidateCollider == null || !candidateCollider.enabled)
                continue;

            Bounds bounds = candidateCollider.bounds;
            Collider[] overlaps = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                Quaternion.identity,
                bookLayerMask,
                QueryTriggerInteraction.Ignore);

            foreach (Collider otherCollider in overlaps)
            {
                if (otherCollider == null)
                    continue;

                BookItem otherBook = otherCollider.GetComponentInParent<BookItem>();
                if (otherBook != null && otherBook.gameObject != candidate)
                    return true;
            }
        }

        return false;
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
