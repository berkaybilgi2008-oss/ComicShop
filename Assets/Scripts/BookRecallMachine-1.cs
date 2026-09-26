using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Kitap Isinlama Makinesi.
///
/// Adi verilen kitabin KAYIP bir kopyasini kendine cagirir.
/// Kayip = zeminin altina dusmus ya da oyun alaninin disina cikmis.
/// Oyun alanindaki kitaplar cagrilamaz -- bu bir kurtarma araci, kitap bulma
/// hilesi degil.
///
/// Ayrica zeminin altina dusen kitaplari otomatik geri getirir -- kitaplarin
/// "yok olmasi"nin en yaygin sebebi budur.
///
/// KURULUM:
/// 1) Sahneye bir Cube ekle (GameObject > 3D Object > Cube).
/// 2) Uzerine bu script'i ekle.
/// 3) Cikis Noktasi bos birakilirsa kitap makinenin biraz onunde belirir.
/// 4) Oyunda makineye bak ve E'ye bas.
/// </summary>
public class BookRecallMachine : MonoBehaviour
{
    [Header("Bekleme Suresi")]
    [Tooltip("Iki isinlama arasindaki bekleme (saniye). Yayinda 60 onerilir, testte 1 yap.")]
    [Min(0f)] public float recallCooldown = 60f;

    public enum RecallMode
    {
        AllLostBooks,
        SpecificBookID
    }

    [Header("Hedef")]
    [Tooltip("AllLostBooks = kayip olan HER kitabi geri getirir (onerilen).\n" +
             "SpecificBookID = sadece asagidaki ID'ye sahip kayip kopyalari getirir.\n" +
             "Makine zaten sadece kayip kitaplari cagirabildigi icin ID sormak " +
             "oyuncuya gereksiz yuk -- hangi kitabin kayboldugunu bilemez.")]
    public RecallMode recallMode = RecallMode.AllLostBooks;

    [Tooltip("Sadece SpecificBookID modunda kullanilir.")]
    [Min(0)] public int targetBookID = 0;

    [Tooltip("Bir oyuncunun ELINDEKI kitap cagirilmasin. Co-op'ta elden kitap " +
             "kapmak kotu hissettirir, acik birak.")]
    public bool skipHeldBooks = true;


    [Header("Cikis")]
    [Tooltip("Kitabin belirecegi nokta. Bos birakilirsa makinenin USTU kullanilir.")]
    public Transform outputPoint;
    [Tooltip("Cikis noktasi bos ise kitabin makinenin kac metre ustunde belirecegi.")]
    [Min(0f)] public float defaultOutputHeight = 1f;
    [Tooltip("Cikis noktasi etrafinda kucuk rastgele sapma -- kitaplar ust uste binmesin.")]
    [Min(0f)] public float outputSpread = 0.12f;

    [Header("Etkilesim")]
    public KeyCode useKey = KeyCode.E;
    [Tooltip("Makineye bu mesafeden yakinken calisir.")]
    [Min(0.5f)] public float useRange = 3f;
    [Tooltip("Oyuncunun makineye BAKMASI sart olsun mu? Yer platformlarinda kapali " +
             "birak -- uzerine basip E'ye basmak yeter.")]
    public bool requireLookingAt = false;
    [Tooltip("Bakma sarti aciksa, ne kadar dogru bakmasi gerektigi. 1 = tam ustune.")]
    [Range(0f, 1f)] public float lookThreshold = 0.5f;

    [Header("Oyun Alani")]
    [Tooltip("Dukkanin sinirlarini kaplayan bir Collider (Box Collider yeterli, " +
             "Is Trigger acik olsun). Bu alanin DISINA cikan kitap KAYIP sayilir.\n" +
             "Bos birakilirsa sadece 'Lost Below Y' kontrolu yapilir.")]
    public Collider playArea;
    [Tooltip("Oyun alanini dukkanin gercek icerigine (raflar ve kitap dogus alanlari) gore otomatik genislet. " +
             "Dukkan tasinip buyudugunde eski kutu, dukkanin icindeki kitaplari yanlislikla KAYIP saymasin.")]
    public bool expandPlayAreaToShop = true;
    [Tooltip("Raflarin ve dogus alanlarinin cevresine eklenen yatay pay (metre).")]
    [Min(0f)] public float shopMargin = 1.5f;

    [Header("Kayip Kitabi Dondurma")]
    [Tooltip("Oyun alaninin disina cikan kitap, belirtilen sure sonra DONDURULUR. " +
             "Sonsuza kadar dusup bosuna fizik hesabi yapmasini engeller. " +
             "E ile cagirinca tekrar normale doner.")]
    public bool freezeLostBooks = true;
    [Tooltip("Kitap kayip sayildiktan kac saniye sonra dondurulsun.")]
    [Min(0f)] public float freezeDelay = 5f;

    [Header("Kayip Kitap Kurtarma")]
    [Tooltip("Dukkanin disina cikan ya da zeminin altina dusen kitabi birkac saniye sonra " +
             "dukkandaki kitap dogus alanina geri getirir. Isinlama pedine gitmek gerekmez.")]
    public bool returnLostBooksToShop = true;
    [Tooltip("Kitap bu kadar saniye boyunca kayip kalirsa geri getirilir (havaya atilan kitap yanlislikla isinlanmasin).")]
    [Min(1f)] public float returnDelay = 4f;
    // Eski alan: sahnede kapali kayitliydi ve kayip kitaplar pede gitmeden hic geri gelmiyordu.
    // Yerini returnLostBooksToShop aldi; seri hale getirme uyumu icin duruyor.
    [HideInInspector] public bool autoRecoverLostBooks = true;
    [Tooltip("Bu yukseklikten asagi dusen kitap kayip sayilir.")]
    public float lostBelowY = -5f;
    [Tooltip("Kayip kontrolunun kac saniyede bir yapilacagi.")]
    [Min(0.5f)] public float lostCheckInterval = 3f;
    [Tooltip("Guvenli duruma donmeden bu kadar denemeden sonra beklenir. Sonsuz " +
             "dongu olusmasini engeller -- kitap surekli geri dusuyorsa sorun " +
             "pedin altinda zemin olmamasidir.")]
    [Min(1)] public int maxRecoveryAttempts = 3;

    [Header("Zemin Kontrolu")]
    [Tooltip("Kitabi birakmadan once cikis noktasinin altinda zemin var mi diye bakar. " +
             "Zemin yoksa kitap birakilmaz ve Console'a hata yazilir.")]
    public bool requireGroundBelowOutput = true;
    [Tooltip("Zemin aramasinin cikis noktasindan asagi ne kadar ineceginin siniri.")]
    [Min(1f)] public float groundSearchDistance = 30f;
    [Tooltip("Kitap zeminin kac metre ustunde birakilsin.")]
    [Min(0f)] public float dropHeightAboveGround = 0.4f;

    [Min(5f)] public float recoveryRetryDelay = 30f;
    private float lastRecallTime = -9999f;
    private float lastLostCheckTime;
    private Camera playerCamera;

    // Hangi kitap kac kez kurtarildi -- sonsuz donguyu kirmak icin.
    private readonly System.Collections.Generic.Dictionary<BookItem, RecoveryBudget> recoveryCounts
        = new System.Collections.Generic.Dictionary<BookItem, RecoveryBudget>();

    private readonly System.Collections.Generic.List<BookItem> staleBooks = new System.Collections.Generic.List<BookItem>();

    // Hangi kitap ne zamandan beri kayip -- dondurma zamanlamasi icin.
    private readonly System.Collections.Generic.Dictionary<BookItem, float> lostSince
        = new System.Collections.Generic.Dictionary<BookItem, float>();

    /// <summary>Kalan bekleme suresi. UI'a baglamak icin.</summary>
    public float CooldownRemaining =>
        Mathf.Max(0f, recallCooldown - (Time.time - lastRecallTime));

    public bool IsReady => CooldownRemaining <= 0f;

    private Vector3 OutputPosition =>
        outputPoint != null ? outputPoint.position : transform.position + Vector3.up * defaultOutputHeight;

    void Update()
    {
        bool networkScene = ConnectionManager.Instance != null;
        bool authority = !networkScene || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        if (authority && Time.time - lastLostCheckTime >= lostCheckInterval)
        {
            lastLostCheckTime = Time.time;
            ScanLostBooks();
        }

        if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(ShopSettings.Key(ShopAction.Recall)) && IsPlayerLooking())
        {
            if (recallMode == RecallMode.AllLostBooks)
                TryRecallAllLost();
            else
                TryRecall(targetBookID);
        }
    }

    // ------------------------------------------------------------------
    // Isinlama
    // ------------------------------------------------------------------

    /// <summary>Kayip olan butun kitaplari geri getirir. Kurtarma araci olarak asil kullanim bu.</summary>
    public bool TryRecallAllLost()
    {
        if (ForwardClientRequest()) return false;
        if (!IsReady)
        {
            Debug.Log($"Isinlama makinesi: {CooldownRemaining:0.0} saniye daha bekle.");
            return false;
        }

        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        int brought = 0;

        foreach (BookItem book in books)
        {
            if (book == null || book.IsHeld || book.currentSlot != null)
                continue;

            if (!IsLost(book))
                continue;

            if (Teleport(book)) { recoveryCounts.Remove(book); brought++; }
        }

        if (brought == 0)
        {
            Debug.Log("Isinlama makinesi: kayip kitap yok, hepsi oyun alaninin icinde.");
            return false;
        }

        lastRecallTime = Time.time;
        Debug.Log($"Isinlama makinesi: {brought} kayip kitap geri getirildi.");
        return true;
    }

    /// <summary>Verilen ID'li kitabin raftaki olmayan bir kopyasini cagirir.</summary>
    public bool TryRecall(int bookID)
    {
        if (ForwardClientRequest()) return false;
        if (!IsReady)
        {
            Debug.Log($"Isinlama makinesi: {CooldownRemaining:0.0} saniye daha bekle.");
            return false;
        }

        BookItem target = FindRecallTarget(bookID);

        if (target == null)
        {
            LogWhyNothingFound(bookID);
            return false;
        }

        if (!Teleport(target))
            return false;

        recoveryCounts.Remove(target);
        lastRecallTime = Time.time;

        Debug.Log($"Isinlama makinesi: '{target.DisplayName}' cagrildi.");
        return true;
    }

    /// <summary>
    /// Cagrilacak kopyayi secer. Once gercekten kaybolmus olani (zemin alti),
    /// yoksa makineye EN UZAK olani -- yani oyuncunun bulmasi en zor olani.
    /// </summary>
    private BookItem FindRecallTarget(int bookID)
    {
        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);

        BookItem lost = null;
        BookItem farthest = null;
        float farthestDistance = -1f;

        Vector3 origin = OutputPosition;

        foreach (BookItem book in books)
        {
            if (!IsRecallable(book, bookID))
                continue;

            // IsRecallable zaten kayip olmayanlari eledi; en uzaktakini secmek
            // birden fazla kayip kopya varsa hangisinin gelecegini belirler.
            if (book.transform.position.y < lostBelowY)
            {
                lost = book;
                break;
            }

            float distance = Vector3.SqrMagnitude(book.transform.position - origin);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthest = book;
            }
        }

        return lost != null ? lost : farthest;
    }

    private bool IsRecallable(BookItem book, int bookID)
    {
        if (book == null || book.bookID != bookID)
            return false;

        // Rafa yerlestirilmis kitaplar cagirilmaz.
        if (book.currentSlot != null)
            return false;

        if (skipHeldBooks && book.IsHeld)
            return false;

        // ONEMLI: makine bir "kitap bulma" araci degil, bir KURTARMA aracidir.
        // Sadece gercekten kaybolmus kopyalari cagirir. Boylece oyuncu oyun
        // alanindaki kitaplari makineyle toplayamaz -- istismar kendiliginden biter.
        return IsLost(book);
    }

    /// <summary>Cagirma basarisiz olunca sebebini acikca yazar.</summary>
    private void LogWhyNothingFound(int bookID)
    {
        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);

        int total = 0, shelved = 0, held = 0, inside = 0;

        foreach (BookItem book in books)
        {
            if (book == null || book.bookID != bookID)
                continue;

            total++;

            if (book.currentSlot != null) shelved++;
            else if (book.IsHeld) held++;
            else if (!IsLost(book)) inside++;
        }

        string area = HasPlayBounds
            ? $"Oyun alani atanmis, sinirlari: {PlayBounds.min} .. {PlayBounds.max}"
            : $"OYUN ALANI ATANMAMIS -- sadece 'Lost Below Y' ({lostBelowY}) kontrolu yapiliyor. " +
              "Duvardan disari cikan ama yere dusmeyen kitaplar kayip sayilmaz!";

        // Baska hangi kitaplar kayip? Kullanici yanlis ID aramis olabilir.
        System.Collections.Generic.HashSet<int> lostIDs = new System.Collections.Generic.HashSet<int>();

        foreach (BookItem book in books)
        {
            if (book == null || book.IsHeld || book.currentSlot != null)
                continue;

            if (IsLost(book))
                lostIDs.Add(book.bookID);
        }

        string lostLine = lostIDs.Count > 0
            ? $"  ANCAK su ID'lerde kayip kitap VAR: {string.Join(", ", lostIDs)}. " +
              $"Recall Mode'u AllLostBooks yaparsan hepsi gelir."
            : "  Su an hicbir kitap kayip degil.";

        Debug.LogWarning(
            $"Isinlama makinesi: {bookID} numarali kitabin KAYIP kopyasi bulunamadi.\n" +
            $"  Toplam kopya: {total}   Rafta: {shelved}   Elde: {held}   Alan icinde: {inside}\n" +
            $"  {area}\n" +
            lostLine);
    }

    [ContextMenu("Durum Raporu")]
    public void LogStatusReport()
    {
        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"[Isinlama Makinesi] Hedef kitap ID: {targetBookID}");
        sb.AppendLine(HasPlayBounds
            ? $"Oyun alani: {PlayBounds.min} .. {PlayBounds.max}"
            : "Oyun alani: ATANMAMIS");
        sb.AppendLine($"Lost Below Y: {lostBelowY}");
        sb.AppendLine();

        foreach (BookItem book in books)
        {
            if (book == null || book.bookID != targetBookID)
                continue;

            string state = book.currentSlot != null ? "RAFTA"
                         : book.IsHeld ? "ELDE"
                         : IsLost(book) ? "KAYIP"
                         : "alan icinde";

            sb.AppendLine($"  {book.name}  konum {book.transform.position}  -> {state}");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>Kitap kayip mi? Zeminin altina dustuyse ya da oyun alaninin disindaysa.</summary>
    public bool IsLost(BookItem book)
    {
        if (book == null)
            return false;

        Vector3 position = book.transform.position;

        if (position.y < lostBelowY)
            return true;

        if (HasPlayBounds && !PlayBounds.Contains(position))
            return true;

        return false;
    }

    // ------------------------------------------------------------------
    // Oyun alani
    // ------------------------------------------------------------------

    private Bounds playBounds;
    private bool playBoundsReady, hasPlayBounds;

    private bool HasPlayBounds { get { if (!playBoundsReady) RebuildPlayBounds(); return hasPlayBounds; } }

    /// <summary>Kayip kontrolunde kullanilan alan: atanmis kutu + dukkanin gercek icerigi.</summary>
    public Bounds PlayBounds { get { if (!playBoundsReady) RebuildPlayBounds(); return playBounds; } }

    /// <summary>
    /// Eski GameZone kutusu dukkan tasindiginda dukkanin dogu tarafini (x > 44.7) disarida
    /// birakiyordu: orada dogan ya da birakilan her kitap KAYIP sayilip donduruluyordu.
    /// Alan artik raflari ve kitap dogus alanlarini her zaman kapsar.
    /// </summary>
    public void RebuildPlayBounds()
    {
        playBoundsReady = true;
        hasPlayBounds = false;
        if (playArea == null) return;

        if (playArea.enabled && playArea.gameObject.activeInHierarchy) playBounds = playArea.bounds;
        else if (playArea is BoxCollider box) playBounds = WorldBounds(box.transform, box.center, box.size);
        else return; // Kapali bir collider'in bounds'u bostur; her kitabi kayip saymasin.
        hasPlayBounds = true;
        if (!expandPlayAreaToShop) return;

        Bounds shop = default;
        bool any = false;
        foreach (var spawner in FindObjectsByType<BookSpawner>(FindObjectsSortMode.None))
        {
            if (spawner == null || spawner.gameObject.scene != gameObject.scene) continue;
            if (spawner.corridorAreas != null && spawner.corridorAreas.Length > 0)
            {
                foreach (var zone in spawner.corridorAreas)
                    if (zone != null && zone.gameObject.activeInHierarchy)
                        Include(ref shop, ref any, WorldBounds(zone.transform, zone.center, zone.size));
            }
            else
            {
                Transform area = spawner.v16SpawnArea != null ? spawner.v16SpawnArea : spawner.transform;
                Include(ref shop, ref any, WorldBounds(area, Vector3.zero, new Vector3(spawner.areaSize.x, 0f, spawner.areaSize.y)));
            }
        }
        foreach (var slot in FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
            if (slot != null && slot.gameObject.scene == gameObject.scene)
                Include(ref shop, ref any, new Bounds(slot.transform.position, Vector3.zero));
        if (!any) return;

        Vector3 min = shop.min - new Vector3(shopMargin, 1f, shopMargin);
        Vector3 max = shop.max + new Vector3(shopMargin, 3f, shopMargin);
        shop.SetMinMax(min, max);
        bool grew = !playBounds.Contains(min) || !playBounds.Contains(max);
        playBounds.Encapsulate(shop);
        if (grew)
            Debug.Log($"Isinlama makinesi: oyun alani dukkani kapsayacak sekilde genisletildi: {playBounds.min} .. {playBounds.max}", this);
    }

    private static void Include(ref Bounds total, ref bool any, Bounds part)
    {
        if (!any) { total = part; any = true; }
        else total.Encapsulate(part);
    }

    private static Bounds WorldBounds(Transform space, Vector3 center, Vector3 size)
    {
        Bounds result = default;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + Vector3.Scale(size * 0.5f, new Vector3(
                (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 world = space.TransformPoint(corner);
            if (i == 0) result = new Bounds(world, Vector3.zero);
            else result.Encapsulate(world);
        }
        return result;
    }

    private readonly System.Text.StringBuilder newlyLost = new System.Text.StringBuilder();
    private int newlyLostCount;

    /// <summary>
    /// Kayip kitaplari tarar. Iki is yapar:
    ///   - Kayip kalan kitabi belirli sure sonra DONDURUR (bosuna fizik hesabi olmasin).
    ///   - Birkac saniye kayip kalan kitabi dukkandaki kitap dogus alanina geri getirir.
    /// </summary>
    private void ScanLostBooks()
    {
        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        int recovered = 0;
        int frozen = 0;
        staleBooks.Clear();
        foreach (var pair in recoveryCounts) if (pair.Key == null) staleBooks.Add(pair.Key);
        foreach (var book in staleBooks) { recoveryCounts.Remove(book); lostSince.Remove(book); }

        foreach (BookItem book in books)
        {
            if (book == null) continue;
            if (book.IsHeld || book.currentSlot != null || !IsLost(book))
            {
                lostSince.Remove(book);
                if (recoveryCounts.TryGetValue(book, out var safeBudget))
                {
                    safeBudget.ObserveSafe();
                    if (safeBudget.Attempts == 0 && safeBudget.RetryAt == 0) recoveryCounts.Remove(book);
                }
                continue;
            }

            if (!lostSince.ContainsKey(book))
            {
                lostSince[book] = Time.time;
                if (newlyLostCount < 5)
                {
                    Vector3 p = book.transform.position;
                    newlyLost.Append($"\n  {book.DisplayName} ({book.name}) @ ({p.x:0.0}, {p.y:0.0}, {p.z:0.0}) - " +
                        (p.y < lostBelowY ? "zeminin altina dustu" : "oyun alaninin disina cikti"));
                }
                newlyLostCount++;
            }
            float lostFor = Time.time - lostSince[book];

            // Dukkana geri getir (pede gitmeye gerek yok). Kisa sure disari cikan kitaba dokunma.
            if (returnLostBooksToShop && lostFor >= returnDelay)
            {
                if (!recoveryCounts.TryGetValue(book, out var budget))
                    recoveryCounts.Add(book, budget = new RecoveryBudget());
                if (budget.Attempt(Time.timeAsDouble, maxRecoveryAttempts, recoveryRetryDelay))
                {
                    if (Teleport(book, true)) { recovered++; continue; }
                    if (budget.RetryAt > Time.timeAsDouble)
                        Debug.LogWarning($"[BOOK RECOVERY] {book.name}: no safe output. Retrying in {recoveryRetryDelay:0}s; manual recall remains available.", this);
                }
            }

            // Dondurma
            if (freezeLostBooks && lostFor >= freezeDelay)
            {
                Rigidbody body = book.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                    frozen++;
                }
            }

        }

        if (newlyLostCount > 0)
            Debug.LogWarning($"[Kayip kitap] {newlyLostCount} kitap kayboldu" +
                (returnLostBooksToShop ? $", {returnDelay:0} sn icinde dukkana geri getirilecek." : ".") + newlyLost, this);
        newlyLost.Clear();
        newlyLostCount = 0;

        if (recovered > 0)
            Debug.Log($"Isinlama makinesi: {recovered} kayip kitap dukkana geri getirildi.");

        if (frozen > 0)
            Debug.Log($"Isinlama makinesi: {frozen} kayip kitap donduruldu, cagrilmayi bekliyor.");
    }

    /// <summary>
    /// Kitabi guvenli bir yere tasir. preferShop: once dukkandaki kitap dogus alanini dener
    /// (otomatik kurtarma; ped dukkanin disinda olabilir). Degilse once makinenin cikisini dener
    /// (oyuncu pedde E'ye bastiginda). Zemin bulunamazsa tasimaz ve false doner.
    /// </summary>
    private bool Teleport(BookItem book, bool preferShop = false)
    {
        Vector3 target = default;
        bool found = preferShop && TryShopOutput(out target);
        if (!found)
        {
            Vector3 pad = OutputPosition + new Vector3(Random.Range(-outputSpread, outputSpread), 0,
                Random.Range(-outputSpread, outputSpread));
            found = TrySafeOutput(pad, out target);
        }
        if (!found && !preferShop) found = TryShopOutput(out target);
        if (!found) return false;
        ThrownBook thrown = book.GetComponent<ThrownBook>();
        if (thrown != null) { thrown.enabled = false; Destroy(thrown); }

        book.SetHeld(false); // Clear stale frozen support/contact state before recovery.
        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(
            target,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * book.NativeRotation);
        book.transform.localScale = book.OriginalScale;

        Rigidbody body = book.GetComponent<Rigidbody>();
        if (body != null)
        {
            // Once fizigi ac, SONRA hizi sifirla -- kinematic bir govdenin
            // hizi yazilamaz, Unity uyari basar.
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        lostSince.Remove(book);
        return true;
    }

    // ------------------------------------------------------------------
    // Etkilesim
    // ------------------------------------------------------------------

    /// <summary>Dukkandaki kitap dogus alaninda, mumkunse ustunde kule/raf olmayan bir nokta bulur.</summary>
    private bool TryShopOutput(out Vector3 target)
    {
        target = default;
        foreach (var spawner in FindObjectsByType<BookSpawner>(FindObjectsSortMode.None))
        {
            if (spawner.gameObject.scene != gameObject.scene || !spawner.ValidateSpawnAreas(out _)) continue;
            bool hasFallback = false;
            Vector3 fallback = default;
            for (int i = 0; i < 12; i++)
            {
                Vector3 sample;
                try { sample = spawner.SampleSpawnPosition(); }
                catch (System.InvalidOperationException) { break; }
                if (!TrySafeOutput(sample, out var candidate)) continue;
                if (!hasFallback) { fallback = candidate; hasFallback = true; }
                if (Physics.OverlapSphereNonAlloc(candidate, 0.3f, outputProbe, Physics.AllLayers, QueryTriggerInteraction.Ignore) == 0)
                { target = candidate; return true; }
            }
            if (hasFallback) { target = fallback; return true; }
        }
        return false;
    }

    private readonly Collider[] outputProbe = new Collider[1];

    private bool TrySafeOutput(Vector3 candidate, out Vector3 target)
    {
        target = candidate;
        if (requireGroundBelowOutput)
        {
            var hits = Physics.RaycastAll(candidate + Vector3.up * .5f, Vector3.down,
                groundSearchDistance + .5f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (var hit in hits)
            {
                if (hit.normal.y < .5f || hit.collider.GetComponentInParent<BookItem>() != null ||
                    hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                target = hit.point + Vector3.up * Mathf.Max(.1f, dropHeightAboveGround);
                found = true; break;
            }
            if (!found) return false;
        }
        return target.y >= lostBelowY && (!HasPlayBounds || PlayBounds.Contains(target));
    }

    public void ResetSession()
    {
        recoveryCounts.Clear();
        lostSince.Clear();
        lastRecallTime = -9999f;
        lastLostCheckTime = Time.time;
        playerCamera = null;
        RebuildPlayBounds();
    }

    private bool ForwardClientRequest()
    {
        if (ConnectionManager.Instance == null) return false;
        var manager = NetworkManager.Singleton;
        if (manager != null && manager.IsServer) return false;
        var player = NetworkPlayerSetup.LocalPlayer;
        if (player != null && player.playerCamera != null)
            player.RecallRpc(transform.position, player.playerCamera.transform.forward);
        return true;
    }

    private bool IsPlayerLooking()
    {
        if (ConnectionManager.Instance != null)
            playerCamera = NetworkPlayerSetup.LocalPlayer != null ? NetworkPlayerSetup.LocalPlayer.playerCamera : null;
        if (playerCamera == null || !playerCamera.enabled)
        {
            if (ConnectionManager.Instance != null) return false;
            playerCamera = Camera.main;
            if (playerCamera == null)
                return false;
        }

        Vector3 toMachine = transform.position - playerCamera.transform.position;

        if (toMachine.sqrMagnitude > useRange * useRange)
            return false;

        if (!requireLookingAt)
            return true;

        return Vector3.Dot(playerCamera.transform.forward, toMachine.normalized) >= lookThreshold;
    }

    void OnDrawGizmosSelected()
    {
        if (playArea != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            Bounds b = Application.isPlaying && HasPlayBounds ? PlayBounds : playArea.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, useRange);

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
        Gizmos.DrawSphere(OutputPosition, 0.06f);

    }
}