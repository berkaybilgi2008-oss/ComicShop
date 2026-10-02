using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    [Header("Corridor Spawn Areas")]
    [Tooltip("When assigned, books spawn only in these boxes. Box colliders may stay disabled.")]
    public BoxCollider[] corridorAreas;
    [Min(0f)] public float corridorEdgePadding = 0.35f;

    [Header("Varsayilan Prefab ve Alan")]
    [Tooltip("BookData icinde ozel prefab verilmezse kullanilacak fiziksel kitap prefab'i.")]
    public GameObject bookPrefab;
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
        LayoutInProgress = true;
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
                var settle = WaitForBooksToSettle();
                while (settle.MoveNext()) yield return settle.Current;
            }
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose();
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
                elapsed >= Mathf.Min(maxSettleSeconds, 4f))
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
        loadPhase = "kitap olusturma";
        sessionBooks.Clear();
        {
            // One guaranteed book per assigned area, remaining books weighted by usable area.
            // Yigin/dagitim modunda tum kitaplar zaten sonradan yerlestirilir: baslangic konumu icin
            // pahali serit ornekleme (3000 kitap x binlerce deneme, TEK karede) Play Solo'da oyunu
            // saniyelerce donduruyordu. Ucuz rastgele koridor noktasi yeterli.
            Vector3[] positions = CreateSpawnPositions(ids.Count);

            // Spawn in small bursts instead of instantiating the whole catalogue in one frame.
            // The burst is intentionally short: books still appear quickly, but Instantiate +
            // renderer/material setup + NetworkObject creation are spread across frames.
            const int spawnBatchSize = 15;
            const float spawnBatchDelay = 0.035f;
            for (int i = 0; i < ids.Count; i++)
            {
                SpawnSingleBook(ids[i], positions[i]);
                if ((i + 1) % spawnBatchSize == 0)
                {
                    ShopLoadingScreen.Progress((float)(i + 1) / ids.Count * 0.45f);
                    yield return new WaitForSecondsRealtime(spawnBatchDelay);
                }
                else if (YieldForFrameBudget())
                {
                    ShopLoadingScreen.Progress((float)(i + 1) / ids.Count * 0.45f);
                    yield return null;
                }
            }
            var books = new List<BookItem>(sessionBooks.Count);
            foreach (var book in sessionBooks) books.Add(book.GetComponent<BookItem>());
            Physics.SyncTransforms();
            ShopLoadingScreen.Progress(0.85f);
            Debug.Log($"BookSpawner: aktif corridor spawn sistemi; {books.Count} kitap atanmis corridor alanlarinda birakildi.");

            int published = 0;
            loadPhase = "ag yayini";
            // Publish the final corridor spawn poses.
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

    public Vector3[] CreateSpawnPositions(int count)
    {
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        if (count < 0) throw new System.ArgumentException("Book count cannot be negative.");

        // Final corridor distribution: 2 : 2 : 1 : 0.
        // Base unit = count / 5. Any remainder is assigned randomly to corridors 1..3.
        // The total number of spawned books never changes.
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            var usable = new List<BoxCollider>(3);
            foreach (var zone in corridorAreas)
                if (zone != null && zone.gameObject.activeInHierarchy && !IsFourthCorridor(zone))
                    usable.Add(zone);

            if (usable.Count < 3)
                throw new System.InvalidOperationException("BookSpawner: 1., 2. ve 3. koridor bulunamadi.");

            int unit = count / 5;
            int[] targets = { unit * 2, unit * 2, unit };
            int remainder = count - (targets[0] + targets[1] + targets[2]);

            for (int i = 0; i < remainder; i++)
                targets[Random.Range(0, 3)]++;

            var positions = new Vector3[count];
            int at = 0;
            for (int corridor = 0; corridor < 3; corridor++)
            {
                Vector3[] corridorPositions = CreateEvenCorridorPositions(usable[corridor], targets[corridor]);
                for (int i = 0; i < corridorPositions.Length; i++)
                    positions[at++] = corridorPositions[i];
            }

            // Keep the requested 2:2:1 distribution, but randomize which physical
            // book gets which corridor position. The positions themselves stay
            // evenly separated so dense corridors do not start with overlapping
            // dynamic rigidbodies and trigger a physics spike.
            for (int i = positions.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }
            return positions;
        }

        throw new System.InvalidOperationException("BookSpawner: corridorAreas atanmamis.");
    }

    private Vector3[] CreateEvenCorridorPositions(BoxCollider zone, int count)
    {
        if (zone == null || count <= 0) return System.Array.Empty<Vector3>();

        Vector3 scale = zone.transform.lossyScale;
        float width = Mathf.Max(0.1f, zone.size.x * Mathf.Abs(scale.x) - 2f * corridorEdgePadding);
        float depth = Mathf.Max(0.1f, zone.size.z * Mathf.Abs(scale.z) - 2f * corridorEdgePadding);

        // Build a roughly square world-space grid. Every chosen cell gets one book,
        // which removes the large random-overlap spikes caused by dense corridor spawns.
        float aspect = width / Mathf.Max(0.1f, depth);
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));

        // If the first estimate is too narrow on one axis, expand the grid until
        // there are enough cells while keeping the cell aspect close to the corridor.
        while (columns * rows < count)
            rows++;

        float cellWidth = width / columns;
        float cellDepth = depth / rows;
        float halfX = zone.size.x * 0.5f - corridorEdgePadding / Mathf.Max(0.001f, Mathf.Abs(scale.x));
        float halfZ = zone.size.z * 0.5f - corridorEdgePadding / Mathf.Max(0.001f, Mathf.Abs(scale.z));

        var cells = new List<int>(columns * rows);
        for (int i = 0; i < columns * rows; i++) cells.Add(i);

        // Randomize cell selection, not the cell positions, so visual distribution
        // stays natural without sacrificing the minimum grid spacing.
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }

        var result = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            int cell = cells[i];
            int x = cell % columns;
            int z = cell / columns;

            float localX = -halfX + (x + 0.5f) * (2f * halfX / columns);
            float localZ = -halfZ + (z + 0.5f) * (2f * halfZ / rows);
            result[i] = zone.transform.TransformPoint(zone.center + new Vector3(localX, 0f, localZ))
                         + Vector3.up * spawnHeight;
        }

        return result;
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
        throw new System.InvalidOperationException("BookSpawner: corridorAreas atanmamis.");
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
        var zones = new List<BoxCollider>();
        if (corridorAreas != null)
            foreach (var existing in corridorAreas)
                if (existing != null && !IsFourthCorridor(existing))
                    zones.Add(existing);
        int added = 0;

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != "Book Spawn Corridors") continue;

            foreach (var zone in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!zone) continue;
                if (!zone.gameObject.name.StartsWith("Corridor_")) continue;
                if (IsFourthCorridor(zone)) continue;
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
            if (!zone || IsFourthCorridor(zone)) continue;
            if (validZones.Count >= 3) break;
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
            error = "BookSpawner: corridorAreas atanmamis. Aktif spawn sistemi yalnizca corridor alanlarini kullanir.";
            return false;
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