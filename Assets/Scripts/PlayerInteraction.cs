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
    [Tooltip("Kitaplar arasinda birakilacak minimum gorunur bosluk.")]
    public float stackSpacing = 0.015f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;

    [Header("Birakma Ayarlari")]
    [Tooltip("Birakilan kitap baska bir collider ile ic iceyse en fazla bu kadar ayri iter.")]
    [Min(0)] public int dropPenetrationPasses = 6;
    [Tooltip("Penetration duzeltmesinin hassasiyeti.")]
    [Min(0f)] public float dropPenetrationPadding = 0.002f;
    [Tooltip("Oyuncu ile carpisma, kitap yere oturana kadar gecici olarak yok sayilir.")]
    [Min(0f)] public float playerCollisionRestoreDelay = 0.1f;

    [Header("Etkilesim")]
    public float interactRange = 3f;
    public LayerMask interactMask = ~0;

    [Header("Tusu")]
    public KeyCode pickupKey = KeyCode.Mouse0;
    public KeyCode dropKey = KeyCode.Mouse1;

    private readonly List<BookItem> heldBooks = new List<BookItem>();
    public IReadOnlyList<BookItem> HeldBooksList => heldBooks;
    public int MaxHeldBooks => maxHeldBooks;

    private BookItem lookedBook;
    private ShelfSlot lookedSlot;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        interactMask |= 1 << 0;

        if (FindFirstObjectByType<Crosshair>() == null)
            gameObject.AddComponent<Crosshair>();
    }

    void Update()
    {
        HandleLookDetection();

        if (Input.GetKeyDown(KeyCode.Mouse0))
            HandlePickupPress();

        if (Input.GetKeyDown(KeyCode.Mouse1))
            HandleDropOrPlacePress();
    }

    void HandleLookDetection()
    {
        if (playerCamera == null)
            return;

        if (lookedBook != null)
            lookedBook.SetHighlight(false);

        lookedBook = null;
        lookedSlot = null;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool canPickMore = heldBooks.Count < maxHeldBooks;

        foreach (RaycastHit hit in hits)
        {
            BookItem book = hit.collider.GetComponentInParent<BookItem>();
            ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();

            if (book != null && canPickMore && !book.IsHeld && book.currentSlot == null)
            {
                lookedBook = book;
                break;
            }

            if (slot != null)
            {
                lookedSlot = slot;
                break;
            }
        }

        if (lookedBook != null)
        {
            lookedSlot = null;
            lookedBook.SetHighlight(true);
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

    void HandleDropOrPlacePress()
    {
        if (lookedSlot != null && heldBooks.Count > 0)
        {
            PlaceOneMatchingBook();
            return;
        }

        DropTopBook();
    }

    void TakeFromShelf()
    {
        if (lookedSlot == null || lookedSlot.FilledCount <= 0 || heldBooks.Count >= maxHeldBooks)
            return;

        BookItem book = lookedSlot.TakeFirstBook();
        if (book == null)
            return;

        book.SetHighlight(false);
        book.SetHeld(true);
        heldBooks.Add(book);
        RepositionHeldBooks();
        lookedSlot = null;
    }

    void PickUp(BookItem book)
    {
        if (book == null || heldBooks.Count >= maxHeldBooks)
            return;

        if (book.currentSlot != null)
            book.currentSlot.RemoveBook(book);

        book.SetHighlight(false);
        book.SetHeld(true);
        heldBooks.Add(book);
        RepositionHeldBooks();
        lookedBook = null;
    }

    void RepositionHeldBooks()
    {
        if (rightHandPoint == null)
            return;

        Vector3 stackUp = rightHandPoint.up;
        bool hasPreviousBook = false;
        float previousMax = 0f;

        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            book.transform.SetParent(rightHandPoint, false);

            // Once kitabin sabit aralikla itilmesi yerine, gercek gorunen modelin
            // yuksekligini olcerek bir sonraki kitabi onun hemen ustune koyuyoruz.
            // Boylece farkli FBX boyutlari ve Base Rotation degerleri birbirinin
            // icine girmiyor.
            book.transform.localPosition = Vector3.zero;
            book.transform.localRotation = book.NativeRotation;
            book.transform.localScale = book.OriginalScale * heldScaleMultiplier;

            float minProjection;
            float maxProjection;
            GetVisualProjection(book, stackUp, out minProjection, out maxProjection);

            if (hasPreviousBook)
            {
                float offsetAlongStack = previousMax + stackSpacing - minProjection;
                book.transform.position += stackUp * offsetAlongStack;
                minProjection += offsetAlongStack;
                maxProjection += offsetAlongStack;
            }

            previousMax = maxProjection;
            hasPreviousBook = true;
        }
    }

    void GetVisualProjection(BookItem book, Vector3 axis, out float minProjection, out float maxProjection)
    {
        Renderer[] renderers = book.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        minProjection = float.MaxValue;
        maxProjection = float.MinValue;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                continue;

            Bounds bounds = renderer.bounds;
            float center = Vector3.Dot(bounds.center, axis);
            float radius = Mathf.Abs(axis.x) * bounds.extents.x +
                           Mathf.Abs(axis.y) * bounds.extents.y +
                           Mathf.Abs(axis.z) * bounds.extents.z;

            minProjection = Mathf.Min(minProjection, center - radius);
            maxProjection = Mathf.Max(maxProjection, center + radius);
            found = true;
        }

        if (!found)
        {
            float center = Vector3.Dot(book.transform.position, axis);
            minProjection = center;
            maxProjection = center;
        }
    }

    void PlaceOneMatchingBook()
    {
        if (lookedSlot == null || heldBooks.Count == 0)
            return;

        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            if (!lookedSlot.Matches(book))
                continue;

            if (lookedSlot.PlaceBook(book))
            {
                heldBooks.RemoveAt(i);
                RepositionHeldBooks();
            }
            return;
        }
    }

    void DropTopBook()
    {
        if (heldBooks.Count == 0)
            return;

        BookItem book = heldBooks[heldBooks.Count - 1];
        heldBooks.RemoveAt(heldBooks.Count - 1);

        Vector3 worldPosition = book.transform.position;
        Quaternion worldRotation = book.transform.rotation;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        // Collider'lari acmadan once oyuncu ile olan carpismayi kapatiyoruz.
        // Boylece birakma aninda kitap oyuncunun collider'ina firlamiyor.
        IgnorePlayerCollision(book, true);

        book.SetHeld(false);

        // Kitap baska bir kitap/raf/zemin ile ilk anda ic ice kaldiysa, Rigidbody'yi
        // dinamik yapmadan once sadece gerekli miktarda disari iteriz. Bu, ust uste
        // ayni yere birakilan kitaplarin ic ice spawn olmasini engeller; kitap yine
        // normal fizik ile asagi dusmeye devam eder.
        ResolveInitialPenetration(book);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.WakeUp();
        }

        StartCoroutine(IgnorePlayerCollisionUntilSettled(book));
        RepositionHeldBooks();
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

    void ResolveInitialPenetration(BookItem book)
    {
        if (book == null || dropPenetrationPasses <= 0)
            return;

        Collider[] bookColliders = book.GetComponentsInChildren<Collider>(true);
        if (bookColliders.Length == 0)
            return;

        Collider[] nearbyColliders = Physics.OverlapSphere(
            book.transform.position,
            GetBookBoundsRadius(book),
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int pass = 0; pass < dropPenetrationPasses; pass++)
        {
            bool moved = false;

            foreach (Collider bookCollider in bookColliders)
            {
                if (bookCollider == null || !bookCollider.enabled)
                    continue;

                foreach (Collider other in nearbyColliders)
                {
                    if (other == null || other == bookCollider || !other.enabled)
                        continue;

                    BookItem otherBook = other.GetComponentInParent<BookItem>();
                    if (otherBook == book)
                        continue;

                    // Oyuncu collider'larini burada duzeltmeye dahil etmiyoruz;
                    // onlar zaten gecici olarak IgnoreCollision durumunda.
                    if (other.transform.IsChildOf(transform))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (Physics.ComputePenetration(
                        bookCollider,
                        bookCollider.transform.position,
                        bookCollider.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance))
                    {
                        if (distance <= 0f)
                            continue;

                        book.transform.position += direction * (distance + dropPenetrationPadding);
                        moved = true;
                    }
                }
            }

            if (!moved)
                break;
        }
    }

    float GetBookBoundsRadius(BookItem book)
    {
        Renderer[] renderers = book.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(book.transform.position, Vector3.zero);
        bool initialized = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!initialized)
            return 0.5f;

        return bounds.extents.magnitude + 0.1f;
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
