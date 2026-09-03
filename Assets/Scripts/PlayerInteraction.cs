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
    public float stackSpacing = 0.09f;
    [Range(0.2f, 1f)] public float heldScaleMultiplier = 0.55f;

    [Header("Birakma Ayarlari")]
    [Tooltip("Kitap elden birakilinca oyuncunun onune dogru ne kadar ileri gider.")]
    [Min(0f)] public float dropForwardDistance = 0.35f;
    [Min(0f)] public float dropStackGap = 0.01f;
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

        // Ilk calisan sistemdeki gibi: X/Z hic degismez, kitaplar sadece
        // rightHandPoint'in local Y ekseninde yukari dogru cikar.
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

        // Kitabi elden ciktigi yukseklikte birakiyoruz. Onceki sistemdeki
        // "yukariya teleport etme" yok; kitap normal fizik ile serbest duser.
        // Hafif ileri atmak oyuncuya carpmasini ve eldeki yiginin altina
        // girmesini de azaltir.
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            forward.Normalize();
        else
            forward = transform.forward;

        worldPosition += forward * dropForwardDistance;

        book.transform.SetParent(null, true);
        book.transform.SetPositionAndRotation(worldPosition, worldRotation);
        book.transform.localScale = book.OriginalScale;

        // Oyuncuyla carpismayi gecici olarak kapat; kitap kendi fiziksel
        // dususunu ve diger kitaplarla carpisma/stack davranisini Physics'e birak.
        IgnorePlayerCollision(book, true);
        book.SetHeld(false);
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

    Bounds GetCombinedColliderBounds(Collider[] colliders)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
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

        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.zero);
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
