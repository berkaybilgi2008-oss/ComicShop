using Unity.Netcode;
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

    [Header("Test")]
    [Tooltip("BookData listesi bosken kullanilacak test kitap turu sayisi. Normal oyunda Setup ALL Book Models tarafindan doldurulan bookTypes kullanilir.")]
    [Min(1)]
    public int testBookTypeCount = 15;

    [Header("Rastgele Kuleler")]
    // New field names intentionally avoid restoring serialized 20% / 25-book settings.
    [Range(0f, 1f)] public float mixedTowerBookFraction = 0.1f;
    [Min(2)] public int smallTowerSize = 10;
    [Min(2)] public int largeTowerSize = 15;
    [Tooltip("Her kitap icin kule yonunden rastgele sapma (derece).")]
    [Range(0f, 180f)] public float towerYawJitter = 8f;

    private readonly HashSet<BookItem> spawnedTowerBooks = new HashSet<BookItem>();

    private bool sessionSpawned;
    private readonly List<GameObject> sessionBooks = new List<GameObject>();

    void Start()
    {
        if (FindFirstObjectByType<NetworkManager>() != null) return;
        ValidateConfiguration(false);
        InitializeStats();
        SpawnBooks(BookTypeCount);
        ShopRound.BeginOffline();
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

    public void SpawnSession()
    {
        if (sessionSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        SpawnBooks(BookTypeCount);
        sessionSpawned = true;
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
            ids.Add(data != null ? data.BookID : index);
        }
        GameStats.Initialize(ids, copiesPerBook);
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

        ValidateConfiguration(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        sessionBooks.Clear();
        try
        {
            // One guaranteed book per assigned area, remaining books weighted by usable area.
            var positions = CreateSpawnPositions(ids.Count);
            for (int i = 0; i < ids.Count; i++) SpawnSingleBook(ids[i], positions[i]);
            var books = new List<BookItem>(sessionBooks.Count);
            foreach (var book in sessionBooks) books.Add(book.GetComponent<BookItem>());
            spawnedTowerBooks.Clear();
            ArrangeTowers(books);
            SeparateInitialBooks(books);
            // Publish the completed server layout, never the pre-arrangement poses.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                foreach (var book in books)
                {
                    var networkBook = book.GetComponent<NetworkBook>();
                    networkBook.Initialize(book.bookID, book.brandID);
                    networkBook.NetworkObject.Spawn(true);
                }
        }
        catch
        {
            foreach (var book in sessionBooks)
            {
                if (book == null) continue;
                var network = book.GetComponent<NetworkObject>();
                if (network != null && network.IsSpawned) network.Despawn(true);
                else Destroy(book);
            }
            sessionBooks.Clear();
            sessionSpawned = false;
            throw;
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
        int small = Mathf.Max(2, smallTowerSize);
        int large = Mathf.Max(2, largeTowerSize);
        int budget = Mathf.Clamp(Mathf.RoundToInt(books.Count * Mathf.Clamp01(mixedTowerBookFraction)), 0, books.Count);
        // Half of the tower BOOK budget goes to each size, not half of the towers.
        // 3600 -> 360 -> 180/10 + 180/15 = 18 + 12 towers.
        var sizes = new List<int>();
        for (int i = 0; i < (budget / 2) / small; i++) sizes.Add(small);
        for (int i = 0; i < (budget - budget / 2) / large; i++) sizes.Add(large);
        for (int i = sizes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (sizes[i], sizes[j]) = (sizes[j], sizes[i]);
        }
        int requested = sizes.Count;
        if (requested == 0) return 0;
        var reservations = new List<Vector4>();
        var stacked = new HashSet<BookItem>();
        Physics.SyncTransforms();
        int cursor = 0;
        for (int tower = 0; tower < requested; tower++)
        {
            int count = sizes[tower];
            int first = cursor;
            cursor += count;
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
            // Leave the grounded stack physical. Existing contact-based settling
            // freezes it only after real support contacts; never suspend books in air.
            for (int i = first; i < first + count; i++) spawnedTowerBooks.Add(books[i]);
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

    private void SeparateInitialBooks(List<BookItem> books)
    {
        Physics.SyncTransforms();
        var placed = new List<Bounds>(books.Count);
        // Towers stay where they were authored; reserve their physical volume first.
        foreach (var book in books)
            if (spawnedTowerBooks.Contains(book)) placed.Add(BookBounds(book));
        int unresolved = 0;
        foreach (var book in books)
        {
            if (spawnedTowerBooks.Contains(book)) continue;
            Bounds original = BookBounds(book);
            Vector3 offset = original.center - book.transform.position;
            float radius = new Vector2(original.extents.x, original.extents.z).magnitude;
            bool found = false;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vector3 candidate = SampleSpawnPosition();
                if (!InsideArea(candidate, radius) || !Ground(candidate + Vector3.up * 0.1f, out var floor)) continue;
                Bounds test = new Bounds(new Vector3(candidate.x, floor.point.y + original.extents.y + 0.003f, candidate.z), original.size);
                // Raise above already placed books, instead of spawning intersecting
                // rigidbodies that explode apart on the first physics step.
                bool raised;
                do
                {
                    raised = false;
                    foreach (var other in placed)
                    {
                        if (!test.Intersects(other)) continue;
                        test.center = new Vector3(test.center.x, other.max.y + test.extents.y + 0.003f, test.center.z);
                        raised = true;
                    }
                } while (raised);
                bool blocked = false;
                foreach (var col in Physics.OverlapBox(test.center, test.extents, Quaternion.identity,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore))
                    if (col.GetComponentInParent<BookItem>() == null) { blocked = true; break; }
                if (blocked) continue;
                book.transform.position = test.center - offset;
                placed.Add(test);
                found = true;
                break;
            }
            if (!found) { placed.Add(original); unresolved++; }
        }
        Physics.SyncTransforms();
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

    public Vector3 SampleSpawnPosition()
    {
        if (corridorAreas != null && corridorAreas.Length > 0)
        {
            float total = 0;
            foreach (var zone in corridorAreas) total += ZoneWeight(zone);
            if (total <= 0) throw new System.InvalidOperationException("BookSpawner: corridor areas have no usable space. Fix their Size/Scale; legacy area was not used.");
            float choice = Random.value * total;
            BoxCollider selected = null;
            foreach (var zone in corridorAreas)
            {
                float weight = ZoneWeight(zone);
                if (weight <= 0) continue;
                selected = zone; choice -= weight;
                if (choice <= 0) break;
            }
            return SampleArea(selected);
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
