using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;

public class BookSpawner : MonoBehaviour
{
    public Transform v16SpawnArea; // Legacy single-area fallback
    [Header("Dairesel Spawn Alanlari")]
    [Tooltip("Sahneye yerlestirdigin BookSpawnCircle objelerini buraya ekle. Daireler atanirsa kitaplar sadece bu alanlarda spawn olur.")]
    public BookSpawnCircle[] spawnCircles;

    [Header("Eski Koridor Alanlari")]
    [Tooltip("Dairesel alanlar bos birakilirsa eski BoxCollider sistemi kullanilir.")]
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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            var networkBook = book.GetComponent<NetworkBook>();
            networkBook.Initialize(bookID, brandID);
            networkBook.NetworkObject.Spawn(true);
        }
    }

    public Vector3[] CreateSpawnPositions(int count)
    {
        if (!ValidateSpawnAreas(out string error)) throw new System.InvalidOperationException(error);
        int guaranteed = GetSpawnZoneCount();
        if (count < guaranteed || count < 0) throw new System.ArgumentException("At least one book per spawn area is required.");
        var positions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            // Ilk turda her daire en az bir kitap alir; kalanlar agirlikli secilir.
            if (i < guaranteed)
                positions[i] = SampleGuaranteedSpawnArea(i);
            else
                positions[i] = SampleSpawnPosition();
        }

        return positions;
    }

    public Vector3 SampleSpawnPosition()
    {
        if (HasCircularSpawnAreas())
        {
            float total = 0f;
            foreach (var circle in spawnCircles)
                if (circle != null && circle.isActiveAndEnabled) total += circle.Weight;

            if (total <= 0f)
                throw new System.InvalidOperationException("BookSpawner: dairesel spawn alanlarinin toplam agirligi sifir.");

            float choice = Random.value * total;

            foreach (var circle in spawnCircles)
            {
                if (circle == null || !circle.isActiveAndEnabled) continue;

                choice -= circle.Weight;
                if (choice <= 0f)
                    return circle.Sample(spawnHeight);
            }

            for (int i = spawnCircles.Length - 1; i >= 0; i--)
                if (spawnCircles[i] != null && spawnCircles[i].isActiveAndEnabled)
                    return spawnCircles[i].Sample(spawnHeight);
        }

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

    private int GetSpawnZoneCount()
    {
        if (HasCircularSpawnAreas())
        {
            int count = 0;
            foreach (var circle in spawnCircles)
                if (circle != null && circle.isActiveAndEnabled) count++;
            return count;
        }

        return corridorAreas == null ? 0 : corridorAreas.Length;
    }

    private bool HasCircularSpawnAreas()
    {
        if (spawnCircles == null || spawnCircles.Length == 0) return false;

        foreach (var circle in spawnCircles)
            if (circle != null && circle.isActiveAndEnabled) return true;

        return false;
    }

    private Vector3 SampleGuaranteedSpawnArea(int index)
    {
        if (HasCircularSpawnAreas())
        {
            int seen = 0;
            foreach (var circle in spawnCircles)
            {
                if (circle == null || !circle.isActiveAndEnabled) continue;
                if (seen++ == index) return circle.Sample(spawnHeight);
            }
        }

        return SampleArea(corridorAreas[index]);
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
        // Circular areas are intentionally manual: you place them in the scene
        // and drag them into Spawn Circles in the Inspector.
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
        if (HasCircularSpawnAreas())
        {
            var seen = new HashSet<BookSpawnCircle>();
            for (int i = 0; i < spawnCircles.Length; i++)
            {
                var circle = spawnCircles[i];
                string reason = circle == null ? "missing reference" :
                    !circle.gameObject.activeInHierarchy ? "inactive GameObject" :
                    !seen.Add(circle) ? "duplicate area" :
                    circle.radius <= 0f || !Finite(circle.radius) ? "invalid radius" :
                    circle.Weight <= 0f || !Finite(circle.Weight) ? "invalid scale/radius" : null;

                if (reason == null) continue;

                error = $"Circular Spawn Area [{i}] '{(circle != null ? circle.name : "Missing")}': {reason}. No books spawned.";
                return false;
            }

            error = null;
            return true;
        }

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
        if (!HasCircularSpawnAreas() && corridorAreas != null && corridorAreas.Length > BookTypeCount * copiesPerBook)
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
        if (spawnCircles != null && spawnCircles.Length > 0)
        {
            foreach (var circle in spawnCircles)
            {
                if (circle == null) continue;
                float radius = circle.radius * Mathf.Abs(circle.transform.lossyScale.x);
                Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.25f);
                Gizmos.DrawWireSphere(circle.transform.position, radius);
            }
        }

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



#if UNITY_EDITOR
    [ContextMenu("Create Spawn Circle")]
    private void CreateSpawnCircle()
    {
        GameObject go = new GameObject("SpawnCircle");
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create book spawn circle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        BookSpawnCircle circle = go.AddComponent<BookSpawnCircle>();
        circle.radius = 2f;
        circle.centerBias = 3f;

        AddCircleReference(circle);
        UnityEditor.Selection.activeGameObject = go;
        UnityEditor.EditorGUIUtility.PingObject(go);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Create 5 Spawn Circles")]
    private void CreateFiveSpawnCircles()
    {
        Vector3[] offsets =
        {
            new Vector3(-4f, 0f, -3f),
            new Vector3( 4f, 0f, -3f),
            new Vector3(-4f, 0f,  3f),
            new Vector3( 4f, 0f,  3f),
            new Vector3( 0f, 0f,  0f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject($"SpawnCircle_{i + 1:00}");
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create book spawn circles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offsets[i];
            go.transform.localRotation = Quaternion.identity;

            BookSpawnCircle circle = go.AddComponent<BookSpawnCircle>();
            circle.radius = 2f;
            circle.centerBias = 3f;
            AddCircleReference(circle);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        if (spawnCircles != null && spawnCircles.Length > 0)
            UnityEditor.Selection.activeGameObject = spawnCircles[spawnCircles.Length - 1].gameObject;
    }

    private void AddCircleReference(BookSpawnCircle circle)
    {
        var list = new List<BookSpawnCircle>();
        if (spawnCircles != null)
        {
            foreach (BookSpawnCircle existing in spawnCircles)
                if (existing != null && !list.Contains(existing))
                    list.Add(existing);
        }

        if (!list.Contains(circle))
            list.Add(circle);

        spawnCircles = list.ToArray();
    }
#endif

    int GetBrandID(int bookID)
    {
        int brand = BrandConfig.GetBrandForBookID(bookID);
        return brand >= 0 ? brand : 0;
    }
}
