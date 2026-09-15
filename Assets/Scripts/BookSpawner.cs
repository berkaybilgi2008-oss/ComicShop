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

    [Header("Rastgele Kuleler")]
    [Range(0f, 1f)] public float towerBookFraction = 0.2f;
    [Min(2)] public int booksPerTower = 25;
    [Tooltip("Her kitap icin kule yonunden rastgele sapma (derece).")]
    [Range(0f, 180f)] public float towerYawJitter = 8f;

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

        var books = new List<BookItem>(ids.Count);
        foreach (int index in ids)
        {
            var book = SpawnSingleBook(index);
            if (book != null) books.Add(book);
        }
        int towers = ArrangeTowers(books);
        // Publish only the final layout. Clients never roll their own random layout.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            foreach (var book in books)
            {
                var networkBook = book.GetComponent<NetworkBook>();
                networkBook.Initialize(book.bookID, book.brandID);
                networkBook.NetworkObject.Spawn(true);
            }
        Debug.Log($"BookSpawner: {books.Count} kitap, {towers} kule (kule basina {Mathf.Max(2, booksPerTower)} kitap).");
    }

    BookItem SpawnSingleBook(int index)
    {
        BookData data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;

        int bookID = data != null ? data.BookID : index;
        int brandID = data != null ? data.BrandID : GetBrandID(bookID);
        GameObject prefabToSpawn = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError($"BookSpawner: BookID {bookID} icin spawn edilecek prefab yok.");
            return null;
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
            return null;
        }

        bookItem.bookID = bookID;
        bookItem.brandID = brandID;

        // Kitaplar runtime'da burada olusturuldugu icin AfterSceneLoad callback'i
        // bu nesneleri henuz goremez. Toon efektini spawn aninda uyguluyoruz.
        BookToonEffect.ApplyToBook(book);

        // Scattered books start with independently random 3D orientations.
        // Tower members are aligned to their supporting plane below.
        book.transform.rotation = Random.rotationUniform;
        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        return bookItem;
    }

    private static Bounds BookBounds(BookItem book)
    {
        Bounds result = new Bounds(book.transform.position, Vector3.zero);
        bool found = false;
        foreach (var col in book.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled || col.isTrigger) continue;
            if (!found) { result = col.bounds; found = true; }
            else result.Encapsulate(col.bounds);
        }
        return result;
    }

    private bool InsideArea(Vector3 point, float radius)
    {
        if (spawnAreas != null && spawnAreas.Length > 0)
        {
            foreach (var area in spawnAreas)
                if (area != null && area.isActiveAndEnabled && Fits(area.transform, area.width, area.depth, point, radius))
                    return true;
            return false;
        }
        return Fits(v16SpawnArea != null ? v16SpawnArea : transform, areaSize.x, areaSize.y, point, radius);
    }

    private static bool Fits(Transform area, float width, float depth, Vector3 point, float radius)
    {
        Vector3 p = area.InverseTransformPoint(point);
        Vector3 scale = area.lossyScale;
        return Mathf.Abs(p.x) + radius / Mathf.Max(0.0001f, Mathf.Abs(scale.x)) <= Mathf.Abs(width) * 0.5f &&
            Mathf.Abs(p.z) + radius / Mathf.Max(0.0001f, Mathf.Abs(scale.z)) <= Mathf.Abs(depth) * 0.5f;
    }

    private static bool Ground(Vector3 start, out RaycastHit ground)
    {
        ground = default;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(start, Vector3.down, 30f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<BookItem>() != null) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.PositiveInfinity && ground.normal.y > 0.98f && ground.collider.attachedRigidbody == null;
    }

    private int ArrangeTowers(List<BookItem> books)
    {
        int count = Mathf.Max(2, booksPerTower);
        // Whole towers only; leftover books stay scattered. Never add or remove books.
        int requested = Mathf.Clamp(Mathf.RoundToInt(books.Count * Mathf.Clamp01(towerBookFraction) / count), 0, books.Count / count);
        if (requested == 0) return 0;
        var reservations = new List<Vector4>();
        var stacked = new HashSet<BookItem>();
        Physics.SyncTransforms();
        for (int tower = 0; tower < requested; tower++)
        {
            int first = tower * count;
            float yaw = Random.Range(0f, 360f);
            float radius = 0f, height = 0f;
            for (int i = first; i < first + count; i++)
            {
                var book = books[i];
                Vector3 heading = Quaternion.Euler(0f, yaw + Random.Range(-towerYawJitter, towerYawJitter), 0f) * Vector3.forward;
                book.transform.rotation = book.GetAlignedRotation(Random.value < 0.5f ? Vector3.up : Vector3.down, heading);
            }
            Physics.SyncTransforms();
            for (int i = first; i < first + count; i++)
            {
                Bounds b = BookBounds(books[i]);
                radius = Mathf.Max(radius, new Vector2(b.extents.x, b.extents.z).magnitude);
                height += b.size.y + 0.001f;
            }
            if (radius <= 0f || height <= 0f) continue;
            radius += 0.02f;
            bool found = false;
            Vector3 basePoint = default;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vector3 candidate = SampleSpawnPosition();
                if (!InsideArea(candidate, radius) || !Ground(candidate + Vector3.up * 0.1f, out var floor)) continue;
                candidate.y = floor.point.y;
                bool clear = true;
                foreach (var reservation in reservations)
                    if (Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(reservation.x, reservation.z)) < radius + reservation.w + 0.1f)
                        clear = false;
                // Confirm support at all corners, not just beneath the center.
                for (int c = 0; c < 4 && clear; c++)
                {
                    Vector3 corner = candidate + new Vector3((c % 2 == 0 ? -1f : 1f) * radius, 0.1f, (c < 2 ? -1f : 1f) * radius);
                    if (!Ground(corner, out var edge) || Mathf.Abs(edge.point.y - candidate.y) > 0.01f) clear = false;
                }
                if (!clear) continue;
                foreach (var col in Physics.OverlapBox(candidate + Vector3.up * (height * 0.5f + 0.005f),
                    new Vector3(radius, height * 0.5f, radius), Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                    if (col.GetComponentInParent<BookItem>() == null) { clear = false; break; }
                if (!clear) continue;
                basePoint = candidate; found = true; break;
            }
            if (!found) continue;
            float top = basePoint.y + 0.002f;
            for (int i = first; i < first + count; i++)
            {
                var book = books[i];
                Bounds b = BookBounds(book);
                book.transform.position += new Vector3(basePoint.x - b.center.x, top - b.min.y, basePoint.z - b.center.z);
                top += b.size.y + 0.001f;
                stacked.Add(book);
            }
            reservations.Add(new Vector4(basePoint.x, basePoint.y, basePoint.z, radius));
        }
        // Keep scattered books from starting inside/above the newly built towers.
        foreach (var book in books)
        {
            if (stacked.Contains(book)) continue;
            Bounds b = BookBounds(book);
            float radius = new Vector2(b.extents.x, b.extents.z).magnitude;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                bool overlaps = false;
                foreach (var r in reservations)
                    if (Vector2.Distance(new Vector2(book.transform.position.x, book.transform.position.z), new Vector2(r.x, r.z)) < radius + r.w + 0.05f)
                        overlaps = true;
                if (!overlaps) break;
                book.transform.position = SampleSpawnPosition();
                if (attempt == 127) Debug.LogWarning("BookSpawner: Dagilim alani cok dar; kule yakininda kitap kalabilir.");
            }
        }
        Physics.SyncTransforms();
        if (reservations.Count < requested) Debug.LogWarning($"BookSpawner: {requested} kuleden {reservations.Count} tanesi sigdi; kalan kitaplar daginik.");
        return reservations.Count;
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
