using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    [Header("Prefab ve Alan")]
    public GameObject bookPrefab;
    public Vector2 areaSize = new Vector2(10f, 10f);
    public float spawnHeight = 1.5f;

    [Header("Kitap Verileri")]
    [Tooltip("Hazir BookData assetlerini buraya sirayla koy. Su an 15 kitapla test edilebilir; 360'a tamamlandiginda sistem otomatik 3600 kopya uretecek.")]
    public BookData[] bookTypes;

    [Min(1)]
    public int copiesPerBook = 10;

    [Header("Test")]
    [Tooltip("BookData listesi bosken kullanilacak kitap turu sayisi. Hazir 15 kitap icin 15 birak.")]
    [Min(1)]
    public int testBookTypeCount = 15;

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

        for (int bookID = 0; bookID < bookTypeCount; bookID++)
        {
            for (int copy = 0; copy < copiesPerBook; copy++)
                ids.Add(bookID);
        }

        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        foreach (int bookID in ids)
            SpawnSingleBook(bookID);

        Debug.Log($"BookSpawner: {ids.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int bookID)
    {
        float x = Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
        float z = Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
        Vector3 pos = transform.position + new Vector3(x, spawnHeight, z);

        Quaternion rot = Random.rotation;
        GameObject book = Instantiate(bookPrefab, pos, rot);
        BookItem bookItem = book.GetComponent<BookItem>();

        if (bookItem == null)
        {
            Debug.LogError("BookSpawner: BookPrefab uzerinde BookItem bulunamadi.");
            Destroy(book);
            return;
        }

        bookItem.bookID = bookID;
        bookItem.brandID = GetBrandID(bookID);

        if (bookTypes != null && bookID < bookTypes.Length && bookTypes[bookID] != null)
        {
            bookItem.bookID = bookTypes[bookID].BookID;
            bookItem.brandID = bookTypes[bookID].BrandID;
        }

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
