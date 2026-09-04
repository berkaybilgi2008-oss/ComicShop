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
    [Tooltip("Kitabin elden birakildiginda ileri dogru kazanacagi hiz.")]
    [Min(0f)] public float dropForwardForce = 2.5f;
    [Tooltip("Kitabin elden birakildiginda yukari dogru kazanacagi hiz.")]
    [Min(0f)] public float dropUpwardForce = 0.75f;
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
    public int ActiveHeldIndex => activeHeldIndex;
    public BookItem ActiveHeldBook => heldBooks.Count == 0 ? null : heldBooks[Mathf.Clamp(activeHeldIndex, 0, heldBooks.Count - 1)];

    private int activeHeldIndex = -1;
    private BookItem lookedBook;
    private ShelfSlot lookedSlot;
    private bool isBookAnimating;

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

        if (isBookAnimating)
            return;

        if (Input.GetKeyDown(pickupKey))
            HandlePickupPress();

        if (Input.GetKeyDown(dropKey))
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
            PlaceActiveBook();
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
        Vector3 targetLocalPosition = GetHeldLocalPosition(heldBooks.Count - 1);
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

        // Asagidan yukariya giderken index +1, yukaridan asagiya giderken -1.
        // Dongu sonunda tekrar baslangica doner.
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

    Vector3 GetHeldLocalPosition(int index)
    {
        return new Vector3(0f, index * stackSpacing, 0f);
    }

    void PlaceActiveBook()
    {
        BookItem book = ActiveHeldBook;
        if (lookedSlot == null || book == null)
            return;

        if (!lookedSlot.Matches(book))
            return;

        StartCoroutine(PlaceActiveBookAnimated(book, lookedSlot));
    }

    IEnumerator PlaceActiveBookAnimated(BookItem book, ShelfSlot slot)
    {
        if (book == null || slot == null || rightHandPoint == null)
            yield break;

        isBookAnimating = true;

        Transform point = slot.GetNextPlacementPoint();
        if (point == null)
        {
            isBookAnimating = false;
            yield break;
        }

        Vector3 startPosition = book.transform.position;
        Quaternion startRotation = book.transform.rotation;
        Vector3 startScale = book.transform.lossyScale;
        Vector3 targetPosition = point.position;
        Quaternion targetRotation = point.rotation * book.NativeRotation;
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
