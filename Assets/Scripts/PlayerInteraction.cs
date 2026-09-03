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
        heldBooks.RemoveAt(heldBooks.Count - 1);

        Vector3 worldPosition = book.transform.position;
        Quaternion worldRotation = book.transform.rotation;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        RepositionHeldBooks();

        // Collider'i tekrar acip Physics'e yeniden dahil ediyoruz. IgnoreCollision
        // collider aktif olduktan sonra uygulanmali; aksi halde bu ayar kaybolabilir.
        book.SetHeld(false);
        Physics.SyncTransforms();
        IgnorePlayerCollision(book, true);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 10f;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 12;

            // Kitap elden birakildigi anda baska bir kitabin collider'inin
            // icinde baslayabilir. Physics.ComputePenetration ile once mevcut
            // kitaplardan fiziksel olarak ayir, sonra kuvvet uygula.
            ResolveBookOverlaps(book);

            Vector3 throwDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            throwDirection.y = 0f;
            if (throwDirection.sqrMagnitude < 0.0001f)
                throwDirection = transform.forward;
            throwDirection.Normalize();

            rb.AddForce(
                throwDirection * dropForwardForce + Vector3.up * dropUpwardForce,
                ForceMode.VelocityChange);
            rb.WakeUp();
        }
    }

    void ResolveBookOverlaps(BookItem droppedBook)
    {
        if (droppedBook == null)
            return;

        Collider[] droppedColliders = droppedBook.GetComponentsInChildren<Collider>(true);
        if (droppedColliders.Length == 0)
            return;

        BookItem[] allBooks = FindObjectsByType<BookItem>(FindObjectsSortMode.None);

        // Birden fazla kitap ust uste geldiyse birkac ayri gecis gerekebilir.
        // Her geciste ComputePenetration, kitabi en kisa yoldan disari tasir.
        for (int pass = 0; pass < 8; pass++)
        {
            bool foundOverlap = false;

            foreach (BookItem otherBook in allBooks)
            {
                if (otherBook == null || otherBook == droppedBook)
                    continue;

                Collider[] otherColliders = otherBook.GetComponentsInChildren<Collider>(true);
                foreach (Collider droppedCollider in droppedColliders)
                {
                    if (droppedCollider == null || !droppedCollider.enabled)
                        continue;

                    foreach (Collider otherCollider in otherColliders)
                    {
                        if (otherCollider == null || !otherCollider.enabled)
                            continue;

                        if (!Physics.ComputePenetration(
                                droppedCollider,
                                droppedCollider.transform.position,
                                droppedCollider.transform.rotation,
                                otherCollider,
                                otherCollider.transform.position,
                                otherCollider.transform.rotation,
                                out Vector3 direction,
                                out float distance))
                            continue;

                        foundOverlap = true;
                        droppedBook.transform.position += direction * (distance + 0.002f);
                        Physics.SyncTransforms();
                    }
                }
            }

            if (!foundOverlap)
                break;
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
