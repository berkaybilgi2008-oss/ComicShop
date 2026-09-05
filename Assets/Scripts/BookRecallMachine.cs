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

    [Header("Kayip Kitabi Dondurma")]
    [Tooltip("Oyun alaninin disina cikan kitap, belirtilen sure sonra DONDURULUR. " +
             "Sonsuza kadar dusup bosuna fizik hesabi yapmasini engeller. " +
             "E ile cagirinca tekrar normale doner.")]
    public bool freezeLostBooks = true;
    [Tooltip("Kitap kayip sayildiktan kac saniye sonra dondurulsun.")]
    [Min(0f)] public float freezeDelay = 5f;

    [Header("Kayip Kitap Kurtarma")]
    [Tooltip("Zeminin altina dusen kitaplari otomatik geri getir. " +
             "Asil bug sigortasi budur, bekleme suresine takilmaz.")]
    public bool autoRecoverLostBooks = true;
    [Tooltip("Bu yukseklikten asagi dusen kitap kayip sayilir.")]
    public float lostBelowY = -5f;
    [Tooltip("Kayip kontrolunun kac saniyede bir yapilacagi.")]
    [Min(0.5f)] public float lostCheckInterval = 3f;
    [Tooltip("Ayni kitap bu kadar kez kurtarildiktan sonra vazgecilir. Sonsuz " +
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

    private float lastRecallTime = -9999f;
    private float lastLostCheckTime;
    private Camera playerCamera;

    // Hangi kitap kac kez kurtarildi -- sonsuz donguyu kirmak icin.
    private readonly System.Collections.Generic.Dictionary<BookItem, int> recoveryCounts
        = new System.Collections.Generic.Dictionary<BookItem, int>();

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
        if (Time.time - lastLostCheckTime >= lostCheckInterval)
        {
            lastLostCheckTime = Time.time;
            ScanLostBooks();
        }

        if (Input.GetKeyDown(useKey) && IsPlayerLooking())
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

            if (Teleport(book))
                brought++;
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

        string area = playArea != null
            ? $"Oyun alani atanmis, sinirlari: {playArea.bounds.min} .. {playArea.bounds.max}"
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
        sb.AppendLine(playArea != null
            ? $"Oyun alani: {playArea.bounds.min} .. {playArea.bounds.max}"
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

        if (playArea != null && !playArea.bounds.Contains(position))
            return true;

        return false;
    }

    /// <summary>
    /// Kayip kitaplari tarar. Iki is yapar:
    ///   - Kayip kalan kitabi belirli sure sonra DONDURUR (bosuna fizik hesabi olmasin).
    ///   - Otomatik kurtarma acikssa geri getirir.
    /// </summary>
    private void ScanLostBooks()
    {
        BookItem[] books = FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        int recovered = 0;
        int frozen = 0;

        foreach (BookItem book in books)
        {
            if (book == null || book.IsHeld || book.currentSlot != null)
                continue;

            if (!IsLost(book))
            {
                lostSince.Remove(book);
                continue;
            }

            if (!lostSince.ContainsKey(book))
                lostSince[book] = Time.time;

            // Dondurma
            if (freezeLostBooks && Time.time - lostSince[book] >= freezeDelay)
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

            // Otomatik kurtarma (kapaliysa atlanir)
            if (!autoRecoverLostBooks)
                continue;

            recoveryCounts.TryGetValue(book, out int attempts);

            if (attempts >= maxRecoveryAttempts)
                continue;

            recoveryCounts[book] = attempts + 1;

            if (attempts + 1 == maxRecoveryAttempts)
            {
                Debug.LogError(
                    $"Isinlama makinesi: '{book.DisplayName}' {maxRecoveryAttempts} kez " +
                    $"kurtarildi ve her seferinde tekrar kayboldu. Bu kitap icin otomatik " +
                    $"kurtarma durduruldu.");
            }

            if (Teleport(book))
                recovered++;
        }

        if (recovered > 0)
            Debug.Log($"Isinlama makinesi: {recovered} kayip kitap otomatik kurtarildi.");

        if (frozen > 0)
            Debug.Log($"Isinlama makinesi: {frozen} kayip kitap donduruldu, cagrilmayi bekliyor.");
    }

    /// <summary>Kitabi cikisa tasir. Zemin bulunamazsa tasimaz ve false doner.</summary>
    private bool Teleport(BookItem book)
    {
        // Ucus bileseni varsa kaldir, kitap normal fizige donsun.
        ThrownBook thrown = book.GetComponent<ThrownBook>();
        if (thrown != null)
            Destroy(thrown);

        Vector3 offset = new Vector3(
            Random.Range(-outputSpread, outputSpread),
            0f,
            Random.Range(-outputSpread, outputSpread));

        Vector3 target = OutputPosition + offset;

        // Cikisin altinda saglam zemin var mi? Yoksa kitap dusup tekrar kaybolur
        // ve kurtarma dongusu baslar.
        if (requireGroundBelowOutput)
        {
            Vector3 rayStart = target + Vector3.up * 0.5f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                    groundSearchDistance + 0.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                target = hit.point + Vector3.up * dropHeightAboveGround;
            }
            else
            {
                Debug.LogError(
                    $"Isinlama makinesi ({name}): cikis noktasinin altinda zemin yok, " +
                    $"kitap birakilmadi. Pedi zeminin uzerine tasi.");
                return false;
            }
        }

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

    private bool IsPlayerLooking()
    {
        if (playerCamera == null)
        {
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
            Bounds b = playArea.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, useRange);

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
        Gizmos.DrawSphere(OutputPosition, 0.06f);

    }
}