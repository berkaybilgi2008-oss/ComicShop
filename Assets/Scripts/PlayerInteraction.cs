using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public Camera playerCamera;
    public Transform rightHandPoint;

    [Header("Tasima Ayarlari")]
    [Min(1)] public int maxHeldBooks = 10;
    public float stackSpacing = 3f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;

    [Header("Elde Kitap Animasyonu")]
    [Min(0.01f)] public float bookMoveDuration = 0.22f;
    public AnimationCurve bookMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Birakma Ayarlari")]
    [Tooltip("Kitabin normal birakildiginda ileri dogru kazanacagi hiz.")]
    [Min(0f)] public float dropForwardForce = 2.5f;
    [Tooltip("Kitabin normal birakildiginda yukari dogru kazanacagi hiz.")]
    [Min(0f)] public float dropUpwardForce = 0.75f;
    [Min(0f)] public float playerCollisionRestoreDelay = 0.1f;

    [Header("Sarjli Atis -- Q")]
    [Tooltip("Kitabi shuriken gibi firlatma yetenegi. Oyuncu kazanana kadar kapali tut.")]
    public bool throwAbilityUnlocked = true;
    [Tooltip("Basili tutulunca sarj eder, birakilinca firlatir.")]
    public KeyCode throwKey = KeyCode.Q;
    [Tooltip("Barin tamamen dolmasi icin gereken sure.")]
    [Min(0.05f)] public float chargeFillDuration = 1.4f;
    [Tooltip("Kitabin bas ustu pozuna gecis suresi.")]
    [Min(0.01f)] public float chargeEnterDuration = 0.18f;

    public enum ThrowHand
    {
        Left,
        Right
    }

    [Header("Bas Ustu Poz")]
    [Tooltip("Kitabin hangi elde tutulacagi. Yon bu ayardan gelir; asagidaki " +
             "offset'in X isareti dikkate ALINMAZ, sadece buyuklugu kullanilir.")]
    public ThrowHand throwHand = ThrowHand.Left;
    [Tooltip("Omuz pivotunun kameraya gore konumu: X = yan mesafe (isareti Throw Hand " +
             "belirler), Y = yukari/asagi, Z = ONE mesafe. Z'yi kucultursen kitap " +
             "buyur ve yaklasir, buyutursen kucululur ve uzaklasir.")]
    public Vector3 throwPoseOffset = new Vector3(0.18f, -0.05f, 0.62f);
    [Tooltip("Pivottan kitaba mesafe -- kolun uzunlugu gibi dusun.")]
    [Min(0.05f)] public float throwArmLength = 0.3f;
    [Tooltip("Sarj sirasinda kitabin olcegi. 1 = kitabin gercek boyu. " +
             "Elde tasinirken kucultuldugu icin buyuk gorunsun diye ayri tutuldu.")]
    [Min(0.2f)] public float chargeScaleMultiplier = 1f;
    [Tooltip("Sarj basindaki aci. Eksi deger = kafanin ARKASINDA.")]
    public float windupStartAngle = -20f;
    [Tooltip("Bar dolunca varilan geriye yuklenme acisi.")]
    public float windupFullAngle = -55f;
    [Tooltip("Savurmanin bittigi aci. Kitap burada elden cikar.")]
    public float releaseAngle = 55f;
    [Tooltip("Savurma yayinin suresi. Kisa tut -- keskin olmali.")]
    [Min(0.02f)] public float throwArcDuration = 0.13f;

    [Header("Zorlanma Titremesi")]
    [Tooltip("Bar doldukca elin titremesi (metre). Sadece konumda, acida degil.")]
    [Min(0f)] public float chargeShakeAmount = 0.018f;
    [Min(0.1f)] public float chargeShakeSpeed = 24f;

    [Header("Atis Gucu")]
    [Min(0f)] public float minThrowSpeed = 6f;
    [Min(0f)] public float maxThrowSpeed = 22f;
    [Tooltip("Kitap elden cikarken hiza uygulanan ek carpan. Yayin son anindaki " +
             "ivmeyi hissettirir -- 1 = ek yok.")]
    [Min(1f)] public float releaseSnap = 1.4f;
    [Tooltip("Donme hizi, radyan/sn.")]
    [Min(0f)] public float minThrowSpin = 10f;
    [Min(0f)] public float maxThrowSpin = 34f;

    [Header("Etkilesim")]
    public float interactRange = 3f;
    public LayerMask interactMask = ~0;

    [Header("Yerlestirme Guvenligi / Debug")]
    [Tooltip("Rafa yerlestirilen kitabin oyuncudan olabilecegi maksimum mesafe. " +
             "Bunun uzerindeki hedefler iptal edilir (kitabin haritanin ucuna ucmasini engeller).")]
    [Min(0.5f)] public float maxPlacementDistance = 6f;

    [Tooltip("Acikken, bir raf gozu kitabi neden kabul etmedigini Console'a yazar.")]
    public bool debugPlacement = false;

    [Header("Tusu")]
    public KeyCode pickupKey = KeyCode.Mouse0;
    public KeyCode dropKey = KeyCode.Mouse1;

    private readonly List<BookItem> heldBooks = new List<BookItem>();
    public IReadOnlyList<BookItem> HeldBooksList => heldBooks;
    public int MaxHeldBooks => maxHeldBooks;
    public int ActiveHeldIndex => activeHeldIndex;
    public BookItem ActiveHeldBook => heldBooks.Count == 0 ? null : heldBooks[Mathf.Clamp(activeHeldIndex, 0, heldBooks.Count - 1)];

    private int activeHeldIndex = -1;
    private BookItem lookedBook;
    private ShelfSlot lookedSlot;
    private bool isBookAnimating;

    private float chargeStartTime;
    private Crosshair crosshair;
    private bool isChargingThrow;
    public bool IsThrowPoseActive => isChargingThrow || isThrowing;
    private bool isThrowing;
    private float chargeAmount;
    public float ThrowCharge => chargeAmount;
    public float ThrowReleaseProgress { get; private set; }
    public Vector3 CurrentThrowShake => isChargingThrow
        ? ChargeShake() * (chargeShakeAmount * chargeAmount) : Vector3.zero;
    private BookItem chargingBook;
    private int lastWheelDirection;
    private ToastBookCarry throwRig;
    private Vector3 enterStartPosition;
    private Quaternion enterStartRotation;
    private Vector3 enterStartScale = Vector3.one;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        interactMask |= 1 << 0;

        // Multiplayer: her oyuncu KENDI nisangahini kullanmali. Sahne genelinde
        // arayinca baska bir oyuncunun nisangahini bulup ona yaziyordu.
        crosshair = GetComponent<Crosshair>();
        if (crosshair == null)
            crosshair = gameObject.AddComponent<Crosshair>();

        // Optional character package: bind when present, including spawned players.
        if (GetComponent<CharacterBookCarryBridge>() == null)
            gameObject.AddComponent<CharacterBookCarryBridge>();
    }

    void Update()
    {
        UpdateCollisionRestoration();
        if (heldBooks.RemoveAll(book => book == null) > 0)
        {
            CancelHandAnimations();
            activeHeldIndex = heldBooks.Count == 0 ? -1 : Mathf.Clamp(activeHeldIndex, 0, heldBooks.Count - 1);
            RepositionHeldBooksImmediate();
        }
        if (TryGetComponent<PlayerKnockdown>(out var knocked) && knocked.IsDown) return;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (isChargingThrow || isThrowing || isBookAnimating)
            {
                CancelHandAnimations();
                RepositionHeldBooksImmediate();
            }
            return;
        }
        HandleLookDetection();
        HandleThrowInput();

        if (crosshair != null)
            crosshair.chargeAmount = isChargingThrow ? chargeAmount : 0f;

        if (isChargingThrow)
        {
            UpdateCharge();
            return;
        }

        if (isThrowing || isBookAnimating)
            return;

        // One inventory action per input frame. Pickup/placement coroutines
        // start immediately; a second button must not release that same book.
        if (Input.GetKeyDown(pickupKey))
        {
            HandlePickupPress();
            return;
        }
        if (Input.GetKeyDown(dropKey) && heldBooks.Count > 0)
        {
            HandleDropOrPlacePress();
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && heldBooks.Count > 1)
        {
            lastWheelDirection = wheel < 0f ? 1 : -1;
            ChangeActiveHeldBook(lastWheelDirection);
        }
    }

    private readonly RaycastHit[] lookHits = new RaycastHit[128];
    private static readonly IComparer<RaycastHit> LookHitOrder =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    void HandleLookDetection()
    {
        if (playerCamera == null)
            return;

        lookedBook = null;
        lookedSlot = null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = lookHits;
        int hitCount = Physics.RaycastNonAlloc(ray, hits, interactRange, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (hitCount == hits.Length)
        {
            // NonAlloc hits are unordered; a full buffer may omit the nearest wall.
            hits = Physics.RaycastAll(ray, interactRange, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            hitCount = hits.Length;
        }
        System.Array.Sort(hits, 0, hitCount, LookHitOrder);

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        ShelfSlot nearestSlot = null;
        BookItem nearestBook = null;

        // Raf collider'i, kitap collider'indan once gelebiliyor. Bu yuzden ikisini de
        // ayri ayri topluyoruz: en yakin serbest kitap + en yakin raf gozu.
        // (Eskiden kitap bulununca lookedSlot null'lanip rafa koyma tamamen bloklaniyordu.)
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = hits[hitIndex];
            if (hit.collider.transform.IsChildOf(transform)) continue;
            var blockingBook = hit.collider.GetComponentInParent<BookItem>();
            if (blockingBook != null && blockingBook.IsHeld) continue;
            var blockingSlot = hit.collider.GetComponentInParent<ShelfSlot>();
            // An interaction mask must never make a wall transparent to pickup.
            if (blockingBook == null && blockingSlot == null) break;
            if ((interactMask.value & (1 << hit.collider.gameObject.layer)) == 0) break;
            if (nearestBook == null)
            {
                BookItem book = hit.collider.GetComponentInParent<BookItem>();
                if (book != null && canPickMore && !book.IsHeld && book.currentSlot == null)
                    nearestBook = book;
            }

            if (nearestSlot == null)
            {
                ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();

                if (slot != null)
                    nearestSlot = slot;
            }

            if (nearestBook != null && nearestSlot != null)
                break;
        }

        lookedBook = nearestBook;
        lookedSlot = nearestSlot;

        if (lookedBook != null)
            lookedBook.SetHighlight(true);
    }

    public void ShowFeedback(string message)
    {
        placementFeedback = message;
        placementFeedbackUntil = Time.unscaledTime + 2.5f;
        ShopAudio.Play(ShopCue.Reject, Vector3.zero, false);
    }
    private string placementFeedback;
    private float placementFeedbackUntil;
    public string InteractionHint
    {
        get
        {
            if (Time.unscaledTime < placementFeedbackUntil) return placementFeedback;
            if (lookedSlot != null && ActiveHeldBook != null)
            {
                if (lookedSlot.PublisherID < 0) return "Raf logosu eksik veya çakışıyor";
                if (!lookedSlot.IsAvailable) return "Raf gözü dolu";
                if (ActiveHeldBook.brandID != lookedSlot.PublisherID) return "Yanlış yayıncı";
                if (lookedSlot.IsClaimed && lookedSlot.OwnerBookID != ActiveHeldBook.bookID)
                    return "Bu raf gözü başka bir kitap grubuna ayrılmış";
                return dropKey + ": Rafa yerleştir";
            }
            if (lookedBook != null || (lookedSlot != null && lookedSlot.FilledCount > 0))
                return heldBooks.Count >= maxHeldBooks ? "Ellerin dolu" : pickupKey + ": Kitabı al";
            return string.Empty;
        }
    }

    void HandlePickupPress()
    {
        if (lookedBook != null && heldBooks.Count < maxHeldBooks)
        {
            PickUp(lookedBook);
            return;
        }

        if (lookedSlot != null && lookedSlot.FilledCount > 0 && heldBooks.Count < maxHeldBooks)
            TakeFromShelf();
    }

    /// <summary>
    /// Q basili tutulur -> kitap sol ele gecer ve bar dolmaya baslar.
    /// Q birakilir     -> bar ne kadar dolduysa o gucle firlar.
    /// </summary>
    // ==================================================================
    // SHURIKEN ATISI
    //
    // Q basili   -> kitap bas ustune kalkar, kapaklari YANA bakar (ince kenar
    //               nisangaha doner), bar dolar, el zorlanmadan titrer.
    // Q birakilir-> el bas arkasindan one tek bir yay cizer; yayin sonunda kitap
    //               NISANGAH dogrultusunda, kendi duzleminde donerek cikar.
    // ==================================================================

    void HandleThrowInput()
    {
        if (!throwAbilityUnlocked)
            return;

        if (Input.GetKeyDown(throwKey)
            && !isChargingThrow && !isThrowing
            && !isBookAnimating && heldBooks.Count > 0)
        {
            BeginCharge();
            return;
        }

        if (isChargingThrow && Input.GetKeyUp(throwKey))
            StartCoroutine(ThrowArc());
    }

    void BeginCharge()
    {
        BookItem book = ActiveHeldBook;
        if (book == null || playerCamera == null)
            return;

        chargingBook = book;
        isChargingThrow = true;
        chargeAmount = 0f;
        ThrowReleaseProgress = 0f;
        chargeStartTime = Time.time;

        enterStartPosition = book.transform.position;
        enterStartRotation = book.transform.rotation;
        enterStartScale = book.transform.lossyScale;
    }

    void UpdateCharge()
    {
        if (chargingBook == null)
        {
            isChargingThrow = false;
            return;
        }

        float elapsed = Time.time - chargeStartTime;
        chargeAmount = Mathf.Clamp01(elapsed / chargeFillDuration);

        GetThrowPose(chargingBook, ThrowSwingAngle(chargeAmount, 0f), chargeAmount,
            out Vector3 position, out Quaternion rotation);

        float blend = EvaluateBookMoveCurve(Mathf.Clamp01(elapsed / chargeEnterDuration));

        // Kitap tasima pozundan atis pozuna suzulurken el de onunla beraber gelir;
        // yoksa el hemen tepeye zipliyor, kitap arkadan yetismeye calisiyordu.
        Vector3 entering = Vector3.LerpUnclamped(enterStartPosition, position, blend);
        if (throwRig == null) throwRig = GetComponentInChildren<ToastBookCarry>();
        if (throwRig != null) throwRig.OffsetThrowWrist(entering - position);

        ApplyThrowPose(chargingBook, entering,
            Quaternion.SlerpUnclamped(enterStartRotation, rotation, blend),
            Vector3.LerpUnclamped(enterStartScale, chargingBook.OriginalScale * chargeScaleMultiplier, blend));
    }

    private void ApplyThrowPose(BookItem book, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (throwRig == null) throwRig = GetComponentInChildren<ToastBookCarry>();
        // Obstacle protection has final authority over the presentation pose.
        // El kitaptan kopmasin: duvar kaydirmasi kadar bilek de kayar.
        Vector3 constrained = ConstrainBookToRoom(book, position);
        if (throwRig != null) throwRig.OffsetThrowWrist(constrained - position);
        position = constrained;
        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(position, rotation);
        book.transform.localScale = scale;
    }

    IEnumerator ThrowArc()
    {
        BookItem book = chargingBook;
        float finalCharge = chargeAmount;

        isChargingThrow = false;
        chargingBook = null;

        if (book == null || !heldBooks.Contains(book) || playerCamera == null)
            yield break;

        isThrowing = true;

        float elapsed = 0f;

        // Bas arkasindan one dogru tek temiz yay; sona dogru hizlanir (bilek sokumu).
        while (elapsed < throwArcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwArcDuration);
            // Kubik egri: basta yuklenme hissi, sonda kirbac gibi bilek sokumu.
            ThrowReleaseProgress = t * t * t;
            float angle = ThrowSwingAngle(finalCharge, ThrowReleaseProgress);

            GetThrowPose(book, angle, 0f, out Vector3 position, out Quaternion rotation);
            ApplyThrowPose(book, position, rotation, book.OriginalScale * chargeScaleMultiplier);

            yield return null;
        }

        isThrowing = false;

        int index = heldBooks.IndexOf(book);
        if (index < 0)
            yield break;

        // The held stack is displayed bottom -> top by GetDisplayOrder(), with the
        // active book always on top. When the top book is thrown, the book that was
        // directly underneath it must become active. This keeps the visible stack
        // order unchanged instead of jumping to an unrelated item.
        List<int> displayOrderBeforeThrow = GetDisplayOrder();
        int fallbackHeldIndex = -1;
        int displayIndex = displayOrderBeforeThrow.IndexOf(index);
        if (displayIndex > 0)
        {
            int nextVisibleHeldIndex = displayOrderBeforeThrow[displayIndex - 1];
            if (nextVisibleHeldIndex >= 0 && nextVisibleHeldIndex < heldBooks.Count)
                fallbackHeldIndex = nextVisibleHeldIndex;
        }

        heldBooks.RemoveAt(index);

        if (heldBooks.Count == 0)
        {
            activeHeldIndex = -1;
        }
        else
        {
            // Convert the pre-removal list index to the new list index.
            BookItem nextBook = fallbackHeldIndex >= 0
                ? GetBookFromPreRemovalIndex(displayOrderBeforeThrow, displayIndex - 1)
                : null;
            int newIndex = nextBook != null ? heldBooks.IndexOf(nextBook) : -1;
            activeHeldIndex = newIndex >= 0
                ? newIndex
                : Mathf.Clamp(index, 0, heldBooks.Count - 1);
        }

        Transform cam = playerCamera.transform;

        ThrowBook(
            book,
            cam.forward * (Mathf.Lerp(minThrowSpeed, maxThrowSpeed, finalCharge) * releaseSnap),
            cam.right,
            Mathf.Lerp(minThrowSpin, maxThrowSpin, finalCharge),
            true);
    }

    /// <summary>
    /// Yay uzerindeki bir acida kitabin konumu ve rotasyonu.
    /// aci 0 = tam tepe, eksi = kafanin arkasi, arti = one savrulmus.
    /// Kapak normali her zaman kameranin sagi: kapaklar yana bakar, ince kenar
    /// nisangah dogrultusuna bakar, donus kitabin kendi duzleminde olur.
    /// </summary>
    void GetThrowPose(BookItem book, float angle, float shake,
                      out Vector3 position, out Quaternion rotation)
    {
        if (throwRig == null) throwRig = GetComponentInChildren<ToastBookCarry>();
        // Asil yol: pozu kol kurar, kitap elin kosesine oturur. Eski kamera yayi
        // sadece kol kemikleri bulunamazsa (ornegin modelsiz sahne) devreye girer.
        if (throwRig != null && throwRig.GetThrowPose(book, angle, throwHand == ThrowHand.Left,
            book.OriginalScale * chargeScaleMultiplier, out position, out rotation))
        {
            if (shake > 0f && chargeShakeAmount > 0f)
            {
                Vector3 tremble = ChargeShake() * (chargeShakeAmount * shake);
                position += tremble;
                throwRig.OffsetThrowWrist(tremble);
            }
            return;
        }
        Transform cam = playerCamera.transform;

        Vector3 coverNormal = cam.right;

        // Yan yon ENUM'dan gelir. Boylece sahnede kayitli eski bir X degeri
        // kitabi yanlis ele goturemez.
        float side = throwHand == ThrowHand.Left ? -1f : 1f;

        Vector3 pivot = cam.position
                        + cam.right * (Mathf.Abs(throwPoseOffset.x) * side)
                        + cam.up * throwPoseOffset.y
                        + cam.forward * throwPoseOffset.z;

        Vector3 armDirection = Quaternion.AngleAxis(angle, coverNormal) * cam.up;
        position = pivot + armDirection * throwArmLength;

        if (shake > 0f && chargeShakeAmount > 0f)
            position += ChargeShake() * (chargeShakeAmount * shake);

        // Kitabin BOYU kolun dogrultusunda -- yay boyunca kolla beraber doner.
        rotation = book != null
            ? book.GetAlignedRotation(coverNormal, armDirection)
            : Quaternion.identity;
    }

    /// <summary>Bar dolarken elin zorlanma titremesi.</summary>
    Vector3 ChargeShake()
    {
        float n = Time.time * chargeShakeSpeed;
        return new Vector3(
            Mathf.PerlinNoise(n, 0.13f) - 0.5f,
            Mathf.PerlinNoise(0.47f, n) - 0.5f,
            Mathf.PerlinNoise(n, n) - 0.5f) * 2f;
    }

    /// <summary>
    /// Yay acisi. Kol kemikleri varsa acilar ToastBookCarry'den gelir; boylece
    /// sahnede kayitli eski (cok geriye kacan) degerler pozu bozamaz.
    /// </summary>
    float ThrowSwingAngle(float charge, float release)
    {
        if (throwRig == null) throwRig = GetComponentInChildren<ToastBookCarry>();
        if (throwRig != null && throwRig.HasThrowArm(throwHand == ThrowHand.Left))
            return throwRig.ThrowSwingAngle(charge, release);
        float windup = Mathf.Lerp(windupStartAngle, windupFullAngle, Mathf.Clamp01(charge));
        return Mathf.Lerp(windup, releaseAngle, Mathf.Clamp01(release));
    }

    /// <summary>Yatay duzlemdeki bakis yonu -- normal birakma icin.</summary>
    Vector3 GetFlatForward()
    {
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        return forward.normalized;
    }

    void HandleDropOrPlacePress()
    {
        if (heldBooks.Count == 0)
            return;

        // Bir raf gozune bakiyorsak sadece iki sonuc olabilir:
        // ya kitap oraya yerlesir, ya da HICBIR SEY olmaz.
        // Reddedilen bir yerlestirme kitabi yere atmamali -- yanlis gozu
        // denemenin cezasi kitabi yerden toplamak olmasin.
        if (lookedSlot != null)
        {
            TryPlaceActiveBook();
            return;
        }

        DropActiveBook();
    }

    void TakeFromShelf()
    {
        if (lookedSlot == null || lookedSlot.FilledCount <= 0 || heldBooks.Count >= maxHeldBooks)
            return;

        BookItem candidate = lookedSlot.PeekLastBook();
        NetworkBook networkBook = candidate != null ? candidate.GetComponent<NetworkBook>() : null;
        if (networkBook != null && networkBook.IsSpawned)
        {
            networkBook.PickUpRpc();
            return;
        }
        BookItem book = lookedSlot.TakeLastBook();
        if (book == null)
            return;

        book.SetHighlight(false);
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        ShopAudio.Play(ShopCue.Pickup, book.transform.position);
        heldBooks.Add(book);
        activeHeldIndex = heldBooks.Count - 1;
        lookedSlot = null;

        StartCoroutine(MoveBookIntoHand(book));
    }

    void PickUp(BookItem book)
    {
        if (book == null || heldBooks.Count >= maxHeldBooks)
            return;

        NetworkBook networkBook = book.GetComponent<NetworkBook>();
        if (networkBook != null && networkBook.IsSpawned)
        {
            networkBook.PickUpRpc();
            return;
        }
        if (book.IsHeld) return;
        if (book.currentSlot != null)
            book.currentSlot.RemoveBook(book);

        book.SetHighlight(false);
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        ShopAudio.Play(ShopCue.Pickup, book.transform.position);
        heldBooks.Add(book);
        activeHeldIndex = heldBooks.Count - 1;
        lookedBook = null;

        StartCoroutine(MoveBookIntoHand(book));
    }

    public void AcceptNetworkBook(BookItem book)
    {
        if (book == null || heldBooks.Contains(book)) return;
        CancelHandAnimations();
        RepositionHeldBooksImmediate();
        heldBooks.Add(book);
        activeHeldIndex = heldBooks.Count - 1;
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        StartCoroutine(MoveBookIntoHand(book));
        lookedBook = null;
    }

    public void ForgetNetworkBook(BookItem book)
    {
        if (book == null) return;
        bool removed = heldBooks.Remove(book);
        IgnorePlayerCollision(book, false);
        if (!removed) return;
        CancelHandAnimations();
        activeHeldIndex = heldBooks.Count == 0 ? -1 : Mathf.Clamp(activeHeldIndex, 0, heldBooks.Count - 1);
        RepositionHeldBooksImmediate();
    }

    private void CancelHandAnimations()
    {
        StopAllCoroutines();
        isBookAnimating = false;
        isChargingThrow = false;
        isThrowing = false;
        chargingBook = null;
        chargeAmount = 0f;
        if (crosshair != null) crosshair.chargeAmount = 0f;
    }

    public void ResetInteraction()
    {
        CancelHandAnimations();
        foreach (var book in heldBooks)
        {
            if (book == null) continue;
            book.transform.SetParent(null, true);
            IgnorePlayerCollision(book, false);
        }
        RestoreAllPlayerCollisions();
        heldBooks.Clear();
        activeHeldIndex = -1;
        lookedBook = null;
        lookedSlot = null;
    }

    public void CancelForKnockdown()
    {
        ReleaseAllHeldBooksForKnockdown();
    }

    public void ReleaseAllHeldBooksForKnockdown()
    {
        CancelHandAnimations();

        if (heldBooks.Count == 0)
        {
            activeHeldIndex = -1;
            return;
        }

        List<BookItem> books = new List<BookItem>(heldBooks);
        heldBooks.Clear();
        activeHeldIndex = -1;

        foreach (BookItem book in books)
        {
            if (book == null) continue;

            book.transform.SetParent(null, true);
            IgnorePlayerCollision(book, false);

            // NetworkBook is released authoritatively by NetworkPlayerSetup when
            // the knockdown begins. Do not send ReleaseRpc here: the server rejects
            // releases from an already-down player.
            if (book.GetComponent<NetworkBook>() != null &&
                book.GetComponent<NetworkBook>().IsSpawned)
                continue;

            book.SetHeld(false);
            book.transform.localScale = book.OriginalScale;

            Rigidbody rb = book.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = Vector3.down * 0.6f;
                rb.angularVelocity = Random.insideUnitSphere * 1.5f;
                rb.WakeUp();
            }
        }

        RestoreAllPlayerCollisions();
    }

    Quaternion GetHeldLocalRotation(BookItem book)
    {
        // Preserve the authored hand anchor and each prefab's calibrated pose.
        return book != null ? book.NativeRotation : Quaternion.identity;
    }

    IEnumerator MoveBookIntoHand(BookItem book)
    {
        if (book == null || rightHandPoint == null)
            yield break;

        isBookAnimating = true;

        Vector3 startPosition = book.transform.position;
        Quaternion startRotation = book.transform.rotation;
        Vector3 startScale = book.transform.lossyScale;

        book.transform.SetParent(rightHandPoint, true);
        Vector3 targetLocalPosition = GetHeldLocalPosition(GetDisplayIndexForBook(book));
        Quaternion targetLocalRotation = GetHeldLocalRotation(book);
        Vector3 targetLocalScale = book.OriginalScale * heldScaleMultiplier;

        Vector3 currentLocalPosition = book.transform.localPosition;
        Quaternion currentLocalRotation = book.transform.localRotation;
        Vector3 currentLocalScale = book.transform.localScale;

        float duration = Mathf.Max(0.01f, bookMoveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateBookMoveCurve(elapsed / duration);
            book.transform.localPosition = Vector3.LerpUnclamped(currentLocalPosition, targetLocalPosition, t);
            book.transform.localRotation = Quaternion.SlerpUnclamped(currentLocalRotation, targetLocalRotation, t);
            book.transform.localScale = Vector3.LerpUnclamped(currentLocalScale, targetLocalScale, t);
            yield return null;
        }

        book.transform.localPosition = targetLocalPosition;
        book.transform.localRotation = targetLocalRotation;
        book.transform.localScale = targetLocalScale;
        isBookAnimating = false;
    }

    float EvaluateBookMoveCurve(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return bookMoveCurve != null ? bookMoveCurve.Evaluate(normalizedTime) : normalizedTime;
    }

    BookItem GetBookFromPreRemovalIndex(List<int> displayOrderBeforeThrow, int displayIndex)
    {
        if (displayOrderBeforeThrow == null || displayIndex < 0 || displayIndex >= displayOrderBeforeThrow.Count)
            return null;

        int heldIndex = displayOrderBeforeThrow[displayIndex];
        if (heldIndex < 0 || heldIndex >= heldBooks.Count)
            return null;

        return heldBooks[heldIndex];
    }

    int GetNextIndexAfterThrown(int removedIndex)
    {
        if (heldBooks.Count == 0)
            return -1;

        // Scroll up/down is a direction, not just a selection. After throwing the
        // active book, continue in that same direction through the remaining stack.
        if (lastWheelDirection > 0)
            return removedIndex >= heldBooks.Count ? 0 : removedIndex;

        if (lastWheelDirection < 0)
        {
            int next = removedIndex - 1;
            if (next < 0) next = heldBooks.Count - 1;
            return next;
        }

        return Mathf.Clamp(removedIndex, 0, heldBooks.Count - 1);
    }

    void ChangeActiveHeldBook(int direction)
    {
        if (heldBooks.Count < 2)
            return;

        if (activeHeldIndex < 0 || activeHeldIndex >= heldBooks.Count)
            activeHeldIndex = heldBooks.Count - 1;

        activeHeldIndex += direction;
        if (activeHeldIndex < 0)
            activeHeldIndex = heldBooks.Count - 1;
        else if (activeHeldIndex >= heldBooks.Count)
            activeHeldIndex = 0;

        StartCoroutine(AnimateHeldStack());
    }

    IEnumerator AnimateHeldStack()
    {
        if (rightHandPoint == null)
            yield break;

        isBookAnimating = true;
        int count = heldBooks.Count;
        Vector3[] startPositions = new Vector3[count];
        Quaternion[] startRotations = new Quaternion[count];
        Vector3[] startScales = new Vector3[count];
        Vector3[] targetPositions = new Vector3[count];
        Quaternion[] targetRotations = new Quaternion[count];
        Vector3[] targetScales = new Vector3[count];

        List<int> displayOrder = GetDisplayOrder();

        for (int i = 0; i < count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            book.transform.SetParent(rightHandPoint, true);
            startPositions[i] = book.transform.localPosition;
            startRotations[i] = book.transform.localRotation;
            startScales[i] = book.transform.localScale;

            int displayIndex = displayOrder.IndexOf(i);
            targetPositions[i] = GetHeldLocalPosition(displayIndex);
            targetRotations[i] = GetHeldLocalRotation(book);
            targetScales[i] = book.OriginalScale * heldScaleMultiplier;
        }

        float duration = Mathf.Max(0.01f, bookMoveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateBookMoveCurve(elapsed / duration);

            for (int i = 0; i < count; i++)
            {
                BookItem book = heldBooks[i];
                if (book == null)
                    continue;

                book.transform.localPosition = Vector3.LerpUnclamped(startPositions[i], targetPositions[i], t);
                book.transform.localRotation = Quaternion.SlerpUnclamped(startRotations[i], targetRotations[i], t);
                book.transform.localScale = Vector3.LerpUnclamped(startScales[i], targetScales[i], t);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            book.transform.localPosition = targetPositions[i];
            book.transform.localRotation = targetRotations[i];
            book.transform.localScale = targetScales[i];
        }

        isBookAnimating = false;
    }

    List<int> GetDisplayOrder()
    {
        List<int> order = new List<int>(heldBooks.Count);

        for (int i = 0; i < heldBooks.Count; i++)
        {
            if (i != activeHeldIndex)
                order.Add(i);
        }

        if (activeHeldIndex >= 0 && activeHeldIndex < heldBooks.Count)
            order.Add(activeHeldIndex);

        return order;
    }

    int GetDisplayIndexForBook(BookItem book)
    {
        int heldIndex = heldBooks.IndexOf(book);
        if (heldIndex < 0)
            return Mathf.Max(0, heldBooks.Count - 1);

        return GetDisplayOrder().IndexOf(heldIndex);
    }

    Vector3 GetHeldLocalPosition(int index)
    {
        return new Vector3(0f, index * stackSpacing, 0f);
    }

    /// <summary>
    /// Aktif kitabi bakilan raf gozune koymayi dener.
    /// Basarili olursa true doner; false donerse cagiran taraf normal birakma yapar.
    /// </summary>
    bool TryPlaceActiveBook()
    {
        BookItem book = ActiveHeldBook;
        if (lookedSlot == null || book == null)
            return false;

        // Explain a local rejection without changing inventory or dropping the book.
        if (!lookedSlot.Matches(book))
        {
            placementFeedback = lookedSlot.PublisherID < 0 ? "Raf logosu eksik veya çakışıyor" :
                !lookedSlot.IsAvailable ? "Raf gözü dolu" :
                book.brandID != lookedSlot.PublisherID ? "Yanlış yayıncı" :
                "Bu kitap grubu farklı bir raf gözüne ayrılmış";
            ShowFeedback(placementFeedback);
            return false;
        }
        NetworkBook networkBook = book.GetComponent<NetworkBook>();
        if (networkBook != null && networkBook.IsSpawned)
        {
            networkBook.PlaceRpc(lookedSlot.NetworkKey);
            return true;
        }

        if (!lookedSlot.TryGetNextPlacementPose(book, out Vector3 targetPosition, out _))
        {
            Debug.LogWarning($"[Yerlestirme] '{lookedSlot.name}' icin gecerli bir kitap konumu " +
                             $"hesaplanamadi. Kitap ele geri birakildi.");
            return false;
        }

        // GUVENLIK: hedef konum oyuncudan cok uzaksa kitabi ucurma.
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget > maxPlacementDistance)
        {
            Debug.LogError($"[Yerlestirme] '{lookedSlot.name}' icin hesaplanan kitap konumu " +
                           $"oyuncudan {distanceToTarget:0.00} m uzakta (limit {maxPlacementDistance} m). " +
                           $"Yerlestirme iptal edildi -- bu slot'un kitap noktalari yanlis yerde.");
            return false;
        }

        StartCoroutine(PlaceActiveBookAnimated(book, lookedSlot));
        return true;
    }

    IEnumerator PlaceActiveBookAnimated(BookItem book, ShelfSlot slot)
    {
        if (book == null || slot == null || rightHandPoint == null)
            yield break;

        isBookAnimating = true;

        if (!slot.TryGetNextPlacementPose(book, out Vector3 targetPosition, out Quaternion slotRotation))
        {
            isBookAnimating = false;
            yield break;
        }

        Vector3 startPosition = book.transform.position;
        Quaternion startRotation = book.transform.rotation;
        Vector3 startScale = book.transform.lossyScale;
        Quaternion targetRotation = slotRotation * book.NativeRotation;
        Vector3 targetScale = book.OriginalScale;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(startPosition, startRotation);
        book.transform.localScale = startScale;

        float duration = Mathf.Max(0.01f, bookMoveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateBookMoveCurve(elapsed / duration);
            book.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            book.transform.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t);
            book.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            yield return null;
        }

        book.transform.SetPositionAndRotation(targetPosition, targetRotation);
        book.transform.localScale = targetScale;

        if (slot.PlaceBook(book))
        {
            ShopAudio.Play(ShopCue.Place, book.transform.position);
            IgnorePlayerCollision(book, false);
            heldBooks.Remove(book);

            if (heldBooks.Count == 0)
                activeHeldIndex = -1;
            else
                activeHeldIndex = Mathf.Clamp(heldBooks.Count - 1, 0, heldBooks.Count - 1);
        }
        else
        {
            // Yerlestirme son anda basarisiz oldu. Kitabi havada birakma, ele geri al.
            Debug.LogWarning($"[Yerlestirme] '{slot.name}' son adimda kitabi kabul etmedi, " +
                             $"kitap ele geri alindi.");
            book.SetHeld(true);
        }

        RepositionHeldBooksImmediate();
        lookedSlot = null;
        isBookAnimating = false;
    }

    void RepositionHeldBooksImmediate()
    {
        if (rightHandPoint == null)
            return;

        List<int> displayOrder = GetDisplayOrder();

        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            int displayIndex = displayOrder.IndexOf(i);
            book.transform.SetParent(rightHandPoint, false);
            book.transform.localPosition = GetHeldLocalPosition(displayIndex);
            book.transform.localRotation = GetHeldLocalRotation(book);
            book.transform.localScale = book.OriginalScale * heldScaleMultiplier;
        }
    }

    void DropActiveBook()
    {
        if (heldBooks.Count == 0)
            return;

        if (activeHeldIndex < 0 || activeHeldIndex >= heldBooks.Count)
            activeHeldIndex = heldBooks.Count - 1;

        int removedIndex = activeHeldIndex;
        BookItem book = heldBooks[removedIndex];
        heldBooks.RemoveAt(removedIndex);

        if (heldBooks.Count == 0)
            activeHeldIndex = -1;
        else
            activeHeldIndex = Mathf.Clamp(removedIndex, 0, heldBooks.Count - 1);

        ThrowBook(
            book,
            GetFlatForward() * dropForwardForce + Vector3.up * dropUpwardForce,
            Vector3.zero,
            0f,
            false);
    }

    private readonly RaycastHit[] roomHits = new RaycastHit[128];
    private readonly Collider[] roomOverlaps = new Collider[128];

    private static bool IsRoomObstacle(Collider collider)
    {
        return collider != null && !collider.isTrigger &&
            collider.GetComponentInParent<BookItem>() == null &&
            collider.GetComponentInParent<PlayerInteraction>() == null;
    }

    public Vector3 ConstrainBookToRoom(BookItem book, Vector3 desired)
    {
        if (book == null || playerCamera == null) return desired;
        // Use a rotation-independent envelope, at least as large as the released
        // book. Unlike the old camera push this only limits motion at obstacles.
        float radius = 0.05f;
        var box = book.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            Vector3 scale = box.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            radius = Vector3.Scale(box.size, scale).magnitude * 0.5f +
                Vector3.Distance(box.transform.TransformPoint(box.center), book.transform.position);
            Vector3 current = book.transform.lossyScale;
            Vector3 original = book.OriginalScale;
            float ratio = Mathf.Max(1f, Mathf.Abs(original.x) / Mathf.Max(0.0001f, Mathf.Abs(current.x)),
                Mathf.Abs(original.y) / Mathf.Max(0.0001f, Mathf.Abs(current.y)),
                Mathf.Abs(original.z) / Mathf.Max(0.0001f, Mathf.Abs(current.z)));
            radius *= ratio;
        }
        radius += 0.02f;
        Vector3 origin = playerCamera.transform.position;
        // A player may be closer to glass than this envelope's radius. First
        // move the cast origin inward; casts alone miss initial overlaps.
        for (int pass = 0; pass < 6; pass++)
        {
            bool moved = false;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, radius, roomOverlaps,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps; i++)
            {
                var obstacle = roomOverlaps[i];
                if (!IsRoomObstacle(obstacle)) continue;
                if (!GameplayPhysics.TryClosestPoint(obstacle, origin, out var closestPoint)) continue;
                Vector3 away = origin - closestPoint;
                float distance = away.magnitude;
                if (distance < 0.0001f || distance >= radius) continue;
                origin += away / distance * (radius - distance + 0.005f);
                moved = true;
            }
            if (!moved) break;
        }
        Vector3 travel = desired - origin;
        float length = travel.magnitude;
        if (length < 0.0001f) return origin;
        Vector3 direction = travel / length;
        float allowed = length;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, roomHits, length,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == roomHits.Length) return origin;
        for (int i = 0; i < count; i++)
            if (IsRoomObstacle(roomHits[i].collider))
                allowed = Mathf.Min(allowed, Mathf.Max(0f, roomHits[i].distance - 0.01f));
        // The center ray also catches a thin wall if a cast starts in contact.
        count = Physics.RaycastNonAlloc(origin, direction, roomHits, length,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == roomHits.Length) return origin;
        for (int i = 0; i < count; i++)
            if (IsRoomObstacle(roomHits[i].collider))
                allowed = Mathf.Min(allowed, Mathf.Max(0f, roomHits[i].distance - radius));
        return origin + direction * allowed;
    }

    void ThrowBook(BookItem book, Vector3 velocity, Vector3 spinAxis, float spin, bool charged)
    {
        if (book == null)
            return;

        Vector3 worldPosition = ConstrainBookToRoom(book, book.transform.position);
        Quaternion worldRotation = book.transform.rotation;
        NetworkBook networkBook = book.GetComponent<NetworkBook>();
        if (networkBook != null && networkBook.IsSpawned)
        {
            book.transform.SetParent(null, true);
            RepositionHeldBooksImmediate();
            networkBook.ReleaseRpc(worldPosition, worldRotation, velocity, spinAxis, spin, charged);
            return;
        }

        book.transform.SetParent(null, true);
        ShopAudio.Play(ShopCue.Release, worldPosition);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        IgnorePlayerCollision(book, true);
        RepositionHeldBooksImmediate();

        book.SetHeld(false);
        Physics.SyncTransforms();

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 10f;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 12;

            rb.AddForce(velocity, ForceMode.VelocityChange);

            if (charged && spinAxis.sqrMagnitude > 0.0001f)
                rb.angularVelocity = spinAxis.normalized * spin;

            rb.WakeUp();

            // Sarjli atista kitap diger kitaplara CARPAR ama onlari SAVURMAZ.
            if (charged && book.GetComponent<ThrownBook>() == null)
                book.gameObject.AddComponent<ThrownBook>().Configure(spinAxis, transform);
        }

        pendingCollisionRestores.Add(new CollisionRestore { book = book, deadline = Time.unscaledTime + 2f, readyAt = -1f });
    }

    void IgnorePlayerCollision(BookItem book, bool ignore)
    {
        if (book == null)
            return;

        if (ignore) ignoredPlayerBooks.Add(book);
        else ignoredPlayerBooks.Remove(book);
        for (int i = pendingCollisionRestores.Count - 1; i >= 0; i--)
            if (pendingCollisionRestores[i].book == book) pendingCollisionRestores.RemoveAt(i);
        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        Collider[] bookColliders = book.GetComponentsInChildren<Collider>(true);

        foreach (Collider bookCollider in bookColliders)
        {
            if (bookCollider == null)
                continue;

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null && bookCollider != playerCollider &&
                    playerCollider.GetComponentInParent<BookItem>() == null)
                    Physics.IgnoreCollision(bookCollider, playerCollider, ignore);
            }
        }
    }

    private struct CollisionRestore
    {
        public BookItem book;
        public float deadline, readyAt;
    }
    private readonly HashSet<BookItem> ignoredPlayerBooks = new HashSet<BookItem>();
    private readonly List<CollisionRestore> pendingCollisionRestores = new List<CollisionRestore>();

    private void UpdateCollisionRestoration()
    {
        ignoredPlayerBooks.RemoveWhere(book => book == null);
        for (int i = pendingCollisionRestores.Count - 1; i >= 0; i--)
        {
            var pending = pendingCollisionRestores[i];
            if (pending.book == null) { pendingCollisionRestores.RemoveAt(i); continue; }
            if (pending.book.IsHeld) { pendingCollisionRestores.RemoveAt(i); continue; }
            var rb = pending.book.GetComponent<Rigidbody>();
            if (pending.readyAt < 0f && (rb == null || rb.isKinematic || rb.IsSleeping() || Time.unscaledTime >= pending.deadline))
            {
                pending.readyAt = Time.unscaledTime + Mathf.Max(0f, playerCollisionRestoreDelay);
                pendingCollisionRestores[i] = pending;
            }
            if (pending.readyAt >= 0f && Time.unscaledTime >= pending.readyAt)
                IgnorePlayerCollision(pending.book, false);
        }
    }

    private void RestoreAllPlayerCollisions()
    {
        foreach (var book in new List<BookItem>(ignoredPlayerBooks))
            if (book != null) IgnorePlayerCollision(book, false);
        ignoredPlayerBooks.Clear();
        pendingCollisionRestores.Clear();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        CancelHandAnimations();
        RepositionHeldBooksImmediate();
    }

    void OnDisable()
    {
        CancelHandAnimations();
        RepositionHeldBooksImmediate();
        RestoreAllPlayerCollisions();
    }
}
