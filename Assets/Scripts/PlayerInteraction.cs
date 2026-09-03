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
    public float stackSpacing = 10f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;

    [Header("Birakma Ayarlari")]
    [Tooltip("Kitabin elden birakildiginda ileri dogru kazanacagi hiz.")]
    [Min(0f)] public float dropForwardForce = 2.5f;
    [Tooltip("Kitabin elden birakildiginda yukari dogru kazanacagi hiz.")]
    [Min(0f)] public float dropUpwardForce = 0.75f;
    [Tooltip("Bırakma noktasi doluysa, kitap fizik sistemine girmeden once bu mesafe boyunca ileri dogru bos nokta aranir.")]
    [Min(0f)] public float dropClearanceDistance = 2f;
    [Tooltip("Bos birakma noktasi ararken kullanilan adim mesafesi.")]
    [Min(0.01f)] public float dropClearanceStep = 0.05f;

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
        IgnorePlayerCollision(book, true);
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
        IgnorePlayerCollision(book, true);
        book.SetHeld(true);
        heldBooks.Add(book);
        RepositionHeldBooks();
        lookedBook = null;
    }

    void RepositionHeldBooks()
    {
        if (rightHandPoint == null)
            return;

        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            if (book == null)
                continue;

            book.transform.SetParent(rightHandPoint, false);
            book.transform.localPosition = new Vector3(0f, i * stackSpacing, 0f);
            book.transform.localRotation = book.NativeRotation;
            book.transform.localScale = book.OriginalScale * heldScaleMultiplier;
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
                // Raf kitabi oyuncuyla tekrar etkilesebilir hale getirir.
                IgnorePlayerCollision(book, false);
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
        Transform originalParent = book.transform.parent;
        Vector3 originalLocalPosition = book.transform.localPosition;
        Quaternion originalLocalRotation = book.transform.localRotation;
        Vector3 originalLocalScale = book.transform.localScale;

        Vector3 worldPosition = book.transform.position;
        Quaternion worldRotation = book.transform.rotation;

        Vector3 throwDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        throwDirection.y = 0f;
        if (throwDirection.sqrMagnitude < 0.0001f)
            throwDirection = transform.forward;
        throwDirection.Normalize();

        // Kitap eldeyken collider kapali ve Rigidbody kinematic/detectCollisions=false.
        // Once collider'lari sadece geometrik birakma noktasi kontrolu icin aciyoruz.
        // Fizik henuz devreye girmiyor; ComputePenetration veya sonradan itme yok.
        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
        }

        SetBookCollidersEnabled(book, true);
        Physics.SyncTransforms();

        if (!TryFindClearDropPosition(book, worldPosition, throwDirection, out Vector3 clearPosition))
        {
            // Bos nokta bulunamadiysa kitabi zorla birakip baska bir kitabin icine sokma.
            // Kitap oldugu eldeki konuma aynen geri doner.
            SetBookCollidersEnabled(book, false);
            RestoreHeldBookTransform(book, originalParent, originalLocalPosition, originalLocalRotation, originalLocalScale);
            return;
        }

        book.transform.position = clearPosition;
        Physics.SyncTransforms();

        heldBooks.RemoveAt(heldBooks.Count - 1);
        RepositionHeldBooks();

        // Gercek fizik tam olarak clearPosition'da basliyor.
        book.SetHeld(false);
        Physics.SyncTransforms();
        IgnorePlayerCollision(book, true);

        rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
            // CCD modu BookItem.SetHeld(false) tarafinda merkezi olarak ayarlaniyor.
            rb.maxDepenetrationVelocity = 10f;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 12;

            rb.AddForce(
                throwDirection * dropForwardForce + Vector3.up * dropUpwardForce,
                ForceMode.VelocityChange);
            rb.WakeUp();
        }
    }

    bool TryFindClearDropPosition(BookItem book, Vector3 startPosition, Vector3 direction, out Vector3 clearPosition)
    {
        clearPosition = startPosition;

        Collider[] bookColliders = book.GetComponentsInChildren<Collider>(true);
        if (bookColliders.Length == 0)
            return true;

        int bookLayerMask = 1 << 8;
        float step = Mathf.Max(0.01f, dropClearanceStep);
        float maxDistance = Mathf.Max(0f, dropClearanceDistance);

        for (float distance = 0f; distance <= maxDistance + 0.0001f; distance += step)
        {
            Vector3 candidatePosition = startPosition + direction * distance;
            bool blocked = false;

            foreach (Collider bookCollider in bookColliders)
            {
                if (bookCollider == null || !bookCollider.enabled)
                    continue;

                Bounds bounds = bookCollider.bounds;
                Vector3 center = bounds.center + (candidatePosition - startPosition);
                Vector3 extents = bounds.extents + Vector3.one * 0.01f;

                Collider[] overlaps = Physics.OverlapBox(
                    center,
                    extents,
                    Quaternion.identity,
                    bookLayerMask,
                    QueryTriggerInteraction.Ignore);

                foreach (Collider otherCollider in overlaps)
                {
                    if (otherCollider == null)
                        continue;

                    BookItem otherBook = otherCollider.GetComponentInParent<BookItem>();
                    if (otherBook != null && otherBook != book && !otherBook.IsHeld)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    break;
            }

            if (!blocked)
            {
                clearPosition = candidatePosition;
                return true;
            }
        }

        return false;
    }

    void SetBookCollidersEnabled(BookItem book, bool enabled)
    {
        if (book == null)
            return;

        Collider[] colliders = book.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    void RestoreHeldBookTransform(
        BookItem book,
        Transform originalParent,
        Vector3 originalLocalPosition,
        Quaternion originalLocalRotation,
        Vector3 originalLocalScale)
    {
        if (book == null)
            return;

        book.transform.SetParent(originalParent, false);
        book.transform.localPosition = originalLocalPosition;
        book.transform.localRotation = originalLocalRotation;
        book.transform.localScale = originalLocalScale;

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
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
}
