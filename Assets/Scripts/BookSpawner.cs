using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    [Header("Prefab ve Alan")]
    public GameObject bookPrefab;
    public Vector2 areaSize = new Vector2(10f, 10f); // X-Z duzleminde sacilma alani
    public float spawnHeight = 1.5f; // yukaridan birak, fizik yayilsin

    [Header("Super Kahraman Kimlik Sistemi")]
    [Tooltip("Toplam kahraman sayisi BrandConfig.cs'ten otomatik okunur (su an: " +
        "20 marka x 5 kahraman + 2 marka x 10 kahraman = 120 kahraman). Buradan degistirilmez.")]
    public int volumesPerHero = 3;
    [Tooltip("Her cildin kac kopyasi var (sabit: 10)")]
    public int copiesPerVolume = 10;
    [Tooltip("Kahraman isimleri sirasiyla (index 0 = Hero ID 0, index 1 = Hero ID 1, ...). UI'da kitap isimlerini gostermek icin kullanilir.")]
    public string[] heroNames;

    [Header("Kitap Kapaklari")]
    [Tooltip("Kapak materyalleri sirasi ONEMLI: index 0 = Hero0-Cilt0, index 1 = Hero0-Cilt1, "
        + "index 2 = Hero0-Cilt2, index 3 = Hero1-Cilt0, ... yani (heroID * volumesPerHero + volumeID) sirasinda olmali. "
        + "Henuz tum kapaklar hazir degilse kisa liste de birakabilirsin, listeyi basa sararak (mod alarak) kullanilir.")]
    public Material[] coverMaterials;

    void Start()
    {
        int heroCount = BrandConfig.TotalHeroCount; // artik BrandConfig'ten okunuyor, tek kaynak
        GameStats.Initialize(heroCount, volumesPerHero, copiesPerVolume);
        SpawnBooks(heroCount);
    }

    void SpawnBooks(int heroCount)
    {
        // Once spawn edilecek TUM kopyalarin (hero, cilt, kopya numarasi) listesini
        // olustur, sonra karistir. copyIndex'i (0-9) burada SAKLIYORUZ ki karistirdiktan
        // sonra da her kopyanin "kacinci kopya" oldugunu bilelim -- raftaki sabit
        // yuvasini bulmak icin bu numara kullanilacak.
        List<(int heroID, int volumeID, int copyIndex)> toSpawn = new List<(int, int, int)>();
        for (int hero = 0; hero < heroCount; hero++)
        {
            for (int vol = 0; vol < volumesPerHero; vol++)
            {
                for (int copy = 0; copy < copiesPerVolume; copy++)
                {
                    toSpawn.Add((hero, vol, copy));
                }
            }
        }

        // Fisher-Yates karistirma
        for (int i = toSpawn.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (toSpawn[i], toSpawn[j]) = (toSpawn[j], toSpawn[i]);
        }

        foreach (var (heroID, volumeID, copyIndex) in toSpawn)
        {
            SpawnSingleBook(heroID, volumeID, copyIndex);
        }

        Debug.Log($"BookSpawner: toplam {toSpawn.Count} kitap spawn edildi "
            + $"({heroCount} kahraman x {volumesPerHero} cilt x {copiesPerVolume} kopya, "
            + $"{BrandConfig.BrandCount} marka).");
    }

    void SpawnSingleBook(int heroID, int volumeID, int copyIndex)
    {
        float x = Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
        float z = Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
        Vector3 pos = transform.position + new Vector3(x, spawnHeight, z);

        Quaternion rot = Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );

        GameObject book = Instantiate(bookPrefab, pos, rot);
        BookItem bookItem = book.GetComponent<BookItem>();
        bookItem.heroID = heroID;
        bookItem.volumeID = volumeID;
        bookItem.copyIndex = copyIndex; // ONEMLI: rafta hep AYNI sabit yuvaya oturmasini saglar
        bookItem.brandID = BrandConfig.GetBrandForHero(heroID); // ONEMLI: hangi markaya ait oldugunu belirler

        // Kahramanin gercek ismini ata (varsa), yoksa varsayilan "Hero X" kullan
        if (heroNames != null && heroID < heroNames.Length && !string.IsNullOrEmpty(heroNames[heroID]))
            bookItem.heroName = heroNames[heroID];
        else
            bookItem.heroName = $"Hero {heroID + 1}";

        // Bu hero+cilt kombinasyonuna karsilik gelen kapagi ata
        if (coverMaterials != null && coverMaterials.Length > 0)
        {
            int coverIndex = (heroID * volumesPerHero + volumeID) % coverMaterials.Length;
            bookItem.SetCoverMaterial(coverMaterials[coverIndex]);
        }

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
    }
}