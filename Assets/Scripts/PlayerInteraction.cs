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
    [Tooltip("Kitaplar arasinda birakilacak minimum bosluk.")]
    public float stackSpacing = 0.005f;
    [Tooltip("Kitaplarin eldeki stack icinde ne kadar ust uste binmesine izin verilecegi. 0 = hic overlap yok.")]
    [Range(0f, 0.35f)] public float stackOverlap = 0.12f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;

    [Header("Birakma Ayarlari")]
    [Min(1)] public int dropPenetrationPasses = 8;
    [Min(0f)] public float dropPenetrationPadding = 0.003f;
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
        float nextStackPosition = 0f;

        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            book.transform.SetParent(rightHandPoint, false);
            book.transform.localRotation = book.NativeRotation;
            book.transform.localScale = book.OriginalScale * heldScaleMultiplier;

            // Onceki kitabin bitis noktasini bulup, yeni kitabi onun hemen arkasina
            // yerlestiriyoruz. Stack overlap degeri kitaplarin tamamen ayrik durup
            // elde gereksiz uzun bir kule olusturmasini engelliyor.
            float halfHeight = GetColliderProjectedHalfHeight(book, stackUp);
            float centerPosition = nextStackPosition + halfHeight;
            book.transform.localPosition = stackUp * centerPosition;

            nextStackPosition = centerPosition + halfHeight;
            nextStackPosition -= halfHeight * stackOverlap;
            nextStackPosition += stackSpacing;
        }
    }

    float GetColliderProjectedHalfHeight(BookItem book, Vector3 axis)
    {
        Collider[] colliders = book.GetComponentsInChildren<Collider>(true);
        bool found = false;
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            Bounds b = col.bounds;
            float center = Vector3.Dot(b.center, axis);
            float radius = Mathf.Abs(axis.x) * b.extents.x +
                           Mathf.Abs(axis.y) * b.extents.y +
                           Mathf.Abs(axis.z) * b.extents.z;

            min = Mathf.Min(min, center - radius);
            max = Mathf.Max(max, center + radius);
            found = true;
        }

        if (!found)
            return 0.05f;

        return Mathf.Max(0.001f, (max - min) * 0.5f);
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

        // Collider acilmadan once oyuncu ile carpismayi kapat.
        IgnorePlayerCollision(book, true);
        book.SetHeld(false);

        // SetHeld collider'lari actigi icin Unity broadphase'i hemen guncelle.
        // Aksi halde ayni noktadaki daha once birakilmis kitaplar OverlapSphere'da
        // bir sonraki fizik adimina kadar gorunmeyebilir.
        Physics.SyncTransforms();
        ResolveInitialPenetration(book);
        Physics.SyncTransforms();

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

        for (int pass = 0; pass < dropPenetrationPasses; pass++)
        {
            Physics.SyncTransforms();

            float radius = GetBookBoundsRadius(book);
            Collider[] nearbyColliders = Physics.OverlapSphere(
                book.transform.position,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);

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

                    if (other.transform.IsChildOf(transform))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (!Physics.ComputePenetration(
                        bookCollider,
                        bookCollider.transform.position,
                        bookCollider.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance))
                        continue;

                    if (distance <= 0f)
                        continue;

                    book.transform.position += direction * (distance + dropPenetrationPadding);
                    moved = true;
                    Physics.SyncTransforms();
                }
            }

            if (!moved)
                break;
        }
    }

    float GetBookBoundsRadius(BookItem book)
    {
        Collider[] colliders = book.GetComponentsInChildren<Collider>(true);
        Bounds bounds = new Bounds(book.transform.position, Vector3.zero);
        bool initialized = false;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            if (!initialized)
            {
                bounds = col.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (!initialized)
            return 0.5f;

        return bounds.extents.magnitude + 0.05f;
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
