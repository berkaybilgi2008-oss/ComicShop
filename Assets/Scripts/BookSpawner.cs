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
    [Tooltip("Acikken kule yok: kitaplar fotograftaki gibi YIGINLAR halinde (ortasi yuksek, kenarlari alcak, " +
             "kitaplar hafif kaymis/donmus) dizilir. Fizik dususu yok; yerlesim aninda biter, her kitap yerinde donmus baslar.")]
    public bool heapLayoutMode = true;
    [Tooltip("Bir yigin sutununun en fazla kitap sayisi.")]
    [Min(2)] public int heapMaxColumnBooks = 14;
    [Tooltip("Kucuk kulelerin en fazla kitap sayisi (6..bu deger).")]
    [Min(2)] public int smallTowerMaxBooks = 10;
    [Tooltip("Buyuk kulelerin en fazla kitap sayisi (14..bu deger).")]
    [Min(2)] public int largeTowerMaxBooks = 20;
    [Tooltip("Her kitap icin kule yonunden rastgele sapma (derece).")]
    [Range(0f, 180f)] public float towerYawJitter = 8f;
    [Tooltip("Kuleler arasinda birakilan en az bosluk (metre); kuleler serit boyunca dengeli dagilir.")]
    [Min(0.1f)] public float towerGapMeters = 1.1f;
    [Tooltip("Kitaplik uclarinda (karsi koridora gecis, raf sirasi sonu) bos birakilan gecit uzunlugu (metre).")]
    [Min(0f)] public float endPassageMeters = 1.0f;
    [Tooltip("Tezgah/masa gibi engellerin cevresinde bos birakilan gecis payi (metre).")]
    [Min(0f)] public float propClearanceMeters = 0.6f;
    [Tooltip("Daginik kitaplarin bir kismi yandaki kitaba yaslanir (egik durur).")]
    [Range(0f, 1f)] public float bookLeanChance = 0.85f;
    // Daginik yiginlarin en fazla yuksekligi: kuleye donusmesin.
    private const float maxLoosePileHeight = 0.7f;

    private readonly HashSet<BookItem> spawnedTowerBooks = new HashSet<BookItem>();
    private readonly List<Vector4> towerSites = new List<Vector4>();
    private GameObject dropWalls;
    private struct DroppedBody { public Rigidbody body; public CollisionDetectionMode mode; public float depenetration; }
    private readonly List<DroppedBody> droppedBodies = new List<DroppedBody>();

    /// <summary>Yerlesim (dusus) suruyorken true: gercekci carpma etkileri bu surede kapali.</summary>
    public static bool LayoutInProgress { get; private set; }

    /// <summary>
    /// Dusus fizigini acilis ekraninin arkasinda HIZLANDIRILMIS calistirir (Physics.Simulate):
    /// kitaplar gercek fizikle devrilip durur, oyuncu saniyelerce beklemez. Sonra yalnizca
    /// gercekten duran ve alti dolu olan kitaplar, alttan uste, oldugu yerde dondurulur.
    /// </summary>
    private IEnumerator FastForwardDrop()
    {
        const float step = 0.02f;
        var entries = new List<(Rigidbody body, BookItem item)>(sessionBooks.Count);
        foreach (var go in sessionBooks)
        {
            if (go == null || !go.TryGetComponent<Rigidbody>(out var rb) || rb.isKinematic) continue;
            entries.Add((rb, go.GetComponent<BookItem>()));
        }
        // Alttan uste dalgalar: ayni anda binlerce dinamik govde simule etmek cok yavasti (oyuncu
        // 20+ sn bekliyordu). Her dalga ~300 kitap; o dalga durunca yerinde donar, sonra ustundeki
        // dalga onun ustune duser. Bekleyen dalgalar havada kinematik bekler.
        entries.Sort((a, b) => a.body.position.y.CompareTo(b.body.position.y));
        var saved = new Dictionary<Rigidbody, (float linear, float angular, float sleep)>(entries.Count);
        foreach (var e in entries)
        {
            saved[e.body] = (e.body.linearDamping, e.body.angularDamping, e.body.sleepThreshold);
            e.body.isKinematic = true;
        }
        var previousMode = Physics.simulationMode;
        LayoutInProgress = true;
        Transform ignore = dropWalls != null ? dropWalls.transform : null;
        int waveSize = Mathf.Clamp(entries.Count / 8 + 1, 150, 400);
        int frozen = 0;
        float simulatedTotal = 0f;
        var active = new List<(Rigidbody body, BookItem item)>(waveSize * 2);
        ShopLoadingScreen.Settling(0f);
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            for (int start = 0; start < entries.Count; start += waveSize)
            {
                for (int i = start; i < Mathf.Min(entries.Count, start + waveSize); i++)
                {
                    var e = entries[i];
                    if (e.body == null) continue;
                    e.body.isKinematic = false;
                    // Dusus sirasinda biraz daha sonumlu: cabuk durulur, yine de devrilir/kayar.
                    e.body.linearDamping = Mathf.Max(e.body.linearDamping, 0.35f);
                    e.body.angularDamping = Mathf.Max(e.body.angularDamping, 0.8f);
                    e.body.sleepThreshold = Mathf.Max(e.body.sleepThreshold, 0.04f);
                    e.body.WakeUp();
                    active.Add(e);
                }
                float simulated = 0f;
                int quiet = 0;
                while (simulated < 2.2f)
                {
                    double began = Time.realtimeSinceStartupAsDouble;
                    while (Time.realtimeSinceStartupAsDouble - began < 0.08 && simulated < 2.2f)
                    {
                        Physics.Simulate(step);
                        simulated += step;
                    }
                    int moving = 0;
                    foreach (var e in active)
                    {
                        if (e.body == null || e.body.isKinematic || e.body.IsSleeping()) continue;
                        if (e.body.linearVelocity.sqrMagnitude > 0.0025f || e.body.angularVelocity.sqrMagnitude > 0.02f) moving++;
                    }
                    ShopLoadingScreen.Settling(Mathf.Clamp01((start + (float)waveSize * Mathf.Min(1f, simulated / 1.2f)) / entries.Count) * 0.97f);
                    if (simulated >= 0.4f && moving <= active.Count / 50) { if (++quiet >= 2) break; } else quiet = 0;
                    yield return null;
                }
                simulatedTotal += simulated;
                // Bu dalgadan duranlar (ve onceki dalgalardan kalanlar) alttan uste donar.
                Physics.SyncTransforms();
                active.Sort((a, b) => a.body.position.y.CompareTo(b.body.position.y));
                for (int pass = 0; pass < 6; pass++)
                {
                    int changed = 0;
                    foreach (var e in active)
                        if (e.item != null && e.item.FreezeWhereResting(ignore)) changed++;
                    frozen += changed;
                    if (changed == 0) break;
                }
                active.RemoveAll(e => e.body == null || e.body.isKinematic);
            }
        }
        finally
        {
            Physics.simulationMode = previousMode;
            foreach (var pair in saved)
            {
                if (pair.Key == null) continue;
                pair.Key.linearDamping = pair.Value.linear;
                pair.Key.angularDamping = pair.Value.angular;
                pair.Key.sleepThreshold = pair.Value.sleep;
            }
        }
        Debug.Log($"BookSpawner: dusus {simulatedTotal:0.0} sn (dalgalar halinde, hizlandirilmis) simule edildi; " +
                  $"{frozen} kitap yerinde donduruldu, {active.Count} serbest.");
        EndDrop();
        LayoutInProgress = false;
        ShopLoadingScreen.Settling(1f);
    }

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
        if (now - frameBudgetStarted < 0.05) return false;
        frameBudgetStarted = now;
        return true;
    }
    private bool sessionSpawned;
    private readonly List<GameObject> sessionBooks = new List<GameObject>();

    // When corridor areas are assigned, they are the complete spawn authority. In this mode
    // books must never be relocated by the shelf-band / heap / tower layout systems.
    private bool CorridorOnlySpawn => corridorAreas != null && corridorAreas.Length > 0;

    // Tum oyuncularda (host ve istemci) sahne yuklenince calisir: raf gozleri kopya sayisini alsin.
    void Awake() => ShelfSlot.MatchCapacityToCopies(copiesPerBook);

    // ---------------- Raf onu seridi ----------------
    private struct ShelfFootprint { public Vector2 min, max; public bool longAlongX; public bool passMin, passMax; public float minY; }
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
                longAlongX = bounds.size.x >= bounds.size.z,
                minY = bounds.min.y
            });
        }
        int alongX = 0;
        foreach (var f in shelfFootprints) if (f.longAlongX) alongX++;
        dominantAlongX = alongX * 2 > shelfFootprints.Count;
        // Kitaplik ucundaki aralik GERCEK bir gecit mi (karsi koridora yurunebiliyor mu)? Duvar kenarindaki
        // yan yana kitapliklarin arasi duvara cikar: orasi gecit degil, yigin kesintisiz devam eder.
        int passages = 0;
        for (int i = 0; i < shelfFootprints.Count; i++)
        {
            var f = shelfFootprints[i];
            f.passMin = IsPassage(f, true);
            f.passMax = IsPassage(f, false);
            if (f.passMin) passages++;
            if (f.passMax) passages++;
            shelfFootprints[i] = f;
        }
        Debug.Log($"BookSpawner: {shelfFootprints.Count} kitaplik, {passages} gercek uc gecidi (digerlerinde yigin kesintisiz).");
    }

    private static readonly RaycastHit[] passageHits = new RaycastHit[32];
    private bool IsPassage(ShelfFootprint f, bool atMin)
    {
        float along = atMin ? (f.longAlongX ? f.min.x : f.min.y) - 0.35f : (f.longAlongX ? f.max.x : f.max.y) + 0.35f;
        float acrossMin = f.longAlongX ? f.min.y : f.min.x, acrossMax = f.longAlongX ? f.max.y : f.max.x;
        float length = acrossMax - acrossMin + 1.6f;
        Vector3 dir = f.longAlongX ? Vector3.forward : Vector3.right;
        float probeY = float.NaN;
        // Tavandan degil kitaplik tabaninin biraz ustunden asagi: zemini bul.
        Vector3 groundProbe = f.longAlongX ? new Vector3(along, f.minY + 1.0f, acrossMin - 0.8f) : new Vector3(acrossMin - 0.8f, f.minY + 1.0f, along);
        if (Ground(groundProbe, out var floor)) probeY = floor.point.y;
        if (float.IsNaN(probeY)) probeY = f.minY;
        // Iki yonden de (tek yuzlu duvar mesh'leri arkadan gorunmez) ve iki yukseklikte tara.
        foreach (float h in new[] { 0.5f, 1.2f })
            for (int d = 0; d < 2; d++)
            {
                float fromAcross = d == 0 ? acrossMin - 0.8f : acrossMax + 0.8f;
                Vector3 start = f.longAlongX ? new Vector3(along, probeY + h, fromAcross) : new Vector3(fromAcross, probeY + h, along);
                int n = Physics.RaycastNonAlloc(start, d == 0 ? dir : -dir, passageHits, length, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                    if (passageHits[k].collider != null && passageHits[k].collider.GetComponentInParent<BookItem>() == null) return false;
            }
        return true;
    }

    // Nokta bir kitapligin UZUN yuzunun onundeki seritte mi? Kitaplik uclarindaki gecitler haric.
    private bool InShelfBand(Vector3 point, float radius, float spill = 0f, float depthSpill = float.NaN)
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
            // Gecit olmayan uc (duvar kenarinda yan yana kitapliklar): serit komsu kitapliginkine ulasir.
            float alongMin = (f.longAlongX ? f.min.x : f.min.y) + radius + 0.1f - spill - (f.passMin ? 0f : 0.9f);
            float alongMax = (f.longAlongX ? f.max.x : f.max.y) - radius - 0.1f + spill + (f.passMax ? 0f : 0.9f);
            if (along < alongMin || along > alongMax) continue;
            float distance = f.longAlongX ? Mathf.Max(f.min.y - p.y, p.y - f.max.y) : Mathf.Max(f.min.x - p.x, p.x - f.max.x);
            if (distance >= shelfFrontClearance + radius && distance <= bookAreaDepthMeters - radius + (float.IsNaN(depthSpill) ? spill : depthSpill)) inBand = true;
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
            if (beyond < 0f || beyond > endPassageMeters + radius - spill) continue;
            if (along < gMin ? !g.passMin : !g.passMax) continue; // gecit degil: bos birakma
            float across = g.longAlongX ? Mathf.Max(g.min.y - p.y, p.y - g.max.y) : Mathf.Max(g.min.x - p.x, p.x - g.max.x);
            if (across <= bookAreaDepthMeters + spill) return false;
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
    private string loadPhase = "";
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
                double stepStart = Time.realtimeSinceStartupAsDouble;
                try { more = routine.MoveNext(); }
                catch (System.Exception error) { SpawnError = error; }
                double stepMs = (Time.realtimeSinceStartupAsDouble - stepStart) * 1000.0;
                if (stepMs > 120.0) Debug.Log($"[Takilma] Yukleme adimi {stepMs:F0} ms ({loadPhase}).");
                if (SpawnError != null || !more) break;
                yield return routine.Current;
                frameBudgetStarted = Time.realtimeSinceStartupAsDouble;
            }
            sessionSpawned = SpawnError == null;
            // Acilis ekrani kitaplar yere inip durana kadar kalir; dusme animasyonu gorunmez.
            // Tur (ve sayac) ancak bu bittikten sonra baslar.
            if (sessionSpawned)
            {
                if (dropWalls != null)
                {
                    var fast = FastForwardDrop();
                    while (fast.MoveNext()) yield return fast.Current;
                }
                else
                {
                    var settle = WaitForBooksToSettle();
                    while (settle.MoveNext()) yield return settle.Current;
                }
            }
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose();
            EndDrop();
            LayoutInProgress = false;
            if (Physics.simulationMode == SimulationMode.Script) Physics.simulationMode = SimulationMode.FixedUpdate;
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
        loadPhase = "raf izleri";
        if (!CorridorOnlySpawn) BuildShelfFootprints();
        loadPhase = "kitap olusturma";
        sessionBooks.Clear();
        {
            // One guaranteed book per assigned area, remaining books weighted by usable area.
            // Yigin/dagitim modunda tum kitaplar zaten sonradan yerlestirilir: baslangic konumu icin
            // pahali serit ornekleme (3000 kitap x binlerce deneme, TEK karede) Play Solo'da oyunu
            // saniyelerce donduruyordu. Ucuz rastgele koridor noktasi yeterli.
            Vector3[] positions;
            if (heapLayoutMode && corridorAreas != null && corridorAreas.Length > 0)
            {
                positions = new Vector3[ids.Count];
                for (int i = 0; i < positions.Length; i++) positions[i] = SampleArea(corridorAreas[i % corridorAreas.Length]);
            }
            else positions = CreateSpawnPositions(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                SpawnSingleBook(ids[i], positions[i]);
                if (YieldForFrameBudget()) { ShopLoadingScreen.Progress((float)i / ids.Count * 0.45f); yield return null; }
            }
            var books = new List<BookItem>(sessionBooks.Count);
            foreach (var book in sessionBooks) books.Add(book.GetComponent<BookItem>());
            spawnedTowerBooks.Clear();
            towerSites.Clear();
            if (!CorridorOnlySpawn)
            {
                if (!heapLayoutMode)
                {
                    var towers = ArrangeTowers(books);
                    while (towers.MoveNext()) yield return towers.Current;
                }
                ShopLoadingScreen.Progress(0.55f);
                loadPhase = "yerlesim";
                var scattered = SeparateInitialBooks(books);
                while (scattered.MoveNext()) yield return scattered.Current;
            }
            else
            {
                // Corridor-only mode is intentionally a hard stop: the four corridor areas
                // are the final positions. Do not move books to shelf fronts, heaps or towers.
                Physics.SyncTransforms();
                ShopLoadingScreen.Progress(0.85f);
                Debug.Log($"BookSpawner: Corridor-only spawn aktif; {books.Count} kitap dort corridor alaninda birakildi. Raf/kule yerlesimi atlandi.");
            }
            int published = 0;
            loadPhase = "ag yayini";
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

    private static Bounds BookVisualBounds(BookItem book)
    {
        Bounds result = default;
        bool found = false;
        if (book.coverRenderer != null && book.coverRenderer.enabled)
        {
            // Kapak ile ayni nesnedeki / altindaki ana mesh'ler (dis cizgi kopyalari haric).
            foreach (var r in book.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.name.IndexOf("outline", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!found) { result = r.bounds; found = true; } else result.Encapsulate(r.bounds);
            }
        }
        else
        {
            foreach (var r in book.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.name.IndexOf("outline", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!found) { result = r.bounds; found = true; } else result.Encapsulate(r.bounds);
            }
        }
        return found ? result : BookBounds(book);
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
    private struct BandCell { public Vector3 point; public Collider floor; public float axisYaw; public float across; public Vector2 outward; }

    /// <summary>
    /// Raf onu seridini kitap boyutunda hucrelere boler (uzun kenar rafa paralel). Her hucre gercek
    /// zeminde, raf/duvar gibi engellerden ve kulelerden uzak. Yurume yolu ve kitaplik aralarindaki
    /// gecitler seridin disinda kaldigi icin hucre almaz.
    /// </summary>
    private List<BandCell> BuildBandCells(float alongSpacing, float acrossSpacing)
    {
        var cells = new List<BandCell>();
        var taken = new Dictionary<long, List<Vector2>>();
        int cellTotal = 0, cellBand = 0, cellArea = 0, cellTower = 0, cellGround = 0, cellBlocked = 0;
        float startY = spawnHeight;
        foreach (var zone in corridorAreas)
            if (zone != null && zone.gameObject.activeInHierarchy) { startY = zone.transform.position.y + spawnHeight; break; }
        float dedupe = Mathf.Max(alongSpacing, acrossSpacing);
        const float probe = 0.12f;
        foreach (var f in shelfFootprints)
        {
            if (f.longAlongX != dominantAlongX) continue;
            float a0 = f.longAlongX ? f.min.x : f.min.y, a1 = f.longAlongX ? f.max.x : f.max.y;
            if (!f.passMin) a0 -= 0.9f;
            if (!f.passMax) a1 += 0.9f;
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
                        // Sirt sirta kitapliklarin seritleri ayni yeri iki kez uretir (2 cm kaymis): yakin
                        // hucreyi mesafeyle ele (eski kova karsilastirmasi sinirda kaciriyordu ve ust uste
                        // binen sutunlar kitaplari yerlestiremiyordu).
                        int gx = Mathf.FloorToInt(p.x / dedupe), gz = Mathf.FloorToInt(p.z / dedupe);
                        long key = CellKey(gx, gz);
                        bool duplicate = false;
                        for (int dx = -1; dx <= 1 && !duplicate; dx++)
                            for (int dz = -1; dz <= 1 && !duplicate; dz++)
                                if (taken.TryGetValue(CellKey(gx + dx, gz + dz), out var list))
                                    foreach (var q in list)
                                    {
                                        float ax = Mathf.Abs(q.x - p.x), az = Mathf.Abs(q.y - p.z);
                                        float needX = f.longAlongX ? alongSpacing : acrossSpacing, needZ = f.longAlongX ? acrossSpacing : alongSpacing;
                                        if (ax < needX * 0.98f && az < needZ * 0.98f) { duplicate = true; break; }
                                    }
                        if (duplicate) continue;
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
                        if (!taken.TryGetValue(key, out var bucket)) taken[key] = bucket = new List<Vector2>(2);
                        bucket.Add(new Vector2(p.x, p.z));
                        cells.Add(new BandCell { point = new Vector3(p.x, floor.point.y, p.z), floor = floor.collider, axisYaw = axisYaw, across = across,
                            outward = f.longAlongX ? new Vector2(0f, side) : new Vector2(side, 0f) });
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
            new Vector3(half.x + propClearanceMeters, 0.45f, half.z + propClearanceMeters), overlapBuffer, Quaternion.identity,
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
            for (int attempt = 0; attempt < 90 && valid < 2; attempt++)
            {
                // Ilk 140 deneme seridin herhangi bir yeri; sonra mevcut bir daginik kitabin ustunde
                // rastgele bir nokta (kaymis, donuk yonlu yigin), daha yuksek yigina izin verilerek.
                bool overPile = attempt >= 55;
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

    /// <summary>
    /// Kitaplari gercek bir kitap yigini gibi dizer (referans fotograf): seritte rastgele "tepe"
    /// merkezleri secilir; her sutunun yuksekligi yakindaki tepelere gore belirlenir (ortasi yuksek,
    /// kenarlara dogru alcalan, duzensiz). Sutundaki her kitap bir alttakinin tam ustunde, hafif
    /// kaymis ve donmus durur; ic ice gecme yok, havada kalan yok, hepsi yerinde donmus baslar.
    /// </summary>
    private IEnumerator PlaceBooksAsHeaps(List<BookItem> books, System.Action<bool> done)
    {
        var loose = new List<BookItem>(books.Count);
        foreach (var book in books) if (!spawnedTowerBooks.Contains(book)) loose.Add(book);
        if (loose.Count == 0 || shelfFootprints.Count == 0 || !gatherInFrontOfShelves) { done(false); yield break; }

        foreach (var book in loose) book.transform.rotation = book.GetAlignedRotation(Vector3.up, Vector3.forward);
        Physics.SyncTransforms();
        int n = loose.Count;
        var lengths = new float[n]; var widths = new float[n]; var heights = new float[n];
        var offsets = new Vector3[n];
        var lengthList = new List<float>(n); var widthList = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            Bounds b = BookBounds(loose[i]);
            lengths[i] = b.size.z; widths[i] = b.size.x; heights[i] = b.size.y;
            offsets[i] = b.center - loose[i].transform.position;
            lengthList.Add(b.size.z); widthList.Add(b.size.x);
        }
        const float yawJitter = 13f, shiftJitter = 0.035f;
        float sin = Mathf.Sin(yawJitter * Mathf.Deg2Rad), cos = Mathf.Cos(yawJitter * Mathf.Deg2Rad);
        float bigL = Percentile(lengthList, 0.95f), bigW = Percentile(widthList, 0.95f);
        float alongSpacing = bigL * cos + bigW * sin + 2f * shiftJitter + 0.02f;
        float acrossSpacing = bigW * cos + bigL * sin + 2f * shiftJitter + 0.02f;
        var cells = BuildBandCells(alongSpacing, acrossSpacing);
        if (cells.Count == 0) { done(false); yield break; }

        // Yukseklik alani: rastgele tepeler (ortasi yuksek), her hucreye biraz duzensizlik.
        int heapCount = Mathf.Max(1, cells.Count / 9);
        var centers = new List<Vector4>(heapCount); // x, z, yaricap, genlik
        for (int h = 0; h < heapCount; h++)
        {
            var c = cells[Random.Range(0, cells.Count)];
            centers.Add(new Vector4(c.point.x, c.point.z, Random.Range(0.9f, 1.7f), Random.Range(0.55f, 1f)));
        }
        var field = new float[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            float f = 0f;
            foreach (var h in centers)
            {
                float d = new Vector2(cells[i].point.x - h.x, cells[i].point.z - h.y).magnitude / h.z;
                if (d < 1f) f += h.w * (1f - d * d);
            }
            field[i] = f * Random.Range(0.75f, 1.15f);
        }
        int cap = Mathf.Max(2, heapMaxColumnBooks);
        // Toplam kitap sayisini tutturan olcek (ikili arama).
        float lo = 0f, hi = 400f;
        for (int iter = 0; iter < 40; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            int total = 0;
            foreach (float f in field) total += Mathf.Min(cap, Mathf.RoundToInt(f * mid));
            if (total < n) lo = mid; else hi = mid;
        }
        var target = new int[cells.Count];
        int assigned = 0;
        for (int i = 0; i < cells.Count; i++) { target[i] = Mathf.Min(cap, Mathf.RoundToInt(field[i] * hi)); assigned += target[i]; }
        // Artan/eksik kitaplari rastgele sutunlara dagit (bos hucreye degil, var olan yiginlara).
        var order = new List<int>(cells.Count);
        for (int i = 0; i < cells.Count; i++) order.Add(i);
        while (assigned < n)
        {
            int i = order[Random.Range(0, order.Count)];
            if ((target[i] == 0 && Random.value < 0.7f) || (target[i] >= cap && Random.value < 0.95f)) continue;
            target[i]++; assigned++;
        }
        while (assigned > n)
        {
            int i = order[Random.Range(0, order.Count)];
            if (target[i] == 0) continue;
            target[i]--; assigned--;
        }

        // Kitaplari sutunlara ver: buyuk kitaplar alta.
        var bookOrder = new List<int>(n);
        for (int i = 0; i < n; i++) bookOrder.Add(i);
        for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (bookOrder[i], bookOrder[j]) = (bookOrder[j], bookOrder[i]); }

        placedGrid.Clear();
        var gridBounds = new List<Bounds>(n);
        var rects = new List<LooseRect>(n);
        var restOrder = new List<BookItem>(n);
        var restSupport = new List<Collider>(n);
        var spare = new List<int>();
        var columnTops = new List<int>(); // her sutunun en ustteki kitabinin rect indeksi
        int cursor = 0, skipped = 0, processed = 0;
        for (int ci = 0; ci < cells.Count; ci++)
        {
            if (target[ci] == 0) continue;
            var cell = cells[ci];
            var column = bookOrder.GetRange(cursor, Mathf.Min(target[ci], n - cursor));
            cursor += column.Count;
            column.Sort((a, b) => (lengths[b] * widths[b]).CompareTo(lengths[a] * widths[a]));
            float flipBase = Random.value < 0.5f ? 0f : 180f;
            int below = -1;
            foreach (int k in column)
            {
                processed++;
                if (YieldForFrameBudget()) { ShopLoadingScreen.Progress(0.55f + 0.3f * processed / n); yield return null; }
                bool placedOk = false;
                for (int attempt = 0; attempt < 4 && !placedOk; attempt++)
                {
                    float jitterScale = attempt == 0 ? 1f : attempt == 1 ? 0.5f : 0f;
                    float yaw = cell.axisYaw + flipBase + (Random.value < 0.25f ? 180f : 0f) + Random.Range(-yawJitter, yawJitter) * jitterScale;
                    Vector2 shift = Random.insideUnitCircle * shiftJitter * jitterScale;
                    Vector2 center = new Vector2(cell.point.x, cell.point.z) + shift;
                    Vector2 axis = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                    var rect = new LooseRect { center = center, axis = axis, half = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f) };
                    float top = cell.point.y;
                    int support = -1;
                    foreach (int i in GridQuery(RectBounds(rect, cell.point.y)))
                    {
                        if (!RectsOverlap(rect, rects[i])) continue;
                        if (rects[i].top > top) { top = rects[i].top; support = i; }
                    }
                    // Ustune bindigi kitap bu sutunun bir alttaki kitabi olmali ve ortasini tasimali.
                    if (support >= 0 && (support != below || !RectContains(rects[support], center, 0.03f))) continue;
                    if (support < 0 && below >= 0) continue;
                    rect.top = top + heights[k];
                    rect.book = restOrder.Count;
                    Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
                    var book = loose[k];
                    book.transform.rotation = book.GetAlignedRotation(Vector3.up, spin * Vector3.forward);
                    book.transform.position = new Vector3(center.x, top + heights[k] * 0.5f + 0.0015f, center.y) - spin * offsets[k];
                    rects.Add(rect);
                    GridAdd(gridBounds, RectBounds(rect, top));
                    restOrder.Add(book);
                    restSupport.Add(support >= 0 ? restOrder[rects[support].book].GetComponentInChildren<Collider>() : cell.floor);
                    below = rects.Count - 1;
                    placedOk = true;
                }
                if (!placedOk) { spare.Add(k); skipped++; }
            }
            if (below >= 0) columnTops.Add(below);
        }
        // Sigmayanlar: rastgele secilen alcak sutunlarin EN USTUNE (hic kitap disarida/havada kalmaz).
        foreach (int k in spare)
        {
            if (columnTops.Count == 0) break;
            int pick = -1;
            float yaw = 0f;
            Vector2 axis = Vector2.up;
            for (int t = 0; t < 40 && pick < 0; t++)
            {
                int c = Random.Range(0, columnTops.Count);
                var top = rects[columnTops[c]];
                float tryYaw = Mathf.Atan2(top.axis.x, top.axis.y) * Mathf.Rad2Deg + (t < 20 ? Random.Range(-yawJitter, yawJitter) : 0f);
                Vector2 tryAxis = new Vector2(Mathf.Sin(tryYaw * Mathf.Deg2Rad), Mathf.Cos(tryYaw * Mathf.Deg2Rad));
                var probe = new LooseRect { center = top.center, axis = tryAxis, half = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f) };
                // Komsu sutun daha yuksekse ve bu kitap ona tasiyorsa ic ice girerdi: baska sutun sec.
                bool clash = false;
                foreach (int i in GridQuery(RectBounds(probe, top.top)))
                    if (i != columnTops[c] && rects[i].top > top.top + 0.001f && RectsOverlap(probe, rects[i])) { clash = true; break; }
                if (clash) continue;
                pick = c; yaw = tryYaw; axis = tryAxis;
            }
            if (pick < 0)
            {
                // Hic uygun yer yoksa en alcak sutunun tam hizasinda.
                pick = 0;
                for (int c = 1; c < columnTops.Count; c++) if (rects[columnTops[c]].top < rects[columnTops[pick]].top) pick = c;
                var lowest = rects[columnTops[pick]];
                yaw = Mathf.Atan2(lowest.axis.x, lowest.axis.y) * Mathf.Rad2Deg; axis = lowest.axis;
            }
            var baseRect = rects[columnTops[pick]];
            var rect = new LooseRect { center = baseRect.center, axis = axis, half = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f),
                top = baseRect.top + heights[k], book = restOrder.Count };
            Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
            var book = loose[k];
            book.transform.rotation = book.GetAlignedRotation(Vector3.up, spin * Vector3.forward);
            book.transform.position = new Vector3(rect.center.x, baseRect.top + heights[k] * 0.5f + 0.0015f, rect.center.y) - spin * offsets[k];
            rects.Add(rect);
            GridAdd(gridBounds, RectBounds(rect, baseRect.top));
            restOrder.Add(book);
            restSupport.Add(restOrder[baseRect.book].GetComponentInChildren<Collider>());
            columnTops[pick] = rects.Count - 1;
        }
        Physics.SyncTransforms();
        int frozen = 0;
        for (int i = 0; i < restOrder.Count; i++)
            if (restSupport[i] != null && restOrder[i].InitializeSpawnSupport(restSupport[i])) frozen++;
        placedGrid.Clear();
        int columns = 0, tallest = 0;
        foreach (int t in target) { if (t > 0) columns++; tallest = Mathf.Max(tallest, t); }
        Debug.Log($"BookSpawner: {n} kitap {heapCount} yigin / {columns} sutun halinde dizildi (en yuksek sutun {tallest}, " +
                  $"{skipped} yedek); {frozen} kitap yerinde donmus.");
        done(true);
    }

    /// <summary>
    /// Karman corman dagilim: seridin her yerinde COK sayida KUCUK yigin (1-6 kitap). Her kitap
    /// rastgele yonde (0-360) ve yiginin ortasindan rastgele kaymis durur; ust uste binenler
    /// yalnizca alttakinin ortasina oturabildiginde biner (devrilmez, havada kalmaz), ic ice gecme
    /// yok (ustten bakis carpisma testi), her kitabin dort kosesi alan icinde. Hepsi yerinde donmus.
    /// </summary>
    private IEnumerator PlaceBooksAsPiles(List<BookItem> books, System.Action<bool> done)
    {
        var loose = new List<BookItem>(books.Count);
        foreach (var book in books) if (!spawnedTowerBooks.Contains(book)) loose.Add(book);
        if (loose.Count == 0 || shelfFootprints.Count == 0 || !gatherInFrontOfShelves) { done(false); yield break; }

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
        float sample = Mathf.Clamp(Percentile(widthList, 0.5f) * 0.6f, 0.2f, 0.5f);
        var cells = BuildBandCells(sample, sample);
        if (cells.Count == 0) { done(false); yield break; }

        placedGrid.Clear();
        var gridBounds = new List<Bounds>(n);
        var rects = new List<LooseRect>(n);
        var restOrder = new List<BookItem>(n);
        var restSupport = new List<Collider>(n);
        var floors = new List<float>(n);
        // Yigin: merkez (x,z), zemin y, hedef kitap sayisi, simdiki sayi.
        var piles = new List<(Vector2 center, float floor, Collider floorCollider, int target, int count)>();
        const float maxPileHeight = 0.5f;
        var covered = new List<bool>(n);
        int onFloor = 0, stacked = 0, forced = 0;

        bool TryPlace(int k, Vector2 center, float floorY, Collider floorCollider, out int support, out LooseRect rect, out float bottom, out float yaw, bool ignoreHeight = false)
        {
            yaw = Random.Range(0f, 360f);
            Vector2 axis = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            rect = new LooseRect { center = center, axis = axis, half = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f) };
            support = -1; bottom = floorY;
            Vector2 side = new Vector2(-axis.y, axis.x);
            for (int q = 0; q < 4; q++)
            {
                Vector2 corner = center + axis * (q < 2 ? rect.half.x : -rect.half.x) + side * ((q & 1) == 0 ? rect.half.y : -rect.half.y);
                Vector3 corner3 = new Vector3(corner.x, floorY + 0.5f, corner.y);
                if (!InShelfBand(corner3, 0.02f) || !InsideAreaRelaxed(corner3, 1.2f)) return false;
            }
            foreach (int i in GridQuery(RectBounds(rect, floorY)))
            {
                if (!RectsOverlap(rect, rects[i])) continue;
                if (rects[i].top > bottom) { bottom = rects[i].top; support = i; }
            }
            if (support >= 0 && !RectContains(rects[support], center, 0.05f)) return false;
            if (!ignoreHeight && bottom - floorY > maxPileHeight - heights[k]) return false;
            return true;
        }

        void Commit(int k, LooseRect rect, float bottom, float yaw, int support, float floorY, Collider floorCollider)
        {
            rect.top = bottom + heights[k];
            rect.book = restOrder.Count;
            Quaternion spin = Quaternion.Euler(0f, yaw, 0f);
            var book = loose[k];
            book.transform.rotation = book.GetAlignedRotation(Vector3.up, spin * Vector3.forward);
            book.transform.position = new Vector3(rect.center.x, bottom + heights[k] * 0.5f + 0.0015f, rect.center.y) - spin * offsets[k];
            rects.Add(rect);
            covered.Add(false);
            if (support >= 0) covered[support] = true;
            GridAdd(gridBounds, RectBounds(rect, bottom));
            restOrder.Add(book);
            restSupport.Add(support >= 0 ? restOrder[rects[support].book].GetComponentInChildren<Collider>() : floorCollider);
            floors.Add(floorY);
            if (support >= 0) stacked++; else onFloor++;
        }

        for (int k = 0; k < n; k++)
        {
            if (YieldForFrameBudget()) { ShopLoadingScreen.Progress(0.55f + 0.3f * k / n); yield return null; }
            bool placedOk = false;
            for (int attempt = 0; attempt < 70 && !placedOk; attempt++)
            {
                // Cogu kitap yeni ya da henuz dolmamis kucuk bir yigina gider; yiginlar dagitik ve alcak.
                bool joinPile = piles.Count > 0 && Random.value < 0.62f;
                int pileIndex = -1;
                Vector2 anchor; float floorY; Collider floorCollider;
                if (joinPile)
                {
                    pileIndex = Random.Range(0, piles.Count);
                    if (piles[pileIndex].count >= piles[pileIndex].target) { joinPile = false; pileIndex = -1; } // dolu: yeni nokta
                }
                if (joinPile)
                {
                    var pile = piles[pileIndex];
                    anchor = pile.center + Random.insideUnitCircle * (widths[k] * 0.42f); // kaykin, hizasiz
                    floorY = pile.floor; floorCollider = pile.floorCollider;
                }
                else
                {
                    var cell = cells[Random.Range(0, cells.Count)];
                    anchor = new Vector2(cell.point.x, cell.point.z) + Random.insideUnitCircle * sample * 0.5f;
                    floorY = cell.point.y; floorCollider = cell.floor;
                }
                if (!TryPlace(k, anchor, floorY, floorCollider, out int support, out var rect, out float bottom, out float yaw)) continue;
                Commit(k, rect, bottom, yaw, support, floorY, floorCollider);
                if (pileIndex >= 0) { var pile = piles[pileIndex]; pile.count++; piles[pileIndex] = pile; }
                else piles.Add((anchor, floorY, floorCollider, Random.Range(1, 7), 1));
                placedOk = true;
            }
            if (placedOk) continue;
            // Yer kalmadiysa: TEK bir kuleyi buyutmek yerine en ALCAK acik yiginlarin ustune, hafif
            // kaydirarak ve rastgele acida koy. Fazla kitaplar tum yiginlara esit ve daginik yayilir;
            // mukemmel sutun olusmaz, ic ice gecme yine yok.
            for (int t = 0; t < 160 && !placedOk; t++)
            {
                int best = -1; float bestTop = float.MaxValue;
                for (int c = 0; c < 10; c++)
                {
                    int i = Random.Range(0, rects.Count);
                    if (covered[i] && t < 120) continue;
                    if (rects[i].top < bestTop) { bestTop = rects[i].top; best = i; }
                }
                if (best < 0) continue;
                var top = rects[best];
                float floorY = floors[top.book];
                Vector2 anchor = top.center + Random.insideUnitCircle * (widths[k] * (t < 100 ? 0.35f : 0.03f));
                if (!TryPlace(k, anchor, floorY, null, out int support, out var rect, out float bottom, out float yaw, true)) continue;
                if (support < 0) continue;
                Commit(k, rect, bottom, yaw, support, floorY, null);
                forced++;
                placedOk = true;
            }
        }
        Physics.SyncTransforms();
        int frozen = 0;
        for (int i = 0; i < restOrder.Count; i++)
            if (restSupport[i] != null && restOrder[i].InitializeSpawnSupport(restSupport[i])) frozen++;
        placedGrid.Clear();
        Debug.Log($"BookSpawner: {restOrder.Count}/{n} kitap {piles.Count} kucuk yigina dagitildi ({onFloor} zeminde, {stacked} ustte, " +
                  $"{forced} yedek); {frozen} kitap yerinde donmus.");
        done(true);
    }

    /// <summary>
    /// Karman corman kitap yiginlari: kitaplar rastgele konum/acida birbirinin ustune YIGILIR,
    /// alttakilerin uzerine yaslanip EGILIR (bir ucu kitapta, bir ucu yerde gibi). Her kitabin alt
    /// yuzeyi, altindaki yukseklik profilinin ust zarfina oturan bir duzlemdir: hic bir yere
    /// gomulmez (ic ice gecme yok), en az bir-iki noktadan temas eder (havada asili durmaz) ve
    /// dayandigi kitaplar hareket edene kadar yerinde donmus baslar. Dort kosesi de seridin
    /// icindedir; yurume yoluna / gecitlere tasmaz.
    /// </summary>
    private IEnumerator PlaceBooksAsMessyHeaps(List<BookItem> books, System.Action<bool> done)
    {
        var loose = new List<BookItem>(books.Count);
        foreach (var book in books) if (!spawnedTowerBooks.Contains(book)) loose.Add(book);
        if (loose.Count == 0 || shelfFootprints.Count == 0 || !gatherInFrontOfShelves) { done(false); yield break; }

        foreach (var book in loose) book.transform.rotation = book.GetAlignedRotation(Vector3.up, Vector3.forward);
        Physics.SyncTransforms();
        int n = loose.Count;
        var lengths = new float[n]; var widths = new float[n]; var thick = new float[n];
        var localOffsets = new Vector3[n];
        var widthList = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            // Gorunen kapak/sayfa olcusu: carpisma kutusu kitaptan kalin/genis olursa yiginda
            // kitaplar arasinda bosluk kalip havada gibi duruyordu.
            Bounds b = BookVisualBounds(loose[i]);
            if (i == 0)
            {
                Bounds c = BookBounds(loose[i]);
                Debug.Log($"BookSpawner: kitap olcusu gorsel {b.size.x:0.000}x{b.size.y:0.000}x{b.size.z:0.000}, carpisma {c.size.x:0.000}x{c.size.y:0.000}x{c.size.z:0.000} m");
            }
            lengths[i] = b.size.z; widths[i] = b.size.x; thick[i] = Mathf.Max(0.003f, b.size.y);
            localOffsets[i] = Quaternion.Inverse(loose[i].transform.rotation) * (b.center - loose[i].transform.position);
            widthList.Add(b.size.x);
        }
        float sample = Mathf.Clamp(Percentile(widthList, 0.5f) * 0.6f, 0.2f, 0.5f);
        var cells = BuildBandCells(sample, sample);
        if (cells.Count == 0) { done(false); yield break; }

        const float maxHeapHeight = 0.4f;   // yigin/kule degil: alana savrulmus karmasa
        const float maxSlope = 0.5f;    // ~27 derece: dik duvar gibi duran kitap olmasin
        const int NT = 9, NS = 5;
        placedGrid.Clear();
        var gridBounds = new List<Bounds>(n);
        var rects = new List<LooseRect>(n);
        var pa = new List<float>(n); var pb = new List<float>(n); var pd = new List<float>(n); var vth = new List<float>(n);
        var order = new List<BookItem>(n);
        var colliders = new List<Collider>(n);
        var contacts = new List<List<Collider>>(n);
        var floors = new List<float>(n);
        var floorColliders = new List<Collider>(n);
        var piles = new List<(Vector2 center, float floor, Collider floorCollider, int target, int count)>();
        var cand = new List<int>(32);
        var h = new float[NT, NS]; var owner = new int[NT, NS];
        var ts = new float[NT]; var ss = new float[NS];
        var hullT = new List<int>(NT);
        int tilted = 0, forced = 0, onFloor = 0;

        float TopAt(int i, Vector2 p)
        {
            var r = rects[i];
            Vector2 d = p - r.center;
            float t = Vector2.Dot(d, r.axis), s = Vector2.Dot(d, new Vector2(-r.axis.y, r.axis.x));
            if (Mathf.Abs(t) > r.half.x || Mathf.Abs(s) > r.half.y) return float.NegativeInfinity;
            return pa[i] + pb[i] * t + pd[i] * s + vth[i];
        }

        // Ust zarf (monoton zincir) uzerinde x=0'i kapsayan dogru parcasinin egimi.
        float HullSlope(float[] xs, float[] ys, int count)
        {
            hullT.Clear();
            for (int i = 0; i < count; i++)
            {
                while (hullT.Count >= 2)
                {
                    int o = hullT[hullT.Count - 2], a = hullT[hullT.Count - 1];
                    float cross = (xs[a] - xs[o]) * (ys[i] - ys[o]) - (ys[a] - ys[o]) * (xs[i] - xs[o]);
                    if (cross >= 0f) hullT.RemoveAt(hullT.Count - 1); else break;
                }
                hullT.Add(i);
            }
            for (int j = 0; j + 1 < hullT.Count; j++)
            {
                int a = hullT[j], b = hullT[j + 1];
                if (xs[a] <= 0f && xs[b] >= 0f)
                {
                    // Tam tepe noktasindaysa kitap rastgele bir yana devrilir.
                    if (Mathf.Abs(xs[b]) < 1e-5f && j + 2 < hullT.Count && Random.value < 0.5f) { a = b; b = hullT[j + 2]; }
                    else if (Mathf.Abs(xs[a]) < 1e-5f && j > 0 && Random.value < 0.5f) { b = a; a = hullT[j - 1]; }
                    return (ys[b] - ys[a]) / Mathf.Max(1e-5f, xs[b] - xs[a]);
                }
            }
            return 0f;
        }

        var profile = new float[Mathf.Max(NT, NS)];
        var ct = new float[NT * NS + 256]; var cs = new float[NT * NS + 256]; var ch = new float[NT * NS + 256];
        int cn = 0;
        var axisVals = new float[Mathf.Max(NT, NS)];

        // Verilen ayak izi icin yukseklik orneklerini doldurur ve alt yuzey duzlemini (a + b*t + d*s) oturtur.
        void Fit(Vector2 center, Vector2 axis, Vector2 side, Vector2 half, float floorY, out float a, out float b, out float d, int levels = 4)
        {
            var probe = new LooseRect { center = center, axis = axis, half = half };
            cand.Clear();
            foreach (int i in GridQuery(RectBounds(probe, floorY)))
                if (RectsOverlap(probe, rects[i])) cand.Add(i);
            for (int it = 0; it < NT; it++) ts[it] = Mathf.Lerp(-half.x, half.x, it / (float)(NT - 1));
            for (int is_ = 0; is_ < NS; is_++) ss[is_] = Mathf.Lerp(-half.y, half.y, is_ / (float)(NS - 1));
            for (int it = 0; it < NT; it++)
                for (int is_ = 0; is_ < NS; is_++)
                {
                    Vector2 p = center + axis * ts[it] + side * ss[is_];
                    float best = floorY; int who = -1;
                    foreach (int i in cand) { float v = TopAt(i, p); if (v > best) { best = v; who = i; } }
                    h[it, is_] = best; owner[it, is_] = who;
                }
            // Kitap yukaridan birakilmis gibi: alt yuzey duzlemi (a + b*t + d*s) HER noktanin ustunde
            // kalirken merkezi olabildigince ALCAGA iner (en dusuk potansiyel enerji). Bu, dayandigi
            // 2-3 noktaya tam oturan, altinda gereksiz bosluk kalmayan dogal egimi verir.
            cn = 0;
            for (int it = 0; it < NT; it++)
                for (int is_ = 0; is_ < NS; is_++) { ct[cn] = ts[it]; cs[cn] = ss[is_]; ch[cn] = h[it, is_]; cn++; }
            foreach (int i in cand)
            {
                var r = rects[i]; Vector2 rs = new Vector2(-r.axis.y, r.axis.x);
                for (int q = 0; q < 4 && cn < ct.Length; q++)
                {
                    Vector2 c = r.center + r.axis * (q < 2 ? r.half.x - 0.002f : -r.half.x + 0.002f) + rs * ((q & 1) == 0 ? r.half.y - 0.002f : -r.half.y + 0.002f);
                    Vector2 dd = c - center;
                    float t = Vector2.Dot(dd, axis), s = Vector2.Dot(dd, side);
                    if (Mathf.Abs(t) > half.x || Mathf.Abs(s) > half.y) continue;
                    ct[cn] = t; cs[cn] = s; ch[cn] = TopAt(i, c); cn++;
                }
            }
            if (cand.Count == 0) { a = floorY; b = 0f; d = 0f; return; } // bos zemin: duz yatar
            float Lift(float bb, float dd2)
            {
                float m = float.NegativeInfinity;
                for (int j = 0; j < cn; j++) { float v = ch[j] - bb * ct[j] - dd2 * cs[j]; if (v > m) m = v; }
                return m;
            }
            // Dis bukey (max of linear) fonksiyon: kabadan inceye izgara aramasi yeterli ve kararlı.
            float lim = maxSlope * 1.15f, bestB = 0f, bestD = 0f, bestA = Lift(0f, 0f);
            float stepSize = lim / 3f;
            for (int level = 0; level < levels; level++)
            {
                float cb = bestB, cd = bestD;
                for (int ib = -3; ib <= 3; ib++)
                    for (int id = -3; id <= 3; id++)
                    {
                        float tb = Mathf.Clamp(cb + ib * stepSize, -lim, lim), td = Mathf.Clamp(cd + id * stepSize, -lim, lim);
                        float v = Lift(tb, td);
                        // Esitlikte daha duz olan: duz zeminde kitap boşuna egilmesin.
                        if (v + 0.0004f * (Mathf.Abs(tb) + Mathf.Abs(td)) < bestA + 0.0004f * (Mathf.Abs(bestB) + Mathf.Abs(bestD)) - 1e-6f)
                        { bestA = v; bestB = tb; bestD = td; }
                    }
                stepSize *= 0.3f;
            }
            b = bestB; d = bestD; a = bestA;
        }

        bool Evaluate(int k, Vector2 center, float floorY, bool strict, out LooseRect rect, out float a, out float b, out float d, out float top,
            float yawOverride = float.NaN)
        {
            float yaw = float.IsNaN(yawOverride) ? Random.Range(0f, 360f) : yawOverride;
            Vector2 axis = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            Vector2 side = new Vector2(-axis.y, axis.x);
            Vector2 full = new Vector2(lengths[k] * 0.5f, widths[k] * 0.5f);
            rect = new LooseRect { center = center, axis = axis, half = full };
            a = b = d = top = 0f;
            // Kenarlar cetvelle cizilmis gibi durmasin: kitap merkezi alanda kalir, koseleri
            // rastgele biraz (en cok ~45 cm) yola / gecide sarkabilir. Kitapliklarin icine asla girmez.
            if (!InShelfBand(new Vector3(center.x, floorY + 0.5f, center.y), 0.02f)) return false;
            // Cogu kitap az sarkar; ara sira biri yola daha cok tasar (gercek dokulmus yigin kenari).
            float spill = Random.value < 0.14f ? Random.Range(0.3f, 0.6f) : Random.value * Random.value * 0.35f;
            for (int q = 0; q < 4; q++)
            {
                Vector2 corner = center + axis * (q < 2 ? full.x : -full.x) + side * ((q & 1) == 0 ? full.y : -full.y);
                Vector3 corner3 = new Vector3(corner.x, floorY + 0.5f, corner.y);
                // -0.07: kitaplar rafin dibine ~3 cm'e kadar yaslanabilir (rafin icine girmez).
                if (!InShelfBand(corner3, -0.07f, spill) || !InsideAreaRelaxed(corner3, 1.2f)) return false;
            }
            // 1. tur tam boy; 2. tur egik kitabin GERCEK (kisalan) yatay izdusumuyle: kitabin uzanmadigi
            // bir yerdeki yuksek kitap onu havaya kaldirmasin.
            Fit(center, axis, side, full, floorY, out a, out b, out d, 2);
            if (Mathf.Abs(b) > maxSlope * 1.3f || Mathf.Abs(d) > maxSlope * 1.3f) return false;
            Vector2 half = new Vector2(full.x / Mathf.Sqrt(1f + b * b), full.y / Mathf.Sqrt(1f + d * d));
            Fit(center, axis, side, half, floorY, out a, out b, out d);
            // Cok dik egim = tek kenara takilip havada duran kitap. Asla kirpilmaz, reddedilir.
            if (Mathf.Abs(b) > maxSlope || Mathf.Abs(d) > maxSlope) return false;
            a += 0.0015f;
            Vector3 nrm = Vector3.Cross(new Vector3(side.x, d, side.y), new Vector3(axis.x, b, axis.y)).normalized;
            float vt = thick[k] / Mathf.Max(0.5f, nrm.y);
            top = a + Mathf.Abs(b) * half.x + Mathf.Abs(d) * half.y + vt;
            if (strict && top - floorY > maxHeapHeight) return false;
            rect.half = half;
            rect.top = top;
            return true;
        }

        void Commit(int k, LooseRect rect, float a, float b, float d, float floorY, Collider floorCollider)
        {
            Vector2 axis = rect.axis, side = new Vector2(-axis.y, axis.x);
            // Temas noktalari: duzleme 3 mm'den yakin ornekler -> dayandigi kitaplar / zemin.
            var touching = new List<Collider>(3);
            bool floorTouch = false;
            for (int it = 0; it < NT; it++)
                for (int is_ = 0; is_ < NS; is_++)
                {
                    float gap = a - 0.0015f + b * ts[it] + d * ss[is_] - h[it, is_];
                    if (gap > 0.003f) continue;
                    int who = owner[it, is_];
                    if (who < 0) floorTouch = true;
                    else if (!touching.Contains(colliders[who])) touching.Add(colliders[who]);
                }
            if (floorTouch && floorCollider != null) touching.Add(floorCollider);
            if (touching.Count == 0)
            {
                // Temas bir alt kitabin kosesinden geldiyse: en yakin ornegin sahibini destek say.
                float bestGap = float.MaxValue; int who = -2;
                for (int it = 0; it < NT; it++)
                    for (int is_ = 0; is_ < NS; is_++)
                    {
                        float gap = a + b * ts[it] + d * ss[is_] - h[it, is_];
                        if (gap < bestGap) { bestGap = gap; who = owner[it, is_]; }
                    }
                if (who >= 0) touching.Add(colliders[who]);
                else if (who == -1 && floorCollider != null) touching.Add(floorCollider);
            }
            Vector3 T = new Vector3(axis.x, b, axis.y).normalized;
            Vector3 N = Vector3.Cross(new Vector3(side.x, d, side.y), new Vector3(axis.x, b, axis.y)).normalized;
            var book = loose[k];
            Quaternion rot = book.GetAlignedRotation(N, T);
            book.transform.rotation = rot;
            Vector3 bottomCenter = new Vector3(rect.center.x, a, rect.center.y);
            Vector3 boxCenter = bottomCenter + N * (thick[k] * 0.5f);
            book.transform.position = boxCenter - rot * localOffsets[k];
            rects.Add(rect);
            pa.Add(a); pb.Add(b); pd.Add(d); vth.Add(thick[k] / Mathf.Max(0.5f, N.y));
            GridAdd(gridBounds, RectBounds(rect, floorY));
            order.Add(book);
            colliders.Add(book.GetComponentInChildren<Collider>());
            contacts.Add(touching);
            floors.Add(floorY); floorColliders.Add(floorCollider);
            if (Mathf.Abs(b) > 0.03f || Mathf.Abs(d) > 0.03f) tilted++;
            if (touching.Count == 1 && floorTouch) onFloor++;
        }

        for (int k = 0; k < n; k++)
        {
            if (YieldForFrameBudget()) { ShopLoadingScreen.Progress(0.55f + 0.3f * k / n); yield return null; }
            bool placedOk = false;
            // Kitap alanin rastgele bir noktasina "kusulur"; birkac rastgele noktadan en ALCAKTA
            // kalan secilir. Boylece bir yerde tepe/kule birikmez, alan esit ama karmakarisik dolar.
            {
                float bestScore = float.MaxValue; LooseRect bestRect = default; float ba = 0, bb = 0, bd = 0, bFloor = 0; Collider bCol = null;
                int valid = 0;
                for (int attempt = 0; attempt < 14 && valid < 5; attempt++)
                {
                    // Dogal dokulme: kitaplar rafin dibinde daha yogun, yola dogru seyrelir.
                    var cell = cells[Random.Range(0, cells.Count)];
                    for (int pick = 0; pick < 6; pick++)
                    {
                        float w = 0.3f + Mathf.Exp(-cell.across / 1.5f);
                        if (Random.value * 1.3f < w) break;
                        cell = cells[Random.Range(0, cells.Count)];
                    }
                    Vector2 anchor = new Vector2(cell.point.x, cell.point.z) + Random.insideUnitCircle * sample * 0.7f;
                    if (!Evaluate(k, anchor, cell.point.y, false, out var rect, out float a, out float b, out float d, out float top)) continue;
                    valid++;
                    float score = top - cell.point.y + Random.value * 0.06f;
                    if (score < bestScore) { bestScore = score; bestRect = rect; ba = a; bb = b; bd = d; bFloor = cell.point.y; bCol = cell.floor; }
                }
                if (valid > 0)
                {
                    RefreshSamples(bestRect, bFloor);
                    Commit(k, bestRect, ba, bb, bd, bFloor, bCol);
                    placedOk = true;
                }
            }
            if (placedOk) continue;
            // Yer kalmadiysa: birkac rastgele yigin noktasindan EN ALCAK sonucu veren secilir;
            // fazla kitaplar tek kuleye degil tum yiginlara esit ve daginik yayilir.
            for (int round = 0; round < 6 && !placedOk; round++)
            {
                float bestTop = float.MaxValue; LooseRect bestRect = default; float ba = 0, bb = 0, bd = 0, bFloor = 0; Collider bCol = null;
                bool any = false;
                for (int c = 0; c < 4; c++)
                {
                    int i = Random.Range(0, rects.Count);
                    Vector2 anchor = rects[i].center + Random.insideUnitCircle * (lengths[k] * 0.5f);
                    if (!Evaluate(k, anchor, floors[i], false, out var rect, out float a, out float b, out float d, out float top)) continue;
                    if (top < bestTop)
                    {
                        // Evaluate'in ornek tamponlari son cagriya ait; secileni tekrar hesaplamak icin sakla.
                        bestTop = top; bestRect = rect; ba = a; bb = b; bd = d; bFloor = floors[i]; bCol = floorColliders[i]; any = true;
                    }
                }
                if (!any) continue;
                // Temas bilgisi icin secilen yerlesimi ayni duzlemle yeniden ornekle.
                RefreshSamples(bestRect, bFloor);
                Commit(k, bestRect, ba, bb, bd, bFloor, bCol);
                forced++;
                placedOk = true;
            }
            // Son care: alcak bir kitabin tam ustune, onunla ayni yonde (egimi de ayni, tam oturur).
            for (int t = 0; t < 200 && !placedOk; t++)
            {
                int i = -1; float low = float.MaxValue;
                for (int c = 0; c < 12; c++) { int j = Random.Range(0, rects.Count); if (rects[j].top < low) { low = rects[j].top; i = j; } }
                if (i < 0) break;
                float yaw = Mathf.Atan2(rects[i].axis.x, rects[i].axis.y) * Mathf.Rad2Deg + (Random.value < 0.5f ? 0f : 180f);
                if (!Evaluate(k, rects[i].center + Random.insideUnitCircle * 0.02f, floors[i], false, out var rect, out float a, out float b, out float d, out _, yaw)) continue;
                Commit(k, rect, a, b, d, floors[i], floorColliders[i]);
                forced++;
                placedOk = true;
            }
        }

        void RefreshSamples(LooseRect rect, float floorY)
        {
            Vector2 axis = rect.axis, side = new Vector2(-axis.y, axis.x);
            cand.Clear();
            foreach (int i in GridQuery(RectBounds(rect, floorY)))
                if (RectsOverlap(rect, rects[i])) cand.Add(i);
            for (int it = 0; it < NT; it++) ts[it] = Mathf.Lerp(-rect.half.x, rect.half.x, it / (float)(NT - 1));
            for (int is_ = 0; is_ < NS; is_++) ss[is_] = Mathf.Lerp(-rect.half.y, rect.half.y, is_ / (float)(NS - 1));
            for (int it = 0; it < NT; it++)
                for (int is_ = 0; is_ < NS; is_++)
                {
                    Vector2 p = rect.center + axis * ts[it] + side * ss[is_];
                    float best = floorY; int who = -1;
                    foreach (int i in cand) { float v = TopAt(i, p); if (v > best) { best = v; who = i; } }
                    h[it, is_] = best; owner[it, is_] = who;
                }
        }

        Physics.SyncTransforms();
        int frozen = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (YieldForFrameBudget()) { ShopLoadingScreen.Progress(0.85f); yield return null; }
            if (order[i].InitializeSpawnSupports(contacts[i])) frozen++;
            else if (order[i].FreezeWhereResting(null)) frozen++;
        }
        placedGrid.Clear();
        Debug.Log($"BookSpawner: {order.Count}/{n} kitap alanlara karmakarisik savruldu ({tilted} egik, {onFloor} yalniz zeminde, " +
                  $"{forced} yedek); {frozen} kitap yerinde donmus.");
        done(true);
    }

    /// <summary>
    /// GERCEK atis: kitaplar alanlarinin ustunden rastgele donerek, rastgele hizla yere firlatilir ve
    /// acilis ekraninin arkasinda hizlandirilmis GERCEK fizikle duser, carpar, kayar, birbirine yaslanir.
    /// Dalgalar halinde (her dalga oncekilerin ustune) atilir; duran kitaplar alttan uste yerinde
    /// donar. Alanin cok disina (yurume yolunun ortasina, kitapligin icine) savrulan kitap tekrar atilir;
    /// kenardan biraz tasmak serbest. Duzenli karmasa yok: sonuc tamamen fizigin sonucu.
    /// </summary>
    private IEnumerator PlaceBooksByThrowing(List<BookItem> books, System.Action<bool> done)
    {
        var loose = new List<BookItem>(books.Count);
        foreach (var book in books) if (!spawnedTowerBooks.Contains(book)) loose.Add(book);
        if (loose.Count == 0 || shelfFootprints.Count == 0 || !gatherInFrontOfShelves) { done(false); yield break; }
        var widthList = new List<float>(loose.Count);
        foreach (var book in loose) { book.transform.rotation = book.GetAlignedRotation(Vector3.up, Vector3.forward); }
        Physics.SyncTransforms();
        foreach (var book in loose) widthList.Add(BookVisualBounds(book).size.x);
        float sample = Mathf.Clamp(Percentile(widthList, 0.5f) * 0.6f, 0.2f, 0.5f);
        var cells = BuildBandCells(sample, sample);
        if (cells.Count == 0) { done(false); yield break; }

        // Kaba yukseklik haritasi: yeni dalga, onceki dalgalarin ustunden (ic ice baslamadan) atilir.
        const float hmCell = 0.3f;
        var heightMap = new Dictionary<long, float>();
        float TopNear(Vector3 p, float radius)
        {
            float top = float.NegativeInfinity;
            int x0 = Mathf.FloorToInt((p.x - radius) / hmCell), x1 = Mathf.FloorToInt((p.x + radius) / hmCell);
            int z0 = Mathf.FloorToInt((p.z - radius) / hmCell), z1 = Mathf.FloorToInt((p.z + radius) / hmCell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (heightMap.TryGetValue(CellKey(x, z), out float v) && v > top) top = v;
            return top;
        }
        void MarkTop(Bounds b)
        {
            int x0 = Mathf.FloorToInt(b.min.x / hmCell), x1 = Mathf.FloorToInt(b.max.x / hmCell);
            int z0 = Mathf.FloorToInt(b.min.z / hmCell), z1 = Mathf.FloorToInt(b.max.z / hmCell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    long key = CellKey(x, z);
                    if (!heightMap.TryGetValue(key, out float v) || b.max.y > v) heightMap[key] = b.max.y;
                }
        }

        var bodies = new Dictionary<BookItem, Rigidbody>(loose.Count);
        var saved = new Dictionary<Rigidbody, (CollisionDetectionMode mode, float dep, float lin, float ang, float sleep)>(loose.Count);
        Vector3 parkBase = new Vector3(0f, -200f, 0f);
        int parkIndex = 0;
        void Park(BookItem book, Rigidbody body)
        {
            body.isKinematic = true;
            body.detectCollisions = false;
            book.transform.position = parkBase + new Vector3((parkIndex % 60) * 1.2f, 0f, (parkIndex / 60) * 1.2f);
            parkIndex++;
        }
        foreach (var book in loose)
        {
            if (!book.TryGetComponent<Rigidbody>(out var body)) continue;
            bodies[book] = body;
            saved[body] = (body.collisionDetectionMode, body.maxDepenetrationVelocity, body.linearDamping, body.angularDamping, body.sleepThreshold);
            Park(book, body);
        }
        Physics.SyncTransforms();

        var queue = new List<BookItem>(bodies.Keys);
        for (int i = queue.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (queue[i], queue[j]) = (queue[j], queue[i]); }
        var retries = new Dictionary<BookItem, int>();
        var loose3 = new List<BookItem>();      // dusmus ama henuz donmamis
        var waveList = new List<BookItem>(256);
        var launchPoints = new List<Vector3>(256);
        var floorOfBook = new Dictionary<BookItem, float>(loose.Count);
        var previousMode = Physics.simulationMode;
        LayoutInProgress = true;
        const float step = 0.02f;
        int waveSize = Mathf.Clamp(queue.Count / 20 + 1, 100, 180);
        int frozenTotal = 0, rethrown = 0, waves = 0;
        float simulatedTotal = 0f;
        ShopLoadingScreen.Progress(0.6f);

        bool Allowed(Vector3 c, float floorY)
        {
            if (c.y < floorY - 0.3f) return false; // zemini delmis
            if (c.y > floorY + 1.2f) return false;  // kitaplik ustune / tavana / baska yere takilmis
            var probe = new Vector3(c.x, floorY + 0.5f, c.z);
            // Kitaplik uclarinda biraz tasma serbest; YOL tarafinda kitabin merkezi seridin en az 0.35 m icinde kalir.
            return InShelfBand(probe, -0.07f, 0.4f, -0.35f) && InsideAreaRelaxed(probe, 1.2f);
        }

        try
        {
            Physics.simulationMode = SimulationMode.Script;
            int cursor = 0;
            while (cursor < queue.Count)
            {
                waves++;
                waveList.Clear(); launchPoints.Clear();
                int end = Mathf.Min(queue.Count, cursor + waveSize);
                for (; cursor < end; cursor++)
                {
                    var book = queue[cursor];
                    var body = bodies[book];
                    // Rafin dibinde yogun, yola dogru seyrek. Atis noktasi ALCAK (tavana/kitaplik ustune
                    // cikmaz); ayni dalgada havada baska kitapla cakisiyorsa baska nokta secilir.
                    BandCell cell = default; Vector3 p = default;
                    for (int tryPoint = 0; tryPoint < 10; tryPoint++)
                    {
                        cell = cells[Random.Range(0, cells.Count)];
                        for (int pick = 0; pick < 6; pick++)
                        {
                            if (Random.value * 1.1f < 0.6f + 0.5f * Mathf.Exp(-cell.across / 1.5f)) break;
                            cell = cells[Random.Range(0, cells.Count)];
                        }
                        Vector2 jitter = Random.insideUnitCircle * sample;
                        // Yol tarafindaki kenardan en az ~0.7 m iceride birak.
                        float over = cell.across + Vector2.Dot(jitter, cell.outward) - (bookAreaDepthMeters - 0.7f);
                        if (over > 0f) jitter -= cell.outward * over;
                        p = new Vector3(cell.point.x + jitter.x, 0f, cell.point.z + jitter.y);
                        // Kel alan kalmasin: ikinci bir aday nokta daha alcaksa (bos zemin) oraya at.
                        {
                            var alt = cells[Random.Range(0, cells.Count)];
                            Vector2 j2 = Random.insideUnitCircle * sample;
                            float over2 = alt.across + Vector2.Dot(j2, alt.outward) - (bookAreaDepthMeters - 0.7f);
                            if (over2 > 0f) j2 -= alt.outward * over2;
                            Vector3 p2 = new Vector3(alt.point.x + j2.x, 0f, alt.point.z + j2.y);
                            float h1 = Mathf.Max(cell.point.y, TopNear(p, 0.6f)) - cell.point.y;
                            float h2 = Mathf.Max(alt.point.y, TopNear(p2, 0.6f)) - alt.point.y;
                            if (h2 + 0.05f < h1) { cell = alt; p = p2; }
                        }
                        float below = Mathf.Max(cell.point.y, TopNear(p, 0.55f));
                        p.y = Mathf.Min(below + 0.55f + Random.Range(0f, 0.5f), cell.point.y + 1.9f);
                        bool clash = false;
                        foreach (var q in launchPoints)
                            if (Mathf.Abs(q.y - p.y) < 0.95f && (new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude < 0.95f) { clash = true; break; }
                        if (!clash) break;
                    }
                    launchPoints.Add(p);
                    floorOfBook[book] = cell.point.y;
                    book.transform.rotation = Random.rotation;
                    book.transform.position = p;
                    body.isKinematic = false;
                    body.detectCollisions = true;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    body.maxDepenetrationVelocity = 1.5f;
                    body.linearDamping = 0.15f;
                    body.angularDamping = 0.6f;
                    body.sleepThreshold = Mathf.Max(saved[body].sleep, 0.04f);
                    Vector2 throwDir = Random.insideUnitCircle.normalized * Random.Range(0.2f, 1.2f);
                    // Seridin yol tarafindaki kitaplar yola dogru firlatilmaz (koridora tasmasin).
                    float toWalk = Vector2.Dot(throwDir, cell.outward);
                    if (toWalk > 0f && cell.across > bookAreaDepthMeters - 1.8f) throwDir -= cell.outward * toWalk * 1.3f;
                    body.linearVelocity = new Vector3(throwDir.x, Random.Range(-0.5f, 0.8f), throwDir.y);
                    body.angularVelocity = Random.insideUnitSphere * Random.Range(1f, 7f);
                    body.WakeUp();
                    waveList.Add(book);
                }
                Physics.SyncTransforms();
                loose3.AddRange(waveList);

                float simulated = 0f; int quiet = 0;
                while (simulated < 3.2f)
                {
                    double began = Time.realtimeSinceStartupAsDouble;
                    while (Time.realtimeSinceStartupAsDouble - began < 0.07 && simulated < 3.2f)
                    {
                        Physics.Simulate(step);
                        simulated += step;
                    }
                    int moving = 0;
                    foreach (var book in loose3)
                    {
                        var body = bodies[book];
                        if (body.isKinematic || body.IsSleeping()) continue;
                        if (body.linearVelocity.sqrMagnitude > 0.0025f || body.angularVelocity.sqrMagnitude > 0.03f) moving++;
                    }
                    ShopLoadingScreen.Progress(0.6f + 0.35f * Mathf.Clamp01((cursor - waveSize + waveSize * Mathf.Min(1f, simulated / 1.5f)) / (float)queue.Count));
                    if (simulated >= 0.8f && moving <= Mathf.Max(1, loose3.Count / 60)) { if (++quiet >= 2) break; } else quiet = 0;
                    yield return null;
                }
                simulatedTotal += simulated;

                // Alanin cok disina savrulan / zemini delen: yeniden atilacak (en fazla 2 kez).
                for (int i = loose3.Count - 1; i >= 0; i--)
                {
                    var book = loose3[i];
                    var body = bodies[book];
                    if (Allowed(body.worldCenterOfMass, floorOfBook[book])) continue;
                    retries.TryGetValue(book, out int tries);
                    if (tries >= 2 && body.worldCenterOfMass.y > floorOfBook[book] - 0.3f) continue; // biraksin, yolda bir kitap olsun
                    retries[book] = tries + 1;
                    Park(book, body);
                    loose3.RemoveAt(i);
                    if (tries < 4) { queue.Add(book); rethrown++; } // sonsuz dongu yok; kalan en sonda kurtarilir
                }
                Physics.SyncTransforms();
                // Duranlar alttan uste donar; donanlarin ustu yukseklik haritasina yazilir.
                loose3.Sort((a, b) => a.WorldCenter.y.CompareTo(b.WorldCenter.y));
                for (int pass = 0; pass < 6; pass++)
                {
                    int changed = 0;
                    for (int i = 0; i < loose3.Count; i++)
                    {
                        var book = loose3[i];
                        if (!book.FreezeWhereResting(null)) continue;
                        changed++;
                        MarkTop(BookBounds(book));
                    }
                    frozenTotal += changed;
                    loose3.RemoveAll(b => b.IsFrozenAtRest);
                    if (changed == 0) break;
                }
                // Donmayanlar (hala kayan) da yuksekligi etkiler.
                foreach (var book in loose3) MarkTop(BookBounds(book));
            }
        }
        finally
        {
            Physics.simulationMode = previousMode;
            foreach (var pair in saved)
            {
                var body = pair.Key;
                if (body == null) continue;
                body.collisionDetectionMode = pair.Value.mode;
                body.maxDepenetrationVelocity = pair.Value.dep;
                body.linearDamping = pair.Value.lin;
                body.angularDamping = pair.Value.ang;
                body.sleepThreshold = pair.Value.sleep;
                if (!body.detectCollisions) { body.detectCollisions = true; }
            }
            LayoutInProgress = false;
        }
        // Park yerinde kalan (olmamali) kitap varsa: en yakin serbest hucrenin ustune birak.
        int stranded = 0;
        foreach (var pair in bodies)
            if (pair.Key.WorldCenter.y < -100f || !pair.Value.detectCollisions)
            {
                var cell = cells[Random.Range(0, cells.Count)];
                pair.Key.transform.position = new Vector3(cell.point.x, Mathf.Max(cell.point.y, TopNear(cell.point, 0.55f)) + 0.6f, cell.point.z);
                pair.Value.isKinematic = false;
                stranded++;
            }
        var machine = FindFirstObjectByType<BookRecallMachine>();
        if (machine != null)
        {
            machine.RebuildPlayBounds();
            int lostNow = 0; var sb = new System.Text.StringBuilder();
            foreach (var pair in bodies)
                if (machine.IsLost(pair.Key))
                {
                    if (lostNow < 4) sb.Append($" [{pair.Key.name} merkez {pair.Key.WorldCenter} kok {pair.Key.transform.position}]");
                    lostNow++;
                }
            Debug.Log($"BookSpawner TANI: yerlesim sonrasi {lostNow} kitap 'kayip' sayiliyor; alan {machine.PlayBounds.min}..{machine.PlayBounds.max}{sb}");
        }
        Debug.Log($"BookSpawner: {bodies.Count} kitap {waves} dalgada GERCEK fizikle yere firlatildi ({simulatedTotal:0.0} sn simule); " +
                  $"{frozenTotal} yerinde dondu, {loose3.Count} serbest, {rethrown} kez yeniden atildi, {stranded} kurtarildi.");
        done(true);
    }

    private IEnumerator SeparateInitialBooks(List<BookItem> books)
    {
        bool handled = false;
        if (heapLayoutMode)
        {
            var heaps = PlaceBooksByThrowing(books, ok => handled = ok);
            while (heaps.MoveNext()) yield return heaps.Current;
            if (handled) yield break;
        }
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
        // Only explicitly named Corridor_01..Corridor_04 objects are auto-registered.
        // Legacy helpers such as Left/Right can remain in the hierarchy without
        // accidentally becoming additional book spawn areas.
        var zones = new List<BoxCollider>(corridorAreas ?? System.Array.Empty<BoxCollider>());
        int added = 0;

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != "Book Spawn Corridors") continue;

            foreach (var zone in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!zone) continue;
                if (!zone.gameObject.name.StartsWith("Corridor_")) continue;
                if (zones.Contains(zone)) continue;

                int vacant = zones.FindIndex(candidate => candidate == null);
                if (vacant >= 0) zones[vacant] = zone;
                else zones.Add(zone);
                added++;
            }
        }

        var validZones = new List<BoxCollider>();
        foreach (var zone in zones)
        {
            if (!zone) continue;
            if (validZones.Count >= 4) break;
            validZones.Add(zone);
        }
        corridorAreas = validZones.ToArray();

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
