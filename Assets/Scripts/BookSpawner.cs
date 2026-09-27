using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public Transform v16SpawnArea; // Legacy single-area fallback
    [Header("Corridor Spawn Areas")]
    [Tooltip("When assigned, books spawn only in these boxes. Box colliders may stay disabled.")]
    public BoxCollider[] corridorAreas;
    [Min(0f)] public float corridorEdgePadding = 0.35f;

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

    [Header("Performans")]
    [Tooltip("Yerdeki binlerce kitabin her biri golge isiklarinda yeniden cizilir. Oyun kasiyorsa " +
             "kapatmayi dene: kitaplar golge dusurmez ama golge almaya devam eder.")]
    public bool booksCastShadows = true;

    [Header("Raf Onu Yerlesimi")]
    [Tooltip("Acikken yerdeki kitaplar ve kuleler koridorlarin ortasina degil, kitapliklarin " +
             "onune (uzun yuzlerine bitisik bir seride) toplanir. Kitapliklarin arasindaki gecitler " +
             "ve koridorun ortasi bos kalir.")]
    public bool gatherInFrontOfShelves = true;
    [Tooltip("Kitaplik yuzunden itibaren kitaplarin dizilecegi seridin genisligi (metre).")]
    // Yeni ad: sahnede kayitli eski serit genisligi (1.4 / 3.5 / 3.8) yeni degeri ezmesin.
    [Min(0.3f)] public float bookAreaDepthMeters = 4.45f;
    [Tooltip("Kitaplik yuzu ile ilk kitap arasinda birakilan bosluk (metre).")]
    [Min(0f)] public float shelfFrontClearance = 0.1f;

    [Header("Test")]
    [Tooltip("BookData listesi bosken kullanilacak test kitap turu sayisi. Normal oyunda Setup ALL Book Models tarafindan doldurulan bookTypes kullanilir.")]
    [Min(1)]
    public int testBookTypeCount = 15;

    [Header("Rastgele Kuleler")]
    // Yeni alan adlari: sahnede kayitli eski kule ayarlari (0.1 / 15'lik / ekstra uzun) geri gelmesin.
    [Tooltip("Kitaplarin kulelerde duran payi. Kalan kitaplar serit icinde daginik yigilir.")]
    [Range(0f, 1f)] public float towerBookShare = 0.08f;
    [Tooltip("Acikken daginik kitaplar kendi alanlarina (gecici gorunmez duvarlarla cevrili) rastgele " +
             "egimle birakilir ve acilis ekrani sirasinda fizikle devrilip dagilir: gercek karmasa, ic ice gecme yok.")]
    public bool dropLooseBooks = true;
    [Tooltip("Kucuk kulelerin en fazla kitap sayisi (6..bu deger).")]
    [Min(2)] public int smallTowerMaxBooks = 10;
    [Tooltip("Buyuk kulelerin en fazla kitap sayisi (14..bu deger).")]
    [Min(2)] public int largeTowerMaxBooks = 20;
    [Tooltip("Her kitap icin kule yonunden rastgele sapma (derece).")]
    [Range(0f, 180f)] public float towerYawJitter = 8f;
    [Tooltip("Kuleler arasinda birakilan en az bosluk (metre); kuleler serit boyunca dengeli dagilir.")]
    [Min(0.1f)] public float towerGapMeters = 1.1f;
    [Tooltip("Kitaplik uclarinda (karsi koridora gecis, raf sirasi sonu) bos birakilan gecit uzunlugu (metre).")]
    [Min(0f)] public float shelfEndPassage = 1.6f;
    [Tooltip("Tezgah/masa gibi engellerin cevresinde bos birakilan gecis payi (metre).")]
    [Min(0f)] public float propClearance = 1.1f;
    [Tooltip("Daginik kitaplarin bir kismi yandaki kitaba yaslanir (egik durur).")]
    [Range(0f, 1f)] public float bookLeanChance = 0.85f;
    // Daginik yiginlarin en fazla yuksekligi: kuleye donusmesin.
    private const float maxLoosePileHeight = 0.7f;

    private readonly HashSet<BookItem> spawnedTowerBooks = new HashSet<BookItem>();
    private readonly List<Vector4> towerSites = new List<Vector4>();
    private GameObject dropWalls;
    private struct DroppedBody { public Rigidbody body; public CollisionDetectionMode mode; public float depenetration; }
    private readonly List<DroppedBody> droppedBodies = new List<DroppedBody>();

    private void EndDrop()
    {
        if (dropWalls != null) { Destroy(dropWalls); dropWalls = null; }
        foreach (var dropped in droppedBodies)
        {
            if (dropped.body == null) continue;
            dropped.body.collisionDetectionMode = dropped.mode;
            dropped.body.maxDepenetrationVelocity = dropped.depenetration;
        }
        droppedBodies.Clear();
    }

    private double frameBudgetStarted;
    private bool YieldForFrameBudget()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        // Yukleme ekrani sadece dusen kitaplari gizler; her karede daha fazla is yapip
        // ekrani kisa tut (ilerleme cubugu yine akici gorunur).
        if (now - frameBudgetStarted < 0.03) return false;
        frameBudgetStarted = now;
        return true;
    }
    private bool sessionSpawned;
    private readonly List<GameObject> sessionBooks = new List<GameObject>();

    // Tum oyuncularda (host ve istemci) sahne yuklenince calisir: raf gozleri kopya sayisini alsin.
    void Awake() => ShelfSlot.MatchCapacityToCopies(copiesPerBook);

    // ---------------- Raf onu seridi ----------------
    private struct ShelfFootprint { public Vector2 min, max; public bool longAlongX; }
    private readonly List<ShelfFootprint> shelfFootprints = new List<ShelfFootprint>();
    private readonly HashSet<Collider> shelfColliders = new HashSet<Collider>();
    // Koridorlari olusturan kitapliklarin yonu (cogunluk). Ters yondeki kitapliklarin (orn. arka
    // duvardaki, capraz gecit koridoruna bakanlar) onune kitap dokulmez: o alan gecis yolu.
    private bool dominantAlongX;

    private void BuildShelfFootprints()
    {
        shelfFootprints.Clear();
        shelfColliders.Clear();
        if (!gatherInFrontOfShelves) return;
        var roots = new HashSet<Transform>();
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
            if (slot != null && slot.gameObject.scene == gameObject.scene) roots.Add(slot.transform.root);
        foreach (var root in roots)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || col.isTrigger || col.GetComponentInParent<BookItem>() != null) continue;
                shelfColliders.Add(col);
                if (!any) { bounds = col.bounds; any = true; } else bounds.Encapsulate(col.bounds);
            }
            if (!any)
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds);
                }
            if (!any || bounds.size.x < 0.05f || bounds.size.z < 0.05f) continue;
            shelfFootprints.Add(new ShelfFootprint
            {
                min = new Vector2(bounds.min.x, bounds.min.z),
                max = new Vector2(bounds.max.x, bounds.max.z),
                longAlongX = bounds.size.x >= bounds.size.z
            });
        }
        int alongX = 0;
        foreach (var f in shelfFootprints) if (f.longAlongX) alongX++;
        dominantAlongX = alongX * 2 > shelfFootprints.Count;
    }

    // Nokta bir kitapligin UZUN yuzunun onundeki seritte mi? Kitaplik uclarindaki gecitler haric.
    private bool InShelfBand(Vector3 point, float radius)
    {
        if (shelfFootprints.Count == 0) return true;
        Vector2 p = new Vector2(point.x, point.z);
        bool inBand = false;
        foreach (var f in shelfFootprints)
        {
            // Hicbir kitapligin icine ya da dibine girme.
            float gap = shelfFrontClearance + radius;
            if (p.x > f.min.x - gap && p.x < f.max.x + gap && p.y > f.min.y - gap && p.y < f.max.y + gap)
            {
                bool alongInside = f.longAlongX ? p.x > f.min.x && p.x < f.max.x : p.y > f.min.y && p.y < f.max.y;
                if (!alongInside) return false; // uc kismi: gecit
                float across = f.longAlongX ? Mathf.Max(f.min.y - p.y, p.y - f.max.y) : Mathf.Max(f.min.x - p.x, p.x - f.max.x);
                if (across < gap) return false;
            }
            if (f.longAlongX != dominantAlongX) continue; // capraz gecide bakan kitaplik: serit yok
            float along = f.longAlongX ? p.x : p.y;
            float alongMin = (f.longAlongX ? f.min.x : f.min.y) + radius + 0.1f;
            float alongMax = (f.longAlongX ? f.max.x : f.max.y) - radius - 0.1f;
            if (along < alongMin || along > alongMax) continue;
            float distance = f.longAlongX ? Mathf.Max(f.min.y - p.y, p.y - f.max.y) : Mathf.Max(f.min.x - p.x, p.x - f.max.x);
            if (distance >= shelfFrontClearance + radius && distance <= bookAreaDepthMeters - radius) inBand = true;
        }
        if (!inBand) return false;
        // Kitaplik sirasinin uclari (kitapliklar arasi gecit, sira sonu, arka raflarin onundeki
        // capraz koridor) her zaman bos: kitaplik ucundan disari dogru shelfEndPassage kadar,
        // serit derinligi boyunca hic kitap yok.
        foreach (var g in shelfFootprints)
        {
            float along = g.longAlongX ? p.x : p.y;
            float gMin = g.longAlongX ? g.min.x : g.min.y, gMax = g.longAlongX ? g.max.x : g.max.y;
            float beyond = along < gMin ? gMin - along : along > gMax ? along - gMax : -1f;
            if (beyond < 0f || beyond > shelfEndPassage + radius) continue;
            float across = g.longAlongX ? Mathf.Max(g.min.y - p.y, p.y - g.max.y) : Mathf.Max(g.min.x - p.x, p.x - g.max.x);
            if (across <= bookAreaDepthMeters) return false;
        }
        return true;
    }

    // ---------------- Yerlesim icin kaba izgara (binlerce kitapta O(n^2) tarama olmasin) ----------------
    private const float GridCell = 0.5f;
    private readonly Dictionary<long, List<int>> placedGrid = new Dictionary<long, List<int>>();
    private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
    private void GridAdd(List<Bounds> placed, Bounds bounds)
    {
        int index = placed.Count;
        placed.Add(bounds);
        int x0 = Mathf.FloorToInt(bounds.min.x / GridCell), x1 = Mathf.FloorToInt(bounds.max.x / GridCell);
        int z0 = Mathf.FloorToInt(bounds.min.z / GridCell), z1 = Mathf.FloorToInt(bounds.max.z / GridCell);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                long key = CellKey(x, z);
                if (!placedGrid.TryGetValue(key, out var list)) placedGrid.Add(key, list = new List<int>(4));
                list.Add(index);
            }
    }
    private readonly HashSet<int> gridSeen = new HashSet<int>();
    private readonly List<int> gridResult = new List<int>();
    private List<int> GridQuery(Bounds area)
    {
        gridSeen.Clear(); gridResult.Clear();
        int x0 = Mathf.FloorToInt(area.min.x / GridCell), x1 = Mathf.FloorToInt(area.max.x / GridCell);
        int z0 = Mathf.FloorToInt(area.min.z / GridCell), z1 = Mathf.FloorToInt(area.max.z / GridCell);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (placedGrid.TryGetValue(CellKey(x, z), out var list))
                    foreach (int i in list) if (gridSeen.Add(i)) gridResult.Add(i);
        return gridResult;
    }

    IEnumerator Start()
    {
        if (FindFirstObjectByType<NetworkManager>() != null) yield break;
        ValidateConfiguration(false);
        InitializeStats();
        yield return SpawnSessionAsync();
        if (SpawnError == null) ShopRound.BeginOffline();
    }

    private int BookTypeCount => bookTypes != null && bookTypes.Length > 0
        ? bookTypes.Length : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);

    public void PrepareSession(NetworkManager manager)
    {
        sessionSpawned = false;
        ValidateConfiguration(true);
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

    public System.Exception SpawnError { get; private set; }
    public IEnumerator SpawnSessionAsync()
    {
        if (sessionSpawned) yield break;
        SpawnError = null;
        ShelfSlot.BuildNetworkRegistry();
        ShopLoadingScreen.Show();
        var routine = SpawnBooks(BookTypeCount);
        try
        {
            yield return null; // Paint the opening card once; no minimum display time.
            frameBudgetStarted = Time.realtimeSinceStartupAsDouble;
            while (true)
            {
                bool more = false;
                try { more = routine.MoveNext(); }
                catch (System.Exception error) { SpawnError = error; }
                if (SpawnError != null || !more) break;
                yield return routine.Current;
                frameBudgetStarted = Time.realtimeSinceStartupAsDouble;
            }
            sessionSpawned = SpawnError == null;
            // Acilis ekrani kitaplar yere inip durana kadar kalir; dusme animasyonu gorunmez.
            // Tur (ve sayac) ancak bu bittikten sonra baslar.
            if (sessionSpawned)
            {
                var settle = WaitForBooksToSettle();
                while (settle.MoveNext()) yield return settle.Current;
                if (dropWalls != null)
                {
                    // Dusus bitti: her kitap oldugu yerde, oldugu egimle donar (yiginlar artik
                    // kendi kendine ziplamaz). Duvarlar destek sayilmaz; sonra kaldirilir.
                    yield return new WaitForFixedUpdate();
                    int frozen = 0, loose = 0;
                    Transform ignore = dropWalls.transform;
                    foreach (var go in sessionBooks)
                    {
                        if (go == null || !go.TryGetComponent<BookItem>(out var item)) continue;
                        if (item.FreezeWhereResting(ignore)) frozen++;
                        else if (go.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic) loose++;
                    }
                    Debug.Log($"BookSpawner: dusus sonrasi {frozen} kitap yerinde donduruldu, {loose} kitap serbest.");
                    EndDrop();
                }
            }
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose();
            EndDrop();
            if (!sessionSpawned)
            {
                foreach (var book in sessionBooks)
                {
                    if (book == null) continue;
                    var net = book.GetComponent<NetworkObject>();
                    if (net != null && net.IsSpawned) net.Despawn(true); else Destroy(book);
                }
                sessionBooks.Clear();
            }
            ShopLoadingScreen.Hide();
        }
    }

    [Header("Acilis")]
    [Tooltip("Kitaplarin yere inip durmasi icin acilis ekraninin en fazla bekleyecegi sure (saniye).")]
    [Min(1f)] public float maxSettleSeconds = 12f;

    // Kitaplar yerlesene kadar bekler: hareket eden kitap kalmayinca (kisa bir sessizlikten sonra)
    // ya da sure dolunca biter. Yalnizca fizik durumunu okur; kitaplara dokunmaz.
    private IEnumerator WaitForBooksToSettle()
    {
        var bodies = new List<Rigidbody>(sessionBooks.Count);
        foreach (var book in sessionBooks)
            if (book != null && book.TryGetComponent<Rigidbody>(out var body)) bodies.Add(body);
        float start = Time.unscaledTime, quietSince = -1f;
        int initial = -1;
        ShopLoadingScreen.Settling(0f);
        while (true)
        {
            int moving = 0;
            foreach (var body in bodies)
            {
                if (body == null || body.isKinematic || body.IsSleeping()) continue;
                if (body.linearVelocity.sqrMagnitude > 0.0025f || body.angularVelocity.sqrMagnitude > 0.04f) moving++;
            }
            if (initial < 0) initial = Mathf.Max(1, moving);
            float elapsed = Time.unscaledTime - start;
            float settled = 1f - Mathf.Clamp01(moving / (float)initial);
            ShopLoadingScreen.Settling(Mathf.Max(settled * 0.95f, elapsed / Mathf.Max(1f, maxSettleSeconds)));
            if (moving == 0) { if (quietSince < 0f) quietSince = Time.unscaledTime; }
            else quietSince = -1f;
            // Kitaplar artik yerinde donmus basliyor; sessizlik kisa surede gelir.
            if ((quietSince >= 0f && Time.unscaledTime - quietSince >= 0.2f && elapsed >= 0.3f) ||
                elapsed >= Mathf.Min(maxSettleSeconds, dropWalls != null ? 7f : 4f))
                break;
            yield return null;
        }
        ShopLoadingScreen.Settling(1f);
        yield return new WaitForSecondsRealtime(0.1f); // Dolan cubuk kisa bir an tam gorunsun.
    }

    private void InitializeStats()
    {
        int bookTypeCount = bookTypes != null && bookTypes.Length > 0
            ? bookTypes.Length
            : Mathf.Min(testBookTypeCount, BrandConfig.TotalBookTypeCount);

        var ids = new List<int>(bookTypeCount);
        for (int index = 0; index < bookTypeCount; index++)
        {
            var data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;
            int id = data != null ? data.BookID : index;
            int brand = data != null ? data.BrandID : BrandConfig.GetBrandForBookID(id);
            // Devre disi yayincilarin kitaplari spawn olur ama tamamlanma hedefine sayilmaz;
            // aksi halde rafa konamadiklari icin tur hic bitmezdi.
            if (BrandConfig.IsPlacementDisabled(brand)) continue;
            ids.Add(id);
        }
        // Her kitap turu tek raf gozune girer; goz kapasitesinden fazla kopya (orn. 20 kopya,
        // 10'luk goz) hic yerlestirilemez. Hedef kapasiteyle sinirlanir, fazlasi gorsel kalir.
        int target = copiesPerBook;
        int slotCapacity = 0;
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
            if (slot != null) slotCapacity = Mathf.Max(slotCapacity, slot.capacity);
        if (slotCapacity > 0 && target > slotCapacity)
        {
            Debug.Log($"BookSpawner: {copiesPerBook} kopya spawn ediliyor; raf gozu {slotCapacity} kitap aldigi icin tamamlanma hedefi {slotCapacity}.");
            target = slotCapacity;
        }
        GameStats.Initialize(ids, target);
    }

    IEnumerator SpawnBooks(int bookTypeCount)
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

        ValidateConfiguration(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        BuildShelfFootprints();
        sessionBooks.Clear();
        {
            // One guaranteed book per assigned area, remaining books weighted by usable area.
            var positions = CreateSpawnPositions(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                SpawnSingleBook(ids[i], positions[i]);
                if (YieldForFrameBudget()) { ShopLoadingScreen.Progress((float)i / ids.Count * 0.45f); yield return null; }
            }
            var books = new List<BookItem>(sessionBooks.Count);
            foreach (var book in sessionBooks) books.Add(book.GetComponent<BookItem>());
            spawnedTowerBooks.Clear();
            var towers = ArrangeTowers(books);
            while (towers.MoveNext()) yield return towers.Current;
            ShopLoadingScreen.Progress(0.55f);
            var scattered = SeparateInitialBooks(books);
            while (scattered.MoveNext()) yield return scattered.Current;
            int published = 0;
            // Publish the completed server layout, never the pre-arrangement poses.
            foreach (var book in books)
            {
                var body = book.GetComponent<Rigidbody>();
                if (body != null) body.isKinematic = book.IsFrozenAtRest;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    var networkBook = book.GetComponent<NetworkBook>();
                    networkBook.Initialize(book.bookID, book.brandID);
                    networkBook.NetworkObject.Spawn(true);
                }
                published++;
                if (YieldForFrameBudget())
                { ShopLoadingScreen.Progress(0.85f + 0.15f * published / books.Count); yield return null; }
            }
        }
        Debug.Log($"BookSpawner: {ids.Count} fiziksel kitap spawn edildi ({bookTypeCount} farkli kitap x {copiesPerBook} kopya).");
    }

    void SpawnSingleBook(int index, Vector3 pos)
    {
        BookData data = bookTypes != null && index < bookTypes.Length ? bookTypes[index] : null;

        int bookID = data != null ? data.BookID : index;
        int brandID = data != null ? data.BrandID : GetBrandID(bookID);
        GameObject prefabToSpawn = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;

        // Prefab'in root rotasyonunu Instantiate ile ezme.
        // Once kitabi olustur, sonra rastgele dunya rotasyonunu native/base rotasyonun ustune uygula.
        GameObject book = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        sessionBooks.Add(book);
        BookItem bookItem = book.GetComponent<BookItem>();

        bookItem.bookID = bookID;
        bookItem.brandID = brandID;

        // Kitaplar runtime'da burada olusturuldugu icin AfterSceneLoad callback'i
        // bu nesneleri henuz goremez. Toon efektini spawn aninda uyguluyoruz.
        BookToonEffect.ApplyToBook(book);
        if (!booksCastShadows)
            foreach (var renderer in book.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Start broad-face down with a random heading. An edge-first spawn plus
        // an impulse was making every book spin violently on session startup.
        Vector3 heading = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
        book.transform.rotation = bookItem.GetAlignedRotation(Vector3.up, heading);
        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true; // No settling until the entire layout is ready.
        }

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
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            foreach (var area in corridorAreas)
            {
                if (area == null || !area.gameObject.activeInHierarchy) continue;
                Vector3 local = area.transform.InverseTransformPoint(point) - area.center;
                Vector3 scale = area.transform.lossyScale;
                float margin = radius + corridorEdgePadding;
                if (Mathf.Abs(local.x) + margin / Mathf.Abs(scale.x) <= area.size.x * .5f &&
                    Mathf.Abs(local.z) + margin / Mathf.Abs(scale.z) <= area.size.z * .5f) return true;
            }
            return false;
        }
        return Fits(v16SpawnArea != null ? v16SpawnArea : transform, areaSize.x, areaSize.y, point, radius);
    }

    private bool InsideAreaRelaxed(Vector3 point, float expand)
    {
        if (corridorAreas == null || corridorAreas.Length == 0) return InsideArea(point, 0f);
        foreach (var area in corridorAreas)
        {
            if (area == null || !area.gameObject.activeInHierarchy) continue;
            Vector3 local = area.transform.InverseTransformPoint(point) - area.center;
            Vector3 scale = area.transform.lossyScale;
            if (Mathf.Abs(local.x) - expand / Mathf.Abs(scale.x) <= area.size.x * .5f &&
                Mathf.Abs(local.z) - expand / Mathf.Abs(scale.z) <= area.size.z * .5f) return true;
        }
        return false;
    }

    private static bool Fits(Transform area, float width, float depth, Vector3 point, float radius)
    {
        Vector3 p = area.InverseTransformPoint(point);
        Vector3 scale = area.lossyScale;
        return Mathf.Abs(p.x) + radius / Mathf.Max(0.0001f, Mathf.Abs(scale.x)) <= Mathf.Abs(width) * 0.5f &&
            Mathf.Abs(p.z) + radius / Mathf.Max(0.0001f, Mathf.Abs(scale.z)) <= Mathf.Abs(depth) * 0.5f;
    }

    private static readonly Collider[] overlapBuffer = new Collider[64];
    private static readonly RaycastHit[] groundBuffer = new RaycastHit[64];
    private static bool Ground(Vector3 start, out RaycastHit ground)
    {
        ground = default;
        float nearest = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(start, Vector3.down, groundBuffer, 30f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int h = 0; h < count; h++)
        {
            var hit = groundBuffer[h];
            if (hit.collider.GetComponentInParent<BookItem>() != null) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.PositiveInfinity && ground.normal.y > 0.98f && ground.collider.attachedRigidbody == null;
    }

    private IEnumerator ArrangeTowers(List<BookItem> books)
    {
        int smallMax = Mathf.Max(2, smallTowerMaxBooks);
        int largeMax = Mathf.Max(smallMax + 1, largeTowerMaxBooks);
        int budget = Mathf.Clamp(Mathf.RoundToInt(books.Count * Mathf.Clamp01(towerBookShare)), 0, books.Count);
        // Kitap payinin yarisi kucuk (6..10), yarisi buyuk (14..20) kulelere.
        var sizes = new List<int>();
        int smallBudget = budget / 2, largeBudget = budget - budget / 2;
        while (smallBudget >= 2)
        {
            int size = Mathf.Min(smallBudget, Random.Range(Mathf.Max(2, smallMax - 4), smallMax + 1));
            sizes.Add(size); smallBudget -= size;
        }
        while (largeBudget >= 2)
        {
            int size = Mathf.Min(largeBudget, Random.Range(Mathf.Max(smallMax + 1, largeMax - 6), largeMax + 1));
            sizes.Add(size); largeBudget -= size;
        }
        for (int i = sizes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (sizes[i], sizes[j]) = (sizes[j], sizes[i]);
        }
        int requested = sizes.Count;
        if (requested == 0) yield break;
        var reservations = towerSites;
        reservations.Clear();
        var stacked = new HashSet<BookItem>();
        Physics.SyncTransforms();
        int cursor = 0;
        for (int tower = 0; tower < requested; tower++)
        {
            if (YieldForFrameBudget()) yield return null;
            int count = sizes[tower];
            int first = cursor;
            cursor += count;
            float yaw = Random.Range(0f, 360f);
            float radius = 0f, height = 0f;
            for (int i = first; i < first + count; i++)
            {
                var book = books[i];
                Vector3 heading = Quaternion.Euler(0f, yaw + Random.Range(-Mathf.Min(1f, towerYawJitter), Mathf.Min(1f, towerYawJitter)), 0f) * Vector3.forward;
                book.transform.rotation = book.GetAlignedRotation(Vector3.up, heading);
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
            // Broad books form the base, narrower books the top.
            books.Sort(first, count, Comparer<BookItem>.Create((a, b) =>
                (BookBounds(b).size.x * BookBounds(b).size.z).CompareTo(BookBounds(a).size.x * BookBounds(a).size.z)));
            bool found = false;
            Collider floorSupport = null;
            Vector3 basePoint = default;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vector3 candidate = SampleSpawnPosition(radius);
                if (!InsideArea(candidate, radius) || !Ground(candidate + Vector3.up * 0.1f, out var floor)) continue;
                candidate.y = floor.point.y;
                bool clear = true;
                foreach (var reservation in reservations)
                    // Kuleler seride dengeli dagilsin: birbirine yapisik kule kumeleri olusmaz.
                    if (Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(reservation.x, reservation.z)) <
                        radius + reservation.w + (attempt < 96 ? towerGapMeters : 0.1f))
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
                if (NearProp(candidate, new Vector3(radius, 0f, radius), floor.collider)) continue;
                basePoint = candidate; floorSupport = floor.collider; found = true; break;
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
            Physics.SyncTransforms();
            Collider support = floorSupport;
            for (int i = first; i < first + count; i++)
            {
                var book = books[i];
                spawnedTowerBooks.Add(book);
                book.InitializeSpawnSupport(support);
                support = book.GetComponentInChildren<Collider>();
                var rigidbody = book.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.solverIterations = 16;
                    rigidbody.solverVelocityIterations = 8;
                }
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
        Debug.Log($"BookSpawner: kule {reservations.Count}/{requested}, kule kitabi {stacked.Count}");
        if (reservations.Count < requested) Debug.LogWarning($"BookSpawner: {requested} kuleden {reservations.Count} tanesi sigdi; kalan kitaplar daginik.");
    }

    // ---------------- Seritte dengeli yerlesim ----------------
    private struct BandCell { public Vector3 point; public Collider floor; public float axisYaw; }

    /// <summary>
    /// Raf onu seridini kitap boyutunda hucrelere boler (uzun kenar rafa paralel). Her hucre gercek
    /// zeminde, raf/duvar gibi engellerden ve kulelerden uzak. Yurume yolu ve kitaplik aralarindaki
    /// gecitler seridin disinda kaldigi icin hucre almaz.
    /// </summary>
    private List<BandCell> BuildBandCells(float alongSpacing, float acrossSpacing)
    {
        var cells = new List<BandCell>();
        var taken = new HashSet<long>();
        int cellTotal = 0, cellBand = 0, cellArea = 0, cellTower = 0, cellGround = 0, cellBlocked = 0;
        float startY = spawnHeight;
        foreach (var zone in corridorAreas)
            if (zone != null && zone.gameObject.activeInHierarchy) { startY = zone.transform.position.y + spawnHeight; break; }
        float dedupe = Mathf.Min(alongSpacing, acrossSpacing) * 0.8f;
        const float probe = 0.12f;
        foreach (var f in shelfFootprints)
        {
            if (f.longAlongX != dominantAlongX) continue;
            float a0 = f.longAlongX ? f.min.x : f.min.y, a1 = f.longAlongX ? f.max.x : f.max.y;
            float axisYaw = f.longAlongX ? 90f : 0f;
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; ; row++)
                {
                    float across = shelfFrontClearance + acrossSpacing * 0.5f + row * acrossSpacing;
                    if (across > bookAreaDepthMeters - acrossSpacing * 0.25f) break;
                    // Satirlar yarim hucre kaydirilir: duz izgara gorunmesin.
                    float shift = (row & 1) == 1 ? alongSpacing * 0.5f : 0f;
                    for (float along = a0 + alongSpacing * 0.5f + shift; along <= a1 - alongSpacing * 0.5f + 0.001f; along += alongSpacing)
                    {
                        float c = side < 0 ? (f.longAlongX ? f.min.y : f.min.x) - across
                                           : (f.longAlongX ? f.max.y : f.max.x) + across;
                        Vector3 p = f.longAlongX ? new Vector3(along, startY, c) : new Vector3(c, startY, along);
                        cellTotal++;
                        if (!InShelfBand(p, probe)) { cellBand++; continue; }
                        // Koridor kutulari raf onlerine tam uzanmiyor; hucre kutunun biraz disina
                        // tasabilir (zemin + engel kontrolu yine de duvar/raf icini eler).
                        if (!InsideAreaRelaxed(p, 1.2f)) { cellArea++; continue; }
                        long key = CellKey(Mathf.FloorToInt(p.x / dedupe), Mathf.FloorToInt(p.z / dedupe));
                        if (taken.Contains(key)) continue;
                        bool nearTower = false;
                        foreach (var t in towerSites)
                            if (new Vector2(p.x - t.x, p.z - t.z).magnitude < t.w + Mathf.Max(alongSpacing, acrossSpacing) * 0.5f)
                            { nearTower = true; break; }
                        if (nearTower) { cellTower++; continue; }
                        if (!Ground(p, out var floor)) { cellGround++; continue; }
                        Vector3 half = f.longAlongX
                            ? new Vector3(alongSpacing * 0.45f, 0.1f, acrossSpacing * 0.45f)
                            : new Vector3(acrossSpacing * 0.45f, 0.1f, alongSpacing * 0.45f);
                        int count = Physics.OverlapBoxNonAlloc(new Vector3(p.x, floor.point.y + 0.14f, p.z), half,
                            overlapBuffer, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                        bool blocked = false;
                        for (int o = 0; o < count; o++)
                            if (overlapBuffer[o] != floor.collider && overlapBuffer[o].GetComponentInParent<BookItem>() == null)
                            { blocked = true; break; }
                        if (!blocked) blocked = NearProp(new Vector3(p.x, floor.point.y, p.z), half, floor.collider);
                        if (blocked) { cellBlocked++; continue; }
                        taken.Add(key);
                        cells.Add(new BandCell { point = new Vector3(p.x, floor.point.y, p.z), floor = floor.collider, axisYaw = axisYaw });
                    }
                }
        }
        Debug.Log($"BookSpawner hucre: toplam {cellTotal}, serit disi {cellBand}, alan disi {cellArea}, kule {cellTower}, " +
                  $"zemin yok {cellGround}, engel {cellBlocked} -> {cells.Count} hucre ({alongSpacing:0.00} x {acrossSpacing:0.00} m)");
        return cells;
    }

    /// <summary>
    /// Tezgah, masa, dekor gibi engellerin cevresinde gecis payi (propClearance) birakilir;
    /// duvar/zemin/tavan gibi buyuk kabuklar, kitapliklar ve kitaplar sayilmaz.
    /// </summary>
    private bool NearProp(Vector3 floorPoint, Vector3 half, Collider floor)
    {
        int count = Physics.OverlapBoxNonAlloc(floorPoint + Vector3.up * 0.6f,
            new Vector3(half.x + propClearance, 0.45f, half.z + propClearance), overlapBuffer, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int o = 0; o < count; o++)
        {
            var col = overlapBuffer[o];
            if (col == floor || shelfColliders.Contains(col) || col.GetComponentInParent<BookItem>() != null) continue;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue; // oyuncular vb.
            if (col is CharacterController) continue;
            Vector3 size = col.bounds.size;
            if (Mathf.Max(size.x, size.z) > 8f) continue; // duvar, zemin, tavan, oda kabugu
            return true;
        }
        return false;
    }

    /// <summary>
    /// Daginik kitaplarin birakildigi her serit parcasini (kitaplik yuzu + yol tarafi + iki uc)
    /// gecici gorunmez duvarla kapatir: dusen kitaplar yurume yoluna ve gecitlere yuvarlanmaz.
    /// Acilis ekrani bitince silinir.
    /// </summary>
    private void CreateDropWalls()
    {
        if (dropWalls != null) Destroy(dropWalls);
        dropWalls = new GameObject("Book Drop Walls (temporary)");
        float startY = spawnHeight;
        foreach (var zone in corridorAreas)
            if (zone != null && zone.gameObject.activeInHierarchy) { startY = zone.transform.position.y + spawnHeight; break; }
        const float thick = 0.2f, height = 3.2f;
        foreach (var f in shelfFootprints)
        {
            if (f.longAlongX != dominantAlongX) continue;
            float a0 = f.longAlongX ? f.min.x : f.min.y, a1 = f.longAlongX ? f.max.x : f.max.y;
            for (int side = -1; side <= 1; side += 2)
            {
                float face = side < 0 ? (f.longAlongX ? f.min.y : f.min.x) : (f.longAlongX ? f.max.y : f.max.x);
                float midAcross = face + side * bookAreaDepthMeters * 0.5f;
                Vector3 probe = f.longAlongX ? new Vector3((a0 + a1) * 0.5f, startY, midAcross) : new Vector3(midAcross, startY, (a0 + a1) * 0.5f);
                if (!InsideAreaRelaxed(probe, 0f) || !Ground(probe, out var floor)) continue;
                float y = floor.point.y + height * 0.5f;
                // (along merkez, across merkez, along boy, across boy)
                AddDropWall(f.longAlongX, (a0 + a1) * 0.5f, face + side * (bookAreaDepthMeters + thick * 0.5f), a1 - a0 + 2f * thick, thick, y, height);
                AddDropWall(f.longAlongX, a0 - thick * 0.5f, midAcross, thick, bookAreaDepthMeters, y, height);
                AddDropWall(f.longAlongX, a1 + thick * 0.5f, midAcross, thick, bookAreaDepthMeters, y, height);
            }
        }
    }

    private void AddDropWall(bool alongX, float along, float across, float alongSize, float acrossSize, float y, float height)
    {
        var wall = new GameObject("Wall");
        wall.transform.SetParent(dropWalls.transform, false);
        wall.transform.position = alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
        var box = wall.AddComponent<BoxCollider>();
        box.size = alongX ? new Vector3(alongSize, height, acrossSize) : new Vector3(acrossSize, height, alongSize);
    }

    private static float Percentile(List<float> values, float q)
    {
        if (values.Count == 0) return 0f;
        values.Sort();
        return values[Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * q), 0, values.Count - 1)];
    }

    // Yerdeki bir kitabin ustten gorunen dondurulmus dikdortgeni.
    private struct LooseRect { public Vector2 center, axis, half; public float top; public int book; }

    private static bool RectsOverlap(in LooseRect a, in LooseRect b)
    {
        Vector2 a2 = new Vector2(-a.axis.y, a.axis.x), b2 = new Vector2(-b.axis.y, b.axis.x);
        Vector2 d = b.center - a.center;
        for (int k = 0; k < 4; k++)
        {
            Vector2 n = k == 0 ? a.axis : k == 1 ? a2 : k == 2 ? b.axis : b2;
            float ra = a.half.x * Mathf.Abs(Vector2.Dot(a.axis, n)) + a.half.y * Mathf.Abs(Vector2.Dot(a2, n));
            float rb = b.half.x * Mathf.Abs(Vector2.Dot(b.axis, n)) + b.half.y * Mathf.Abs(Vector2.Dot(b2, n));
            if (Mathf.Abs(Vector2.Dot(d, n)) > ra + rb) return false;
        }
        return true;
    }

    private static bool RectContains(in LooseRect r, Vector2 point, float inset)
    {
        Vector2 d = point - r.center;
        Vector2 side = new Vector2(-r.axis.y, r.axis.x);
        return Mathf.Abs(Vector2.Dot(d, r.axis)) <= r.half.x - inset && Mathf.Abs(Vector2.Dot(d, side)) <= r.half.y - inset;
    }

    private static Bounds RectBounds(in LooseRect r, float bottom)
    {
        Vector2 side = new Vector2(-r.axis.y, r.axis.x);
        float ex = Mathf.Abs(r.axis.x) * r.half.x + Mathf.Abs(side.x) * r.half.y;
        float ez = Mathf.Abs(r.axis.y) * r.half.x + Mathf.Abs(side.y) * r.half.y;
        return new Bounds(new Vector3(r.center.x, (bottom + r.top) * 0.5f, r.center.y),
            new Vector3(ex * 2f, Mathf.Max(0.001f, r.top - bottom), ez * 2f));
    }

    /// <summary>
    /// Daginik kitaplar serit icinde RASTGELE konum ve yonle, dogal yiginlar halinde dagilir.
    /// Kurallar: kitabin dort kosesi de seridin icinde (yurume yoluna, kitaplik uclarindaki
    /// gecitlere tasmaz); ustune bindigi kitabin tam ustunde durur (devrilmez, yerinde donmus baslar);
    /// yiginlar alcak kalir (kule olmaz); kulelere yaslanmaz.
    /// </summary>
    private IEnumerator PlaceBooksInBand(List<BookItem> books, System.Action<bool> done)
    {
        var loose = new List<BookItem>(books.Count);
        foreach (var book in books)
            if (!spawnedTowerBooks.Contains(book)) loose.Add(book);
        if (loose.Count == 0 || shelfFootprints.Count == 0 || !gatherInFrontOfShelves) { done(false); yield break; }

        // Olculer: uzun kenar +Z (yaw 0) iken dunya kutusu.
        foreach (var book in loose) book.transform.rotation = book.GetAlignedRotation(Vector3.up, Vector3.forward);
        Physics.SyncTransforms();
        int n = loose.Count;
        var lengths = new float[n]; var widths = new float[n]; var heights = new float[n];
        var offsets = new Vector3[n];
        var widthList = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            Bounds b = BookBounds(loose[i]);
            lengths[i] = b.size.z; widths[i] = b.size.x; heights[i] = b.size.y;
            offsets[i] = b.center - loose[i].transform.position;
            widthList.Add(b.size.x);
        }
        // Ornekleme hucreleri seridin tamamini kaplar (kitap boyundan kucuk).
        float sampleSpacing = Mathf.Clamp(Percentile(widthList, 0.5f) * 0.6f, 0.2f, 0.5f);
        var cells = BuildBandCells(sampleSpacing, sampleSpacing);
        if (cells.Count == 0) { done(false); yield break; }

        placedGrid.Clear();
        var gridBounds = new List<Bounds>(books.Count);
        var rects = new List<LooseRect>(books.Count);
        void AddRect(LooseRect rect, float bottom) { rects.Add(rect); GridAdd(gridBounds, RectBounds(rect, bottom)); }
        // Kuleler engel: ustlerine/yanlarina daginik kitap binmez.
        foreach (var book in books)
        {
            if (!spawnedTowerBooks.Contains(book)) continue;
            Bounds b = BookBounds(book);
            AddRect(new LooseRect { center = new Vector2(b.center.x, b.center.z), axis = Vector2.up,
                half = new Vector2(b.extents.z + 0.05f, b.extents.x + 0.05f), top = b.max.y, book = -1 }, b.min.y);
        }

        var restOrder = new List<BookItem>(n);
        var restSupport = new List<Collider>(n);
        var floorOf = new List<float>(n);
        var bookRect = new List<int>(n);
        var bookSpin = new List<Quaternion>(n);
        var bookK = new List<int>(n);
        int piled = 0, fallback = 0, leaning = 0;
        for (int k = 0; k < n; k++)
        {
            if (YieldForFrameBudget())
            { ShopLoadingScreen.Progress(0.55f + 0.3f * k / n); yield return null; }
            var book = loose[k];
            Vector2 half = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f);
            bool found = false;
            LooseRect best = default;
            float bestBottom = float.MaxValue, bestYaw = 0f;
            int bestSupport = -1;
            BandCell bestCell = default;
            int valid = 0;
            bool bestLean = false;
            Vector2 bestLeanDir = Vector2.zero;
            float bestLeanHeight = 0f;
            // Yarisinda en alcak aday (zemin dolsun), yarisinda rastgele aday: yiginlar ve bosluklar
            // karisik, karman corman bir dagilim.
            bool takeLowest = Random.value < 0.7f;
            // Birkac gecerli aday icinden en alcagi: once zemin dolar, yiginlar kendiliginden
            // ve duzensiz olusur; yine de hic bir yer bos kalip baska yer kuleye donmez.
            for (int attempt = 0; attempt < 220 && valid < 4; attempt++)
            {
                // Ilk 140 deneme seridin herhangi bir yeri; sonra mevcut bir daginik kitabin ustunde
                // rastgele bir nokta (kaymis, donuk yonlu yigin), daha yuksek yigina izin verilerek.
                bool overPile = attempt >= 140;
                var cell = cells[Random.Range(0, cells.Count)];
                Vector2 c;
                if (!overPile)
                    c = new Vector2(cell.point.x + Random.Range(-0.5f, 0.5f) * sampleSpacing,
                                    cell.point.z + Random.Range(-0.5f, 0.5f) * sampleSpacing);
                else
                {
                    if (restOrder.Count == 0) break;
                    int ri = Random.Range(0, rects.Count);
                    if (rects[ri].book < 0) continue;
                    var baseRect = rects[ri];
                    Vector2 baseSide = new Vector2(-baseRect.axis.y, baseRect.axis.x);
                    c = baseRect.center + baseRect.axis * (Random.Range(-0.5f, 0.5f) * baseRect.half.x)
                                        + baseSide * (Random.Range(-0.5f, 0.5f) * baseRect.half.y);
                    cell.point.y = floorOf[baseRect.book];
                }
                float yaw = Random.Range(0f, 360f);
                Vector2 axis = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                var rect = new LooseRect { center = c, axis = axis, half = half };
                // Dort kose de seritte: kitap yurume yoluna / gecide tasmaz.
                Vector2 side = new Vector2(-axis.y, axis.x);
                bool inside = true;
                for (int q = 0; q < 4 && inside; q++)
                {
                    Vector2 corner = c + axis * (q < 2 ? half.x : -half.x) + side * ((q & 1) == 0 ? half.y : -half.y);
                    Vector3 corner3 = new Vector3(corner.x, cell.point.y + 0.5f, corner.y);
                    inside = InShelfBand(corner3, 0.02f) && InsideAreaRelaxed(corner3, 1.2f);
                }
                if (!inside) continue;
                float floorY = cell.point.y;
                float top = floorY;
                int support = -1;
                bool blocked = false;
                foreach (int i in GridQuery(RectBounds(rect, floorY)))
                {
                    var other = rects[i];
                    if (!RectsOverlap(rect, other)) continue;
                    if (other.book < 0) { blocked = true; break; }
                    if (other.top > top) { top = other.top; support = i; }
                }
                if (blocked) continue;
                // Ustune bindigi kitabin ortasinda durmali; kenarda kalan kitap ya o kitaba
                // YASLANIR (bir kenari yerde, egik) ya da bu aday atlanir.
                bool lean = false;
                Vector2 leanDir = Vector2.zero;
                float leanHeight = 0f;
                if (support >= 0 && !RectContains(rects[support], c, 0.04f))
                {
                    if (dropLooseBooks || rects[support].book < 0 || top - floorY > heights[k] * 2.3f || Random.value >= bookLeanChance) continue;
                    lean = true;
                    leanHeight = top - floorY;
                    leanDir = rects[support].center - c;
                    if (leanDir.sqrMagnitude < 0.0001f) continue;
                    leanDir.Normalize();
                }
                if (!lean && top - floorY > (overPile ? maxLoosePileHeight * 1.6f : maxLoosePileHeight) - heights[k]) continue;
                valid++;
                float bottom = lean ? floorY : top;
                bool take = takeLowest ? bottom < bestBottom : Random.Range(0, valid) == 0;
                if (take)
                {
                    best = rect; bestBottom = bottom; bestYaw = yaw; bestSupport = lean ? -1 : support; bestCell = cell; found = true;
                    bestLean = lean; bestLeanDir = leanDir; bestLeanHeight = leanHeight;
                }
            }
            if (!found)
            {
                // Yer kalmadiysa: mevcut daginik bir kitabin tam ustune, ayni yonde.
                fallback++;
                int pick = -1;
                for (int t = 0; t < 40; t++)
                {
                    int i = Random.Range(0, rects.Count);
                    if (rects[i].book >= 0 && (pick < 0 || rects[i].top < rects[pick].top)) pick = i;
                }
                if (pick < 0) { pick = 0; while (pick < rects.Count && rects[pick].book < 0) pick++; }
                if (pick >= rects.Count)
                {
                    // Henuz daginik kitap yok: rastgele bir serit hucresinin zeminine.
                    bestCell = cells[Random.Range(0, cells.Count)];
                    bestYaw = Random.Range(0f, 360f);
                    best = new LooseRect { center = new Vector2(bestCell.point.x, bestCell.point.z),
                        axis = new Vector2(Mathf.Sin(bestYaw * Mathf.Deg2Rad), Mathf.Cos(bestYaw * Mathf.Deg2Rad)), half = half };
                    bestBottom = bestCell.point.y; bestSupport = -1;
                }
                else
                {
                var baseRect = rects[pick];
                bestBottom = baseRect.top; bestSupport = pick;
                bestCell = cells[0];
                bestCell.point.y = floorOf[baseRect.book];
                // Duzgun ust uste dizilmesin: kaymis merkez ve rastgele yon; kitap seridin disina
                // tasmayan ilk dizilim kullanilir, bulunamazsa alttakinin hizasinda kalir.
                bestYaw = Mathf.Atan2(baseRect.axis.x, baseRect.axis.y) * Mathf.Rad2Deg + Random.Range(-10f, 10f);
                best = new LooseRect { center = baseRect.center, half = half,
                    axis = new Vector2(Mathf.Sin(bestYaw * Mathf.Deg2Rad), Mathf.Cos(bestYaw * Mathf.Deg2Rad)) };
                Vector2 baseSide = new Vector2(-baseRect.axis.y, baseRect.axis.x);
                for (int t = 0; t < 25; t++)
                {
                    float yaw = Random.Range(0f, 360f);
                    Vector2 axis = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                    Vector2 c = baseRect.center + baseRect.axis * (Random.Range(-0.4f, 0.4f) * baseRect.half.x)
                                                + baseSide * (Random.Range(-0.4f, 0.4f) * baseRect.half.y);
                    Vector2 side = new Vector2(-axis.y, axis.x);
                    bool inside = true;
                    for (int q = 0; q < 4 && inside; q++)
                    {
                        Vector2 corner = c + axis * (q < 2 ? half.x : -half.x) + side * ((q & 1) == 0 ? half.y : -half.y);
                        Vector3 corner3 = new Vector3(corner.x, bestCell.point.y + 0.5f, corner.y);
                        inside = InShelfBand(corner3, 0.02f) && InsideAreaRelaxed(corner3, 1.2f);
                    }
                    if (!inside) continue;
                    best = new LooseRect { center = c, axis = axis, half = half };
                    bestYaw = yaw;
                    break;
                }
                }
            }
            Quaternion spin = Quaternion.Euler(0f, bestYaw, 0f);
            Quaternion flat = book.GetAlignedRotation(Vector3.up, spin * Vector3.forward);
            if (bestLean)
            {
                // Bir kenari yerde, diger kenari yandaki kitabin ustunde: egik, dogal duran kitap.
                Vector2 side2 = new Vector2(-best.axis.y, best.axis.x);
                float lever = Mathf.Abs(Vector2.Dot(best.axis, bestLeanDir)) * half.x + Mathf.Abs(Vector2.Dot(side2, bestLeanDir)) * half.y;
                float tilt = Mathf.Min(38f, Mathf.Atan2(bestLeanHeight, Mathf.Max(0.05f, 2f * lever)) * Mathf.Rad2Deg);
                Quaternion lift = Quaternion.AngleAxis(-tilt, Vector3.Cross(Vector3.up, new Vector3(bestLeanDir.x, 0f, bestLeanDir.y)));
                book.transform.rotation = lift * flat;
                float centerY = bestBottom + bestLeanHeight * 0.5f + heights[k] * 0.5f * Mathf.Cos(tilt * Mathf.Deg2Rad) + 0.004f;
                book.transform.position = new Vector3(best.center.x, centerY, best.center.y) - lift * (spin * offsets[k]);
                best.top = bestBottom + bestLeanHeight + heights[k];
                best.book = -2; // egik kitabin ustune/yanina baska kitap binmez
                AddRect(best, bestBottom);
                leaning++;
                restOrder.Add(book);
                restSupport.Add(null); // fizik bir an oturtur (donmus baslamaz)
                floorOf.Add(bestCell.point.y);
                bookRect.Add(rects.Count - 1); bookSpin.Add(spin); bookK.Add(k);
                continue;
            }
            best.top = bestBottom + heights[k];
            best.book = restOrder.Count;
            book.transform.rotation = flat;
            book.transform.position = new Vector3(best.center.x, bestBottom + heights[k] * 0.5f + 0.002f, best.center.y) - spin * offsets[k];
            AddRect(best, bestBottom);
            Collider supportCollider = bestCell.floor;
            if (bestSupport >= 0)
            {
                piled++;
                int below = rects[bestSupport].book;
                supportCollider = below >= 0 ? restOrder[below].GetComponentInChildren<Collider>() : null;
            }
            restOrder.Add(book);
            restSupport.Add(supportCollider);
            floorOf.Add(bestCell.point.y);
            bookRect.Add(rects.Count - 1); bookSpin.Add(spin); bookK.Add(k);
        }
        int frozen = 0;
        if (dropLooseBooks)
        {
            // Duz yerlesimden birakma pozuna: her kitap altindakilerin birakma yuksekliginin ustunde,
            // rastgele egik. Katlar arasinda bosluk oldugu icin baslangicta hic bir kitap digerinin
            // icinde degil; dusup devrilince fizik gercek, ic ice gecmeyen bir karmasa uretir.
            const float maxTilt = 22f;
            var raisedTop = new float[rects.Count];
            droppedBodies.Clear();
            for (int i = 0; i < rects.Count; i++) raisedTop[i] = rects[i].top;
            for (int r = 0; r < restOrder.Count; r++)
            {
                int ri = bookRect[r];
                var rect = rects[ri];
                float baseY = floorOf[r];
                foreach (int j in GridQuery(RectBounds(rect, floorOf[r])))
                {
                    if (j == ri || (rects[j].book >= 0 && j > ri) || !RectsOverlap(rect, rects[j])) continue;
                    baseY = Mathf.Max(baseY, raisedTop[j]);
                }
                // Egik kitabin dikey yari boyu (uzun kenar * sin + kalinlik * cos): katlar arasinda
                // tam o kadar bosluk; dusus alcak kalir (hizli dusen ince kitap zemini delmesin).
                float angle = Random.Range(-maxTilt, maxTilt);
                float halfSpan = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * rect.half.x
                                 + Mathf.Cos(angle * Mathf.Deg2Rad) * heights[bookK[r]] * 0.5f + 0.012f;
                float centerY = baseY + halfSpan + Random.Range(0f, 0.05f);
                raisedTop[ri] = centerY + halfSpan;
                Quaternion tilt = Quaternion.AngleAxis(angle, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right);
                var book = restOrder[r];
                if (book.TryGetComponent<Rigidbody>(out var dropBody))
                {
                    droppedBodies.Add(new DroppedBody { body = dropBody, mode = dropBody.collisionDetectionMode,
                        depenetration = dropBody.maxDepenetrationVelocity });
                    // Dusus sirasinda kitaplar ince zemini/duvari delip dukkandan cikmasin ve
                    // birbirine degince firlamasin.
                    dropBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    dropBody.maxDepenetrationVelocity = 1.5f;
                }
                book.transform.rotation = tilt * book.transform.rotation;
                book.transform.position = new Vector3(rect.center.x, centerY, rect.center.y) - tilt * (bookSpin[r] * offsets[bookK[r]]);
            }
            Physics.SyncTransforms();
            CreateDropWalls();
        }
        else
        {
            Physics.SyncTransforms();
            for (int i = 0; i < restOrder.Count; i++)
                if (restSupport[i] != null && restOrder[i].InitializeSpawnSupport(restSupport[i])) frozen++;
        }
        placedGrid.Clear();
        Debug.Log($"BookSpawner: {n} daginik kitap seride dagitildi ({n - piled - leaning} zeminde, {piled} yiginda, {leaning} egik, {fallback} yedek, {frozen} donmus); " +
                  $"{spawnedTowerBooks.Count} kitap {towerSites.Count} kulede.");
        done(true);
    }

    private IEnumerator SeparateInitialBooks(List<BookItem> books)
    {
        bool handled = false;
        var bandPlacement = PlaceBooksInBand(books, ok => handled = ok);
        while (bandPlacement.MoveNext()) yield return bandPlacement.Current;
        if (handled) yield break;
        Physics.SyncTransforms();
        placedGrid.Clear();
        var placed = new List<Bounds>(books.Count);
        // Towers stay where they were authored; reserve their physical volume first.
        int towerCount = 0;
        foreach (var book in books)
            if (spawnedTowerBooks.Contains(book)) { GridAdd(placed, BookBounds(book)); towerCount++; }
        var placedBooks = new List<BookItem>(books.Count);
        for (int i = 0; i < towerCount; i++) placedBooks.Add(null); // towers already have support
        // Each scattered book remembers what it rests on so it can start frozen (no 3000-body settle).
        var restOrder = new List<BookItem>(books.Count);
        var restSupport = new List<Collider>(books.Count);
        int unresolved = 0, processed = 0;
        int rejArea = 0, rejTower = 0, rejOccupied = 0, rejGround = 0, rejHigh = 0, rejBlocked = 0;
        var blockers = new Dictionary<string, int>();
        foreach (var book in books)
        {
            processed++;
            if (YieldForFrameBudget())
            { ShopLoadingScreen.Progress(0.55f + 0.3f * processed / books.Count); yield return null; }
            if (spawnedTowerBooks.Contains(book)) continue;
            Bounds original = BookBounds(book);
            Vector3 offset = original.center - book.transform.position;
            float radius = new Vector2(original.extents.x, original.extents.z).magnitude;
            bool found = false;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vector3 candidate = SampleSpawnPosition(radius);
                if (!InsideArea(candidate, radius)) { rejArea++; continue; }
                // Ucuz 2B doluluk kontrolu once: pahali zemin isini dolu noktalar icin atma.
                Bounds flat = new Bounds(new Vector3(candidate.x, 0f, candidate.z), new Vector3(original.size.x, 1000f, original.size.z));
                Bounds near = flat; near.Expand(new Vector3(0.12f, 0f, 0.12f));
                bool towerOverlap = false, occupied = false;
                foreach (int i in GridQuery(near))
                {
                    Bounds other = placed[i];
                    bool overlapX = Mathf.Abs(flat.center.x - other.center.x) < flat.extents.x + other.extents.x;
                    bool overlapZ = Mathf.Abs(flat.center.z - other.center.z) < flat.extents.z + other.extents.z;
                    if (i < towerCount &&
                        Mathf.Abs(flat.center.x - other.center.x) < flat.extents.x + other.extents.x + 0.06f &&
                        Mathf.Abs(flat.center.z - other.center.z) < flat.extents.z + other.extents.z + 0.06f)
                    { towerOverlap = true; break; }
                    if (overlapX && overlapZ) occupied = true;
                }
                if (towerOverlap) { rejTower++; continue; }
                // Yogun seritte cok deneme yapmadan kisa katmanlar olusur (raf onu yigini).
                // Once serbest zemin aranir; yigin yalnizca serit gercekten doluysa olusur.
                if (occupied && attempt < 100) { rejOccupied++; continue; }
                if (!Ground(candidate + Vector3.up * 0.1f, out var floor)) { rejGround++; continue; }
                Bounds test = new Bounds(new Vector3(candidate.x, floor.point.y + original.extents.y + 0.003f, candidate.z), original.size);
                // Dense areas may use shallow layers, never a new accidental tower.
                int restingOn = -1;
                bool raised;
                var nearby = new List<int>(GridQuery(near));
                do
                {
                    raised = false;
                    foreach (int i in nearby)
                    {
                        Bounds other = placed[i];
                        if (!test.Intersects(other)) continue;
                        test.center = new Vector3(test.center.x, other.max.y + test.extents.y + 0.003f, test.center.z);
                        restingOn = i;
                        raised = true;
                    }
                } while (raised);
                if (test.min.y - floor.point.y > 0.1f) { rejHigh++; continue; }
                bool blocked = false;
                int overlapCount = Physics.OverlapBoxNonAlloc(test.center, test.extents, overlapBuffer, Quaternion.identity,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int o = 0; o < overlapCount; o++)
                    if (overlapBuffer[o].GetComponentInParent<BookItem>() == null)
                    {
                        blocked = true;
                        string key = overlapBuffer[o].name;
                        blockers[key] = blockers.TryGetValue(key, out int n) ? n + 1 : 1;
                        break;
                    }
                if (blocked) { rejBlocked++; continue; }
                book.transform.position = test.center - offset;
                GridAdd(placed, test);
                placedBooks.Add(book);
                BookItem below = restingOn >= 0 ? placedBooks[restingOn] : null;
                restOrder.Add(book);
                restSupport.Add(restingOn < 0 ? floor.collider
                    : below != null ? below.GetComponentInChildren<Collider>() : null);
                found = true;
                break;
            }
            if (!found) { GridAdd(placed, original); placedBooks.Add(book); unresolved++; }
        }
        Physics.SyncTransforms();
        // Lower books were placed first, so each support is frozen before the book above it.
        int frozen = 0;
        for (int i = 0; i < restOrder.Count; i++)
            if (restSupport[i] != null && restOrder[i].InitializeSpawnSupport(restSupport[i])) frozen++;
        placedGrid.Clear();
        int outOfBand = 0;
        foreach (var book in restOrder)
            if (book != null && gatherInFrontOfShelves && shelfFootprints.Count > 0 && !InShelfBand(book.transform.position, 0f)) outOfBand++;
        Debug.Log($"BookSpawner: toplam {books.Count} kitap | kule kitabi {towerCount} | daginik {books.Count - towerCount} " +
                  $"(yerlesen {restOrder.Count}, donmus {frozen}, yer bulunamayan {unresolved}, serit disi {outOfBand}) | raf izi {shelfFootprints.Count}");
        Debug.Log($"BookSpawner ret: alan {rejArea}, kule {rejTower}, dolu {rejOccupied}, zemin {rejGround}, yuksek {rejHigh}, engel {rejBlocked} [" +
                  string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(blockers, kv => kv.Value), 6), kv => kv.Key + ":" + kv.Value)) + "]");
        if (unresolved > 0) Debug.LogWarning($"BookSpawner: {unresolved} kitap icin cakismasiz yer bulunamadi. Alan cok dar veya zemin eksik; alan/adet ayarini kontrol et.", this);
    }

    public Vector3[] CreateSpawnPositions(int count)
    {
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        int guaranteed = corridorAreas == null ? 0 : corridorAreas.Length;
        if (count < guaranteed || count < 0) throw new System.ArgumentException("At least one book per corridor is required.");
        var positions = new Vector3[count];
        for (int i = 0; i < count; i++) positions[i] = i < guaranteed ? SampleArea(corridorAreas[i]) : SampleSpawnPosition();
        return positions;
    }

    public Vector3 SampleSpawnPosition() => SampleSpawnPosition(0.15f);

    public Vector3 SampleSpawnPosition(float radius)
    {
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            bool useBand = gatherInFrontOfShelves && shelfFootprints.Count > 0;
            Vector3 fallback = Vector3.zero;
            bool hasFallback = false;
            float total = 0;
            foreach (var zone in corridorAreas) total += ZoneWeight(zone);
            if (total <= 0) throw new System.InvalidOperationException("BookSpawner: corridor areas have no usable space. Fix their Size/Scale; legacy area was not used.");
            for (int attempt = 0; attempt < (useBand ? 2048 : 256); attempt++)
            {
                float choice = Random.value * total;
                BoxCollider selected = null;
                foreach (var zone in corridorAreas)
                {
                    float weight = ZoneWeight(zone);
                    if (weight <= 0) continue;
                    selected = zone; choice -= weight;
                    if (choice <= 0) break;
                }
                Vector3 point = SampleArea(selected);
                int coverage = 0;
                foreach (var zone in corridorAreas)
                {
                    if (ZoneWeight(zone) <= 0f) continue;
                    Vector3 p = zone.transform.InverseTransformPoint(point) - zone.center;
                    Vector3 scale = zone.transform.lossyScale;
                    if (Mathf.Abs(p.x) <= zone.size.x * 0.5f - corridorEdgePadding / Mathf.Abs(scale.x) &&
                        Mathf.Abs(p.z) <= zone.size.z * 0.5f - corridorEdgePadding / Mathf.Abs(scale.z)) coverage++;
                }
                if (Random.value >= 1f / Mathf.Max(1, coverage)) continue;
                if (!useBand) return point;
                if (!hasFallback) { fallback = point; hasFallback = true; }
                if (InShelfBand(point, radius)) return point;
            }
            if (hasFallback) return fallback; // Seritte yer yoksa koridor icinde bir nokta.
            throw new System.InvalidOperationException("Could not sample corridor union.");
        }
        Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
        return area.TransformPoint(new Vector3(Random.Range(-areaSize.x*.5f,areaSize.x*.5f),spawnHeight,Random.Range(-areaSize.y*.5f,areaSize.y*.5f)));
    }
    Vector3 SampleArea(BoxCollider selected)
    {
        Vector3 scale = selected.transform.lossyScale;
        float halfX = selected.size.x * .5f - corridorEdgePadding / Mathf.Abs(scale.x);
        float halfZ = selected.size.z * .5f - corridorEdgePadding / Mathf.Abs(scale.z);
        return selected.transform.TransformPoint(selected.center + new Vector3(Random.Range(-halfX, halfX), 0,
            Random.Range(-halfZ, halfZ))) + Vector3.up * spawnHeight;
    }

    // Only the named scene group is discovered, never arbitrary gameplay colliders.
    public int DiscoverCorridors()
    {
        var zones = new List<BoxCollider>(corridorAreas ?? System.Array.Empty<BoxCollider>());
        int added = 0;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != "Book Spawn Corridors") continue;
            foreach (var zone in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (zones.Contains(zone)) continue;
                int vacant = zones.FindIndex(candidate => candidate == null);
                if (vacant >= 0) zones[vacant] = zone; else zones.Add(zone);
                added++;
            }
        }
        corridorAreas = zones.ToArray();
        return added;
    }

    public bool ValidateSpawnAreas(out string error)
    {
        if (!Finite(spawnHeight) || spawnHeight < 0 || !Finite(corridorEdgePadding) || corridorEdgePadding < 0)
        { error = "Spawn Height / Edge Padding must be finite and non-negative."; return false; }
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            var seen = new HashSet<BoxCollider>();
            for (int i = 0; i < corridorAreas.Length; i++)
            {
                var zone = corridorAreas[i];
                string reason = zone == null ? "missing reference" : !zone.gameObject.activeInHierarchy ? "inactive GameObject" :
                    zone.gameObject.scene != gameObject.scene ? "different scene" : !seen.Add(zone) ? "duplicate area" :
                    !Finite(zone.center.sqrMagnitude) || !Finite(zone.transform.position.sqrMagnitude) ||
                    !Finite(zone.transform.lossyScale.sqrMagnitude) || !Finite(ZoneWeight(zone)) || ZoneWeight(zone) <= 0 ? "Size/Scale too small after Edge Padding" :
                    Vector3.Dot(zone.transform.up, Vector3.up) < .999f ? "area must be horizontal (Y rotation is supported)" : null;
                if (reason == null) continue;
                error = $"Corridor [{i}] '{(zone != null ? zone.name : "Missing")}': {reason}. No books spawned.";
                return false;
            }
        }
        else
        {
            Transform area = v16SpawnArea != null ? v16SpawnArea : transform;
            if (areaSize.x <= 0 || areaSize.y <= 0 || !Finite(areaSize.sqrMagnitude) ||
                Mathf.Abs(area.lossyScale.x * area.lossyScale.z) < .00001f)
            { error = "Legacy spawn area has invalid Size or zero Scale. Assign corridor areas."; return false; }
        }
        error = null; return true;
    }

    void ValidateConfiguration(bool networked)
    {
        DiscoverCorridors();
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        if (copiesPerBook < 1 || BookTypeCount < 1) throw new System.InvalidOperationException("Book catalogue/count is empty.");
        if (corridorAreas != null && corridorAreas.Length > BookTypeCount * copiesPerBook)
            throw new System.InvalidOperationException("There must be at least one book per corridor.");
        var ids = new HashSet<int>();
        for (int i = 0; i < BookTypeCount; i++)
        {
            var data = bookTypes != null && i < bookTypes.Length ? bookTypes[i] : null;
            if (bookTypes != null && bookTypes.Length > 0 && data == null)
                throw new System.InvalidOperationException($"Book catalogue [{i}] is missing.");
            int id = data != null ? data.BookID : i;
            var prefab = data != null && data.bookPrefab != null ? data.bookPrefab : bookPrefab;
            if (id < 0 || !ids.Add(id) || prefab == null || prefab.GetComponent<BookItem>() == null ||
                (networked && (prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetworkBook>() == null)))
                throw new System.InvalidOperationException($"Invalid BookID/prefab at catalogue [{i}] (ID {id}). Nothing spawned.");
        }
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    float ZoneWeight(BoxCollider zone)
    {
        if (!zone || !zone.gameObject.activeInHierarchy) return 0;
        Vector3 scale = zone.transform.lossyScale;
        return Mathf.Max(0,zone.size.x*Mathf.Abs(scale.x)-2*corridorEdgePadding) *
               Mathf.Max(0,zone.size.z*Mathf.Abs(scale.z)-2*corridorEdgePadding);
    }
    void OnDrawGizmosSelected()
    {
        if (corridorAreas == null) return;
        var old = Gizmos.matrix;
        foreach (var zone in corridorAreas)
        {
            if (!zone) continue;
            Gizmos.matrix = zone.transform.localToWorldMatrix;
            Gizmos.color = new Color(.15f,1f,.35f,.15f); Gizmos.DrawCube(zone.center,zone.size);
            Gizmos.color = Color.green; Gizmos.DrawWireCube(zone.center,zone.size);
        }
        Gizmos.matrix = old;
    }

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
