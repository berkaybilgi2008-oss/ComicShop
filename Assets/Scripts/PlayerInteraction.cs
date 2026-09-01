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
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (lookedBook != null)
        {
            lookedBook.SetHighlight(false);
            lookedBook = null;
        }

        lookedSlot = null;

        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        BookItem foundBook = null;
        ShelfSlot foundSlot = null;

        foreach (RaycastHit hit in hits)
        {
            BookItem book = hit.collider.GetComponentInParent<BookItem>();
            if (book != null && canPickMore && !book.IsHeld)
            {
                foundBook = book;
                break;
            }
        }

        if (foundBook == null)
        {
            foreach (RaycastHit hit in hits)
            {
                ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();
                if (slot != null && heldBooks.Count > 0)
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
            AutoPlaceAllMatchingBooks();
            return;
        }

        if (heldBooks.Count > 0 && lookedBook == null && lookedSlot == null)
            DropTopBook();
    }

    void AutoPlaceAllMatchingBooks()
    {
        ShelfSlot[] allSlots = FindObjectsOfType<ShelfSlot>();

        for (int i = heldBooks.Count - 1; i >= 0; i--)
        {
            BookItem book = heldBooks[i];
            ShelfSlot matchingSlot = FindAvailableSlotFor(book, allSlots);

            if (matchingSlot != null && matchingSlot.PlaceBook(book))
                heldBooks.RemoveAt(i);
        }

        RepositionHeldBooks();
    }

    ShelfSlot FindAvailableSlotFor(BookItem book, ShelfSlot[] allSlots)
    {
        foreach (ShelfSlot slot in allSlots)
        {
            if (slot.Matches(book))
                return slot;
        }
        return null;
    }

    void PickUp(BookItem book)
    {
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

        // Kitabi oneki el pozisyonundan cikartiyoruz; artik ileriye teleport etmiyoruz.
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

        // Kitap oyuncunun collider'ina takilmasin. Kitap tamamen durdugunda
        // normal oyuncu-kitap carpismasi tekrar acilir.
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

        // Kinematic olduktan sonra cok kisa bir pay birakiyoruz; boylece son
        // fizik adiminda oyuncuya tekrar carpma olmaz.
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
