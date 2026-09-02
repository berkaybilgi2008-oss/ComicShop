using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public Camera playerCamera;
    public Transform rightHandPoint;

    [Header("Tasima Ayarlari")]
    public int maxHeldBooks = 10;
    public float stackSpacing = 0.06f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;
    [Range(0.2f, 1f)] public float heldThicknessMultiplier = 0.8f;

    [Header("Etkilesim")]
    public float interactRange = 3f;
    public LayerMask interactMask;

    [Header("Tusu")]
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.Q;

    private readonly List<BookItem> heldBooks = new List<BookItem>();
    public IReadOnlyList<BookItem> HeldBooksList => heldBooks;
    public int MaxHeldBooks => maxHeldBooks;

    private BookItem lookedBook;
    private ShelfSlot lookedSlot;

    void Update()
    {
        HandleLookDetection();

        if (Input.GetKeyDown(pickupKey))
            HandlePickupPress();

        if (Input.GetKeyDown(dropKey))
            DropTopBook();
    }

    void HandleLookDetection()
    {
        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (lookedBook != null)
        {
            lookedBook.SetHighlight(false);
            lookedBook = null;
        }

        lookedSlot = null;

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        BookItem foundBook = null;
        ShelfSlot foundSlot = null;

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            BookItem book = hit.collider.GetComponentInParent<BookItem>();
            if (book != null && canPickMore && !book.IsHeld && foundBook == null)
                foundBook = book;

            ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();
            if (slot != null && foundSlot == null)
                foundSlot = slot;
        }

        // Raf bolmesine bakiliyorsa bolum her zaman onceliklidir.
        // Boylece raftaki kitaplari tek tek secmek yerine bolumden sirayla aliriz.
        if (foundSlot != null && (foundSlot.FilledCount > 0 || heldBooks.Count > 0))
        {
            lookedSlot = foundSlot;
            return;
        }

        if (foundBook != null)
        {
            lookedBook = foundBook;
            lookedBook.SetHighlight(true);
        }
        else
        {
            lookedSlot = foundSlot;
        }
    }

    void HandlePickupPress()
    {
        // Elde kitap varsa E = baktigin raf bolmesine bir tane koy.
        if (lookedSlot != null && heldBooks.Count > 0)
        {
            PlaceOneMatchingBook();
            return;
        }

        // Elde kitap yoksa E = baktigin raf bolmesinden siradaki kitabi al.
        if (lookedSlot != null && heldBooks.Count == 0)
        {
            TakeFromShelf();
            return;
        }

        // Raf degilse normal yerdeki kitabi al.
        if (lookedBook != null && heldBooks.Count < maxHeldBooks)
            PickUp(lookedBook);
    }

    void TakeFromShelf()
    {
        if (lookedSlot == null || heldBooks.Count >= maxHeldBooks)
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
        if (book == null)
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
        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            book.transform.SetParent(rightHandPoint);
            book.transform.localPosition = new Vector3(0f, i * stackSpacing, 0f);
            book.transform.localRotation = Quaternion.identity;

            Vector3 baseScale = book.OriginalScale * heldScaleMultiplier;
            book.transform.localScale = new Vector3(
                baseScale.x,
                baseScale.y * heldThicknessMultiplier,
                baseScale.z
            );
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

        book.transform.SetParent(null);
        book.transform.position = worldPosition;
        book.transform.rotation = worldRotation;
        book.transform.localScale = book.OriginalScale;
        book.SetHeld(false);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        StartCoroutine(IgnorePlayerCollisionUntilSettled(book));
        RepositionHeldBooks();
    }

    IEnumerator IgnorePlayerCollisionUntilSettled(BookItem book)
    {
        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        Collider[] bookColliders = book != null ? book.GetComponentsInChildren<Collider>() : null;
        if (bookColliders == null)
            yield break;

        foreach (Collider bookCollider in bookColliders)
        {
            if (bookCollider == null) continue;
            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(bookCollider, playerCollider, true);
            }
        }

        Rigidbody rb = book.GetComponent<Rigidbody>();
        while (book != null && rb != null && !rb.isKinematic)
            yield return null;

        yield return new WaitForSeconds(0.1f);
        if (book == null)
            yield break;

        foreach (Collider bookCollider in bookColliders)
        {
            if (bookCollider == null) continue;
            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(bookCollider, playerCollider, false);
            }
        }
    }
}
