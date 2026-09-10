using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bir raf gozu (bolme). Kitaplar artik elle yerlestirilen "Point" child'larina
/// degil, bu objenin KENDI BoxCollider'inin icine hesaplanarak konur.
///
/// NEDEN: RAF gibi 200x olceklenmis FBX'lerin altinda, child bir Transform'un
/// 0.01'lik local offset'i dunyada METRELERE donusuyor. Bu yuzden elle konan
/// Point'ler Scene view'da dogru gozukse bile kitap alakasiz bir yere uculuyordu.
/// Otomatik mod bu problemi tamamen ortadan kaldirir: kitap her zaman
/// gordugun sari kutunun ICINE gider.
/// </summary>
public class ShelfSlot : MonoBehaviour
{
    public enum PlacementMode
    {
        AutoFromCollider,
        ManualPoints
    }

    public enum SpreadAxis
    {
        Auto,
        LocalX,
        LocalY,
        LocalZ
    }

    [Header("Marka")]
    [Min(0)] public int brandID;

    [Header("Kapasite")]
    [Min(1)] public int capacity = 10;

    [Header("Yerlesim Modu")]
    [Tooltip("AutoFromCollider = kitaplar bu objenin BoxCollider'inin icine otomatik dizilir (ONERILEN).\n" +
             "ManualPoints = asagidaki Point listesindeki Transform'lar kullanilir.")]
    public PlacementMode placementMode = PlacementMode.AutoFromCollider;

    [Header("Otomatik Yerlesim Ayarlari")]
    [Tooltip("Kitaplarin yan yana dizilecegi eksen. Auto = en genis YATAY ekseni kendi secer (ONERILEN).")]
    public SpreadAxis spreadAxis = SpreadAxis.Auto;

    [Tooltip("Sol kenardan ilk kitap merkezine mesafe (dunya birimi, parent scale'den bagimsiz).")]
    [Min(0f)] public float firstBookInset = 0.04f;

    [Tooltip("Kitap merkezleri arasindaki mesafe (dunya birimi).")]
    [Min(0.001f)] public float bookSpacing = 0.08f;

    [Tooltip("Kitaplar kutunun ALT yuzeyine otursun mu?")]
    public bool alignToBottom = true;

    [Tooltip("Kitabin ALTI raf tahtasina otursun (pivot degil). Kapaliysa kitabin ortasi " +
             "tahtaya hizalanir ve kitap tahtanin icine gomulur.")]
    public bool restBookOnSurface = true;

    [Tooltip("Raf tahtasinin ustunde birakilacak bosluk (metre).")]
    public float bottomLift = 0.005f;

    [Tooltip("Ince ayar: dunya uzayinda yukseklik/derinlik offset'i. Dizilme eksenindeki " +
             "bilesen yok sayilir; sol kenar mesafesini First Book Inset ile ayarla.")]
    public Vector3 worldOffset = Vector3.zero;

    [Tooltip("Kitabin rafa konuldugundaki ek rotasyonu (slot'un rotasyonu uzerine eklenir).")]
    public Vector3 bookRotationOffsetEuler = Vector3.zero;

    [Header("Guvenlik")]
    [Tooltip("Hesaplanan/atanan nokta, slot merkezinden bu kadar metreden UZAKSA yerlestirme " +
             "iptal edilir ve Console'a hata basilir. Kitabin haritanin ucuna ucmasini engeller.")]
    [Min(0.1f)] public float maxPointDistance = 3f;

    [Header("Elle Nokta Modu (opsiyonel)")]
    [Tooltip("Sadece placementMode = ManualPoints iken kullanilir.")]
    public Transform[] placementPoints = new Transform[10];

    [Header("Debug")]
    public bool showGizmos = true;

    private BookItem[] placedBooks;
    private int ownerBookID = -1;

    public int FilledCount { get; private set; }
    public int OwnerBookID => ownerBookID;
    public bool IsAvailable => FilledCount < capacity;
    public bool IsClaimed => ownerBookID >= 0;

    public ulong NetworkKey { get; private set; }
    private static readonly Dictionary<ulong, ShelfSlot> networkSlots = new Dictionary<ulong, ShelfSlot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => networkSlots.Clear();

    public static void ResetNetworkSession()
    {
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
        {
            slot.EnsureArray();
            foreach (var book in slot.placedBooks)
                if (book != null && book.currentSlot == slot) book.currentSlot = null;
            System.Array.Clear(slot.placedBooks, 0, slot.placedBooks.Length);
            slot.FilledCount = 0;
            slot.ownerBookID = -1;
        }
        networkSlots.Clear();
    }

    public static void BuildNetworkRegistry()
    {
        networkSlots.Clear();
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
        {
            string path = slot.gameObject.scene.path;
            var indices = new Stack<string>();
            for (Transform t = slot.transform; t != null; t = t.parent)
                indices.Push(t.GetSiblingIndex() + ":" + t.name);
            path += "/" + string.Join("/", indices);
            // Stable FNV-1a, unlike string.GetHashCode()/instance IDs across processes.
            ulong key = 14695981039346656037UL;
            unchecked { foreach (char c in path) { key ^= c; key *= 1099511628211UL; } }
            if (key == 0 || networkSlots.ContainsKey(key))
                throw new System.InvalidOperationException("Duplicate shelf network identity: " + path);
            slot.NetworkKey = key;
            networkSlots.Add(key, slot);
        }
    }

    public static ShelfSlot FindNetworkSlot(ulong key)
    {
        if (!networkSlots.TryGetValue(key, out var slot) || slot == null)
        {
            BuildNetworkRegistry();
            networkSlots.TryGetValue(key, out slot);
        }
        return slot;
    }

    public int GetBookIndex(BookItem book)
    {
        EnsureArray();
        return System.Array.IndexOf(placedBooks, book);
    }

    public BookItem PeekLastBook()
    {
        EnsureArray();
        for (int i = placedBooks.Length - 1; i >= 0; i--)
            if (placedBooks[i] != null) return placedBooks[i];
        return null;
    }

    // Replay the host's committed slot/index, including late joins and holes in a shelf.
    public void ApplyNetworkPlacement(BookItem book, int index)
    {
        EnsureArray();
        if (book == null || index < 0 || index >= placedBooks.Length || placedBooks[index] == book) return;
        if (placedBooks[index] != null) RemoveBook(placedBooks[index]);
        if (book.currentSlot != null) book.currentSlot.RemoveBook(book);
        placedBooks[index] = book;
        FilledCount++;
        ownerBookID = book.bookID;
        book.currentSlot = this;
        GameStats.RegisterPlacement(book.bookID);
    }

    void Awake()
    {
        EnsureArray();
    }

    void EnsureArray()
    {
        capacity = Mathf.Max(1, capacity);

        if (placedBooks == null)
        {
            placedBooks = new BookItem[capacity];
            return;
        }

        if (placedBooks.Length != capacity)
        {
            BookItem[] resized = new BookItem[capacity];
            int copyCount = Mathf.Min(placedBooks.Length, capacity);
            for (int i = 0; i < copyCount; i++)
                resized[i] = placedBooks[i];
            placedBooks = resized;
        }
    }

    // ------------------------------------------------------------------
    // Eslesme
    // ------------------------------------------------------------------

    public bool Matches(BookItem book)
    {
        if (book == null || book.brandID != brandID || !IsAvailable)
            return false;

        if (IsClaimed)
            return book.bookID == ownerBookID;

        return !IsBookIDClaimedByAnotherSlot(book.bookID);
    }

    // ------------------------------------------------------------------
    // Konum hesabi
    // ------------------------------------------------------------------

    private BoxCollider ResolveBox()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            return box;

        return GetComponentInChildren<BoxCollider>();
    }

    private Vector3 AxisVector(SpreadAxis axis)
    {
        switch (axis)
        {
            case SpreadAxis.LocalY: return Vector3.up;
            case SpreadAxis.LocalZ: return Vector3.forward;
            default: return Vector3.right;
        }
    }

    private float AxisComponent(Vector3 v, SpreadAxis axis)
    {
        switch (axis)
        {
            case SpreadAxis.LocalY: return v.y;
            case SpreadAxis.LocalZ: return v.z;
            default: return v.x;
        }
    }

    /// <summary>
    /// Auto modda: kitaplari dizmek icin en genis YATAY ekseni secer.
    /// Boylece dikey bir eksen secilip 10 kitabin ust uste binmesi imkansiz olur.
    /// </summary>
    private SpreadAxis ResolveSpreadAxis(BoxCollider box)
    {
        if (spreadAxis != SpreadAxis.Auto)
            return spreadAxis;

        Transform space = box.transform;
        SpreadAxis[] candidates = { SpreadAxis.LocalX, SpreadAxis.LocalY, SpreadAxis.LocalZ };

        SpreadAxis best = SpreadAxis.LocalX;
        float bestScore = -1f;
        SpreadAxis longest = SpreadAxis.LocalX;
        float longestLength = -1f;

        foreach (SpreadAxis axis in candidates)
        {
            float size = Mathf.Abs(AxisComponent(box.size, axis));
            Vector3 worldVector = space.TransformVector(AxisVector(axis) * size);
            float length = worldVector.magnitude;

            if (length > longestLength)
            {
                longestLength = length;
                longest = axis;
            }

            if (length < 0.0001f)
                continue;

            // 1 = tamamen yatay, 0 = tamamen dikey
            float horizontality = 1f - Mathf.Abs(Vector3.Dot(worldVector / length, Vector3.up));
            if (horizontality < 0.5f)
                continue;

            float score = length * horizontality;
            if (score > bestScore)
            {
                bestScore = score;
                best = axis;
            }
        }

        return bestScore > 0f ? best : longest;
    }

    /// <summary>
    /// Kitabin pivotu ile ALT kenari arasindaki mesafe (verilen rotasyonda, dunya biriminde).
    /// Kitabi tasimadan, matematiksel olarak hesaplar -- boylece animasyon hedefi ile
    /// nihai konum birebir ayni olur ve kitap yerine oturunca ziplamaz.
    /// </summary>
    private float ComputeSurfaceLift(BookItem book, Quaternion slotRotation)
    {
        if (book == null || !TryGetLocalBounds(book, out Bounds local))
            return 0f;

        Vector3 scale = book.OriginalScale;
        Vector3 c = Vector3.Scale(local.center, scale);
        Vector3 e = Vector3.Scale(local.extents, scale);

        Matrix4x4 m = Matrix4x4.Rotate(slotRotation * book.NativeRotation);

        float centerY = m.m10 * c.x + m.m11 * c.y + m.m12 * c.z;
        float halfY = Mathf.Abs(m.m10) * Mathf.Abs(e.x)
                    + Mathf.Abs(m.m11) * Mathf.Abs(e.y)
                    + Mathf.Abs(m.m12) * Mathf.Abs(e.z);

        return halfY - centerY;
    }

    /// <summary>Kitabin KENDI local uzayindaki sinirlari (mevcut scale/rotasyondan bagimsiz).</summary>
    private static bool TryGetLocalBounds(BookItem book, out Bounds localBounds)
    {
        localBounds = new Bounds();
        bool found = false;

        Matrix4x4 toRoot = book.transform.worldToLocalMatrix;

        Renderer[] renderers = book.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            Bounds source;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                source = filter.sharedMesh.bounds;
            else
                source = renderer.localBounds;

            Matrix4x4 m = toRoot * renderer.transform.localToWorldMatrix;
            EncapsulateTransformedBox(ref localBounds, ref found, m, source);
        }

        if (found)
            return true;

        BoxCollider[] boxes = book.GetComponentsInChildren<BoxCollider>();
        foreach (BoxCollider box in boxes)
        {
            if (box == null || !box.enabled)
                continue;

            Matrix4x4 m = toRoot * box.transform.localToWorldMatrix;
            EncapsulateTransformedBox(ref localBounds, ref found, m, new Bounds(box.center, box.size));
        }

        return found;
    }

    private static void EncapsulateTransformedBox(ref Bounds bounds, ref bool found, Matrix4x4 matrix, Bounds box)
    {
        Vector3 c = box.center;
        Vector3 e = box.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                c.x + ((i & 1) == 0 ? -e.x : e.x),
                c.y + ((i & 2) == 0 ? -e.y : e.y),
                c.z + ((i & 4) == 0 ? -e.z : e.z));

            Vector3 point = matrix.MultiplyPoint3x4(corner);

            if (!found)
            {
                bounds = new Bounds(point, Vector3.zero);
                found = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }
    }

    /// <summary>Bir kitabin dunya uzayindaki gorsel sinirlari (renderer, yoksa collider).</summary>
    private static bool TryGetWorldBounds(BookItem book, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;

        Renderer[] renderers = book.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (found)
            return true;

        Collider[] colliders = book.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;

            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return found;
    }

    /// <summary>Slot'un referans merkezi. Guvenlik kontrolu buna gore yapilir.</summary>
    public Vector3 SlotCenter
    {
        get
        {
            BoxCollider box = ResolveBox();
            if (box != null)
                return box.transform.TransformPoint(box.center);

            Collider any = GetComponentInChildren<Collider>();
            if (any != null)
                return any.bounds.center;

            return transform.position;
        }
    }

    /// <summary>
    /// index numarali kitabin dunya konumu ve rotasyonu -- KITAP verilirse kitabin
    /// kendi boyutuna gore raf yuzeyine oturtulmus HALI doner. Animasyonun hedefi ile
    /// son konumun ayni olmasi icin her iki taraf da bu fonksiyonu kullanmali.
    /// </summary>
    public bool TryGetPlacementPose(int index, BookItem book, out Vector3 position, out Quaternion rotation)
    {
        if (!TryGetPlacementPose(index, out position, out rotation))
            return false;

        if (book != null && alignToBottom && restBookOnSurface)
            position += Vector3.up * ComputeSurfaceLift(book, rotation);

        return true;
    }

    /// <summary>index numarali kitabin dunya konumu ve rotasyonu.</summary>
    public bool TryGetPlacementPose(int index, out Vector3 position, out Quaternion rotation)
    {
        position = SlotCenter;
        rotation = transform.rotation * Quaternion.Euler(bookRotationOffsetEuler);

        if (index < 0 || index >= capacity)
            return false;

        bool resolved;

        if (placementMode == PlacementMode.ManualPoints)
        {
            resolved = TryGetManualPose(index, ref position, ref rotation);

            // Elle atanan nokta yok / bozuksa otomatige dus, kitap ucmasin.
            if (!resolved)
                resolved = TryGetAutoPose(index, ref position, ref rotation);
        }
        else
        {
            resolved = TryGetAutoPose(index, ref position, ref rotation);
        }

        if (!resolved)
            return false;

        float distance = Vector3.Distance(position, SlotCenter);
        if (distance > maxPointDistance)
        {
            Debug.LogError(
                $"ShelfSlot '{name}': {index}. kitap noktasi slot merkezinden {distance:0.00} m uzakta " +
                $"(limit {maxPointDistance} m). Yerlestirme iptal edildi. Sebep genelde parent " +
                $"objedeki buyuk scale (orn. 200x RAF) yuzunden elle konmus Point child'larinin " +
                $"alakasiz bir dunya konumuna dusmesidir. Cozum: Placement Mode = AutoFromCollider.");
            return false;
        }

        return true;
    }

    private bool TryGetManualPose(int index, ref Vector3 position, ref Quaternion rotation)
    {
        if (placementPoints == null || index >= placementPoints.Length)
            return false;

        Transform point = placementPoints[index];
        if (point == null)
            return false;

        position = point.position;
        rotation = point.rotation * Quaternion.Euler(bookRotationOffsetEuler);
        return true;
    }

    private bool TryGetAutoPose(int index, ref Vector3 position, ref Quaternion rotation)
    {
        BoxCollider box = ResolveBox();
        if (box == null)
            return false;

        Transform space = box.transform;
        SpreadAxis resolvedAxis = ResolveSpreadAxis(box);
        Vector3 axis = AxisVector(resolvedAxis);
        float axisSize = Mathf.Abs(AxisComponent(box.size, resolvedAxis));
        Vector3 worldAxis = space.TransformVector(axis);
        if (worldAxis.sqrMagnitude < 0.00000001f)
            return false;

        // Start at the negative edge of the shelf's spread axis. Add distances
        // AFTER transforming so even a 200x parent keeps the 0.04 / 0.08 spacing.
        Vector3 leftEdge = space.TransformPoint(box.center - axis * (axisSize * 0.5f));
        position = leftEdge + worldAxis.normalized *
            (Mathf.Max(0f, firstBookInset) + Mathf.Max(0.001f, bookSpacing) * index);

        if (alignToBottom)
            position.y = box.bounds.min.y + bottomLift;

        // Old scene offsets must not shift the fixed left-edge inset.
        position += worldOffset - Vector3.Project(worldOffset, worldAxis);
        rotation = space.rotation * Quaternion.Euler(bookRotationOffsetEuler);
        return true;
    }

    public bool TryGetNextPlacementPose(out Vector3 position, out Quaternion rotation)
    {
        return TryGetNextPlacementPose(null, out position, out rotation);
    }

    public bool TryGetNextPlacementPose(BookItem book, out Vector3 position, out Quaternion rotation)
    {
        EnsureArray();

        int index = FindFreeIndex();
        if (index < 0)
        {
            position = SlotCenter;
            rotation = transform.rotation;
            return false;
        }

        return TryGetPlacementPose(index, book, out position, out rotation);
    }

    // Eski API -- baska bir script cagirirsa diye duruyor.
    public Transform GetNextPlacementPoint()
    {
        int index = FindFreeIndex();
        if (index < 0 || placementPoints == null || index >= placementPoints.Length)
            return null;

        return placementPoints[index];
    }

    // ------------------------------------------------------------------
    // Yerlestirme / alma
    // ------------------------------------------------------------------

    public bool PlaceBook(BookItem book)
    {
        EnsureArray();

        if (!Matches(book))
            return false;

        int index = FindFreeIndex();
        if (index < 0)
            return false;

        // Kitabi tanitarak istiyoruz: donen konum, kitabin ALTI raf tahtasina oturacak
        // sekilde zaten hesaplanmis oluyor. Animasyon da ayni fonksiyonu kullandigi icin
        // kitap yerine varinca ziplamaz.
        if (!TryGetPlacementPose(index, book, out Vector3 position, out Quaternion rotation))
            return false;

        if (!IsClaimed)
            ownerBookID = book.bookID;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(position, rotation * book.NativeRotation);
        book.transform.localScale = book.OriginalScale;
        book.SetHeld(false);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        placedBooks[index] = book;
        FilledCount++;
        book.currentSlot = this;

        GameStats.RegisterPlacement(book.bookID);
        return true;
    }

    public BookItem TakeLastBook()
    {
        EnsureArray();

        if (FilledCount <= 0)
            return null;

        for (int i = placedBooks.Length - 1; i >= 0; i--)
        {
            BookItem book = placedBooks[i];
            if (book == null)
                continue;

            placedBooks[i] = null;
            FilledCount = Mathf.Max(0, FilledCount - 1);
            GameStats.UnregisterPlacement(book.bookID);
            book.currentSlot = null;

            if (FilledCount == 0)
                ownerBookID = -1;

            return book;
        }

        return null;
    }

    public BookItem TakeFirstBook()
    {
        return TakeLastBook();
    }

    public void RemoveBook(BookItem book)
    {
        if (book == null || placedBooks == null)
            return;

        int index = System.Array.IndexOf(placedBooks, book);
        if (index < 0)
            return;

        placedBooks[index] = null;
        FilledCount = Mathf.Max(0, FilledCount - 1);
        GameStats.UnregisterPlacement(book.bookID);
        book.currentSlot = null;

        if (FilledCount == 0)
            ownerBookID = -1;
    }

    private bool IsBookIDClaimedByAnotherSlot(int bookID)
    {
        ShelfSlot[] allSlots = FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None);
        foreach (ShelfSlot slot in allSlots)
        {
            if (slot == null || slot == this || !slot.IsClaimed)
                continue;

            if (slot.OwnerBookID == bookID)
                return true;
        }

        return false;
    }

    private int FindFreeIndex()
    {
        EnsureArray();

        for (int i = 0; i < placedBooks.Length; i++)
        {
            if (placedBooks[i] == null)
                return i;
        }

        return -1;
    }

    // ------------------------------------------------------------------
    // Editor yardimcilari
    // ------------------------------------------------------------------

    [ContextMenu("Manuel Point'leri Otomatik Konumlara Tasi")]
    public void SnapManualPointsToAutoPositions()
    {
        if (placementPoints == null)
            return;

        int moved = 0;
        for (int i = 0; i < placementPoints.Length && i < capacity; i++)
        {
            Transform point = placementPoints[i];
            if (point == null)
                continue;

            Vector3 position = SlotCenter;
            Quaternion rotation = transform.rotation;

            if (!TryGetAutoPose(i, ref position, ref rotation))
                continue;

            point.SetPositionAndRotation(position, rotation);
            moved++;
        }

        Debug.Log($"ShelfSlot '{name}': {moved} adet Point otomatik konumlara tasindi.");
    }

    [ContextMenu("Slot Bilgisini Yaz")]
    public void LogSlotInfo()
    {
        BoxCollider box = ResolveBox();
        if (box == null)
        {
            Debug.LogWarning($"ShelfSlot '{name}': BoxCollider yok.");
            return;
        }

        Transform space = box.transform;
        Vector3 worldX = space.TransformVector(Vector3.right * box.size.x);
        Vector3 worldY = space.TransformVector(Vector3.up * box.size.y);
        Vector3 worldZ = space.TransformVector(Vector3.forward * box.size.z);
        SpreadAxis resolved = ResolveSpreadAxis(box);

        float worldStep = Mathf.Max(0.001f, bookSpacing);

        Debug.Log(
            $"[Slot Bilgisi] '{name}'\n" +
            $"  Merkez (dunya): {SlotCenter}\n" +
            $"  Kutu dunya olculeri -> LocalX: {worldX.magnitude:0.000} m, " +
            $"LocalY: {worldY.magnitude:0.000} m, LocalZ: {worldZ.magnitude:0.000} m\n" +
            $"  Secilen dizilme ekseni: {resolved} (ayar: {spreadAxis})\n" +
            $"  Kapasite: {capacity}, kitaplar arasi mesafe: {worldStep * 1000f:0.0} mm\n" +
            $"  Alta oturt: {alignToBottom}, kitabi yuzeye kaldir: {restBookOnSurface}");
    }

    void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        BoxCollider box = ResolveBox();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        for (int i = 0; i < capacity; i++)
        {
            Vector3 position = SlotCenter;
            Quaternion rotation = transform.rotation;

            bool ok = placementMode == PlacementMode.ManualPoints
                ? (TryGetManualPose(i, ref position, ref rotation) || TryGetAutoPose(i, ref position, ref rotation))
                : TryGetAutoPose(i, ref position, ref rotation);

            if (!ok)
                continue;

            bool filled = placedBooks != null && i < placedBooks.Length && placedBooks[i] != null;
            Gizmos.color = filled ? new Color(0.2f, 1f, 0.3f, 1f) : new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawSphere(position, 0.015f);
        }
    }
}
