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

    private readonly List<BookItem> heldBooks = new List<BookItem>();
    public IReadOnlyList<BookItem> HeldBooksList => heldBooks;
    public int MaxHeldBooks => maxHeldBooks;

    private BookItem lookedBook;
    private ShelfSlot lookedSlot;

    void Update()
    {
        HandleLookDetection();

        if (Input.GetKeyDown(KeyCode.E))
            HandleInteractPress();
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

        // Kitaplar hangi Layer'a konmuş olursa olsun bulunabilsin.
        // Önceden interactMask kullanıldığı için Book prefab'ının collider'ı
        // maskede değilse oyuncu kitabı hiç göremiyordu.
        if (canPickMore)
        {
            RaycastHit[] bookHits = Physics.RaycastAll(
                ray,
                interactRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            System.Array.Sort(bookHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in bookHits)
            {
                BookItem book = hit.collider.GetComponentInParent<BookItem>();
                if (book != null && !book.IsHeld)
                {
                    foundBook = book;
                    break;
                }
            }
        }

        // Kitap bulunamadığında ve elde kitap varken raf bölmesini ara.
        if (foundBook == null && heldBooks.Count > 0)
        {
            RaycastHit[] shelfHits = Physics.RaycastAll(
                ray,
                interactRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            System.Array.Sort(shelfHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in shelfHits)
            {
                ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();
                if (slot != null)
                {
                    foundSlot = slot;
                    break;
                }
            }
        }

        if (foundBook != null)
        {
            lookedBook = foundBook;
            lookedBook.SetHighlight(true);
        }
        else if (foundSlot != null)
        {
            lookedSlot = foundSlot;
        }
    }

    void HandleInteractPress()
    {
        if (lookedBook != null && heldBooks.Count < maxHeldBooks)
        {
            PickUp(lookedBook);
            return;
        }

        if (heldBooks.Count > 0 && lookedSlot != null)
        {
            PlaceOneMatchingBook();
            return;
        }

        if (heldBooks.Count > 0 && lookedBook == null && lookedSlot == null)
            DropTopBook();
    }

    void PlaceOneMatchingBook()
    {
        if (lookedSlot == null)
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

    void DropTopBook()
    {
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
            if (bookCollider == null)
                continue;

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
            if (bookCollider == null)
                continue;

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null)
                    Physics.IgnoreCollision(bookCollider, playerCollider, false);
            }
        }
    }
}
