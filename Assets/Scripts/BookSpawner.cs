using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public GameObject bookPrefab;
    public Vector2 areaSize = new Vector2(10f, 10f);
    public float spawnHeight = 1.5f;
    public BookData[] bookTypes;
    [Min(1)] public int copiesPerBook = 10;
    [Min(1)] public int testBookTypeCount = 15;

    void Start()
    {
        int bookTypeCount = bookTypes != null && bookTypes.Length > 0 ? bookTypes.Length : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);
        GameStats.Initialize(bookTypeCount, copiesPerBook);
        SpawnBooks(bookTypeCount);
    }

    void SpawnBooks(int bookTypeCount)
    {
        List<int> ids = new List<int>(bookTypeCount * copiesPerBook);
        for (int index = 0; index < bookTypeCount; index++)
            for (int copy = 0; copy < copiesPerBook; copy++) ids.Add(index);

        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        foreach (int index in ids) SpawnSingleBook(index);
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

        Vector3 pos = transform.position + new Vector3(Random.Range(-areaSize.x / 2f, areaSize.x / 2f), spawnHeight, Random.Range(-areaSize.y / 2f, areaSize.y / 2f));
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
        book.transform.rotation = Random.rotation * bookItem.NativeRotation;

        // Spawn sonrasinda uygula; runtime scene-load taramasi spawned kitaplari kacirabilir.
        BookToonEffect.ApplyToBook(book);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null) rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
