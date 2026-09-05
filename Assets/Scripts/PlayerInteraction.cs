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
    private bool isThrowing;
    private float chargeAmount;
    private BookItem chargingBook;
    private Vector3 enterStartPosition;
    private Quaternion enterStartRotation;
    private Vector3 enterStartScale = Vector3.one;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        interactMask |= 1 << 0;

        crosshair = FindFirstObjectByType<Crosshair>();
        if (crosshair == null)
            crosshair = gameObject.AddComponent<Crosshair>();
    }

    void Update()
    {
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

        if (Input.GetKeyDown(pickupKey))
            HandlePickupPress();

        if (Input.GetKeyDown(dropKey) && heldBooks.Count > 0)
            HandleDropOrPlacePress();

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && heldBooks.Count > 1)
            ChangeActiveHeldBook(wheel < 0f ? 1 : -1);
    }

    void HandleLookDetection()
    {
        if (playerCamera == null)
            return;

        lookedBook = null;
        lookedSlot = null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        ShelfSlot nearestSlot = null;
        BookItem nearestBook = null;

        // Raf collider'i, kitap collider'indan once gelebiliyor. Bu yuzden ikisini de
        // ayri ayri topluyoruz: en yakin serbest kitap + en yakin raf gozu.
        // (Eskiden kitap bulununca lookedSlot null'lanip rafa koyma tamamen bloklaniyordu.)
        foreach (RaycastHit hit in hits)
        {
            if (nearestBook == null)
            {
                BookItem book = hit.collider.GetComponentInParent<BookItem>();
                if (book != null && canPickMore && !book.IsHeld && book.currentSlot == null)
                    nearestBook = book;
            }

            if (nearestSlot == null)
            {
                ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();

                // Slot, kendi child collider'i uzerinden de bulunabilsin.
                if (slot == null)
                    slot = hit.collider.GetComponentInChildren<ShelfSlot>();

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

        float angle = Mathf.Lerp(windupStartAngle, windupFullAngle, chargeAmount);
        GetThrowPose(chargingBook, angle, chargeAmount, out Vector3 position, out Quaternion rotation);

        float blend = EvaluateBookMoveCurve(Mathf.Clamp01(elapsed / chargeEnterDuration));

        chargingBook.transform.position = Vector3.LerpUnclamped(enterStartPosition, position, blend);
        chargingBook.transform.rotation = Quaternion.SlerpUnclamped(enterStartRotation, rotation, blend);
        chargingBook.transform.localScale = Vector3.LerpUnclamped(
            enterStartScale, chargingBook.OriginalScale * chargeScaleMultiplier, blend);
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

        float startAngle = Mathf.Lerp(windupStartAngle, windupFullAngle, finalCharge);
        float elapsed = 0f;

        // Bas arkasindan one dogru tek temiz yay; sona dogru hizlanir (bilek sokumu).
        while (elapsed < throwArcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwArcDuration);
            // Kubik egri: basta yuklenme hissi, sonda kirbac gibi bilek sokumu.
            float angle = Mathf.Lerp(startAngle, releaseAngle, t * t * t);

            GetThrowPose(book, angle, 0f, out Vector3 position, out Quaternion rotation);
            book.transform.position = position;
            book.transform.rotation = rotation;
            book.transform.localScale = book.OriginalScale * chargeScaleMultiplier;

            yield return null;
        }

        isThrowing = false;

        int index = heldBooks.IndexOf(book);
        if (index < 0)
            yield break;

        heldBooks.RemoveAt(index);
        activeHeldIndex = heldBooks.Count == 0 ? -1 : Mathf.Clamp(index, 0, heldBooks.Count - 1);

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
        {
            float n = Time.time * chargeShakeSpeed;
            Vector3 noise = new Vector3(
                Mathf.PerlinNoise(n, 0.13f) - 0.5f,
                Mathf.PerlinNoise(0.47f, n) - 0.5f,
                Mathf.PerlinNoise(n, n) - 0.5f) * 2f;

            position += noise * (chargeShakeAmount * shake);
        }

        // Kitabin BOYU kolun dogrultusunda -- yay boyunca kolla beraber doner.
        rotation = book != null
            ? book.GetAlignedRotation(coverNormal, armDirection)
            : Quaternion.identity;
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

        BookItem book = lookedSlot.TakeLastBook();
        if (book == null)
            return;

        book.SetHighlight(false);
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        heldBooks.Add(book);
        activeHeldIndex = heldBooks.Count - 1;
        lookedSlot = null;

        StartCoroutine(MoveBookIntoHand(book));
    }

    void PickUp(BookItem book)
    {
        if (book == null || heldBooks.Count >= maxHeldBooks)
            return;

        if (book.currentSlot != null)
            book.currentSlot.RemoveBook(book);

        book.SetHighlight(false);
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        heldBooks.Add(book);
        activeHeldIndex = heldBooks.Count - 1;
        lookedBook = null;

        StartCoroutine(MoveBookIntoHand(book));
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
        Quaternion targetLocalRotation = book.NativeRotation;
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
            targetRotations[i] = book.NativeRotation;
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

        if (!lookedSlot.Matches(book))
        {
            if (debugPlacement)
                Debug.Log($"[Yerlestirme] '{lookedSlot.name}' bu kitabi kabul etmiyor " +
                          $"(slot brand {lookedSlot.brandID}, kitap brand {book.brandID}, " +
                          $"dolu {lookedSlot.FilledCount}/{lookedSlot.capacity}, " +
                          $"sahip bookID {lookedSlot.OwnerBookID}, kitap bookID {book.bookID}).");
            return false;
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
            book.transform.localRotation = book.NativeRotation;
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

    void ThrowBook(BookItem book, Vector3 velocity, Vector3 spinAxis, float spin, bool charged)
    {
        if (book == null)
            return;

        Vector3 worldPosition = book.transform.position;
        Quaternion worldRotation = book.transform.rotation;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        IgnorePlayerCollision(book, true);
        RepositionHeldBooksImmediate();
        RestoreBookToBookCollisions(book);

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
                book.gameObject.AddComponent<ThrownBook>().Configure(spinAxis);
        }

        StartCoroutine(IgnorePlayerCollisionUntilSettled(book));
    }

    void RestoreBookToBookCollisions(BookItem releasedBook)
    {
        if (releasedBook == null)
            return;

        Collider[] releasedColliders = releasedBook.GetComponentsInChildren<Collider>(true);
        BookItem[] allBooks = FindObjectsByType<BookItem>(FindObjectsSortMode.None);

        foreach (BookItem otherBook in allBooks)
        {
            if (otherBook == null || otherBook == releasedBook)
                continue;

            Collider[] otherColliders = otherBook.GetComponentsInChildren<Collider>(true);

            foreach (Collider releasedCollider in releasedColliders)
            {
                if (releasedCollider == null || !releasedCollider.enabled)
                    continue;

                foreach (Collider otherCollider in otherColliders)
                {
                    if (otherCollider == null || !otherCollider.enabled || releasedCollider == otherCollider)
                        continue;

                    Physics.IgnoreCollision(releasedCollider, otherCollider, false);
                }
            }
        }
    }

    void IgnorePlayerCollision(BookItem book, bool ignore)
    {
        if (book == null)
            return;

        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        Collider[] bookColliders = book.GetComponentsInChildren<Collider>(true);

        foreach (Collider bookCollider in bookColliders)
        {
            if (bookCollider == null)
                continue;

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null && bookCollider != playerCollider)
                    Physics.IgnoreCollision(bookCollider, playerCollider, ignore);
            }
        }
    }

    IEnumerator IgnorePlayerCollisionUntilSettled(BookItem book)
    {
        Rigidbody rb = book != null ? book.GetComponent<Rigidbody>() : null;

        while (book != null && rb != null && !rb.isKinematic)
            yield return null;

        if (playerCollisionRestoreDelay > 0f)
            yield return new WaitForSeconds(playerCollisionRestoreDelay);

        if (book == null)
            yield break;

        IgnorePlayerCollision(book, false);
    }
}