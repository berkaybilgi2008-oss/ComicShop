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
    }

    void Update()
    {
        HandleLookDetection();

        if (Input.GetKeyDown(pickupKey))
            HandlePickupPress();

        if (Input.GetKeyDown(dropKey))
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

        // Interaction is intentionally not coupled to the UI crosshair.
        // Search every physics layer, then decide from the actual component hit.
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        ShelfSlot nearestSlot = null;

        foreach (RaycastHit hit in hits)
        {
            BookItem book = hit.collider.GetComponentInParent<BookItem>();
            if (book != null)
            {
                if (canPickMore && !book.IsHeld)
                {
                    if (book.currentSlot == null)
                    {
                        lookedBook = book;
                        break;
                    }

                    if (nearestSlot == null)
                        nearestSlot = book.currentSlot;
                }

                continue;
            }

            ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();
            if (slot != null && nearestSlot == null)
                nearestSlot = slot;
        }

        if (lookedBook != null)
        {
            lookedSlot = null;
            lookedBook.SetHighlight(true);
        }
        else
        {
            lookedSlot = nearestSlot;
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

        BookItem book = lookedSlot.TakeLastBook();
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

        // Preserve the previous prototype rule: the first matching held book is placed.
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

        IgnorePlayerCollision(book, true);
        RepositionHeldBooks();

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

            Vector3 throwDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            throwDirection.y = 0f;
            if (throwDirection.sqrMagnitude < 0.0001f)
                throwDirection = transform.forward;
            throwDirection.Normalize();

            rb.AddForce(
                throwDirection * 2.5f + Vector3.up * 0.75f,
                ForceMode.VelocityChange);
            rb.WakeUp();
        }

        StartCoroutine(IgnorePlayerCollisionUntilSettled(book));
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

        float delay = 0.1f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (book == null)
            yield break;

        IgnorePlayerCollision(book, false);
    }
}
