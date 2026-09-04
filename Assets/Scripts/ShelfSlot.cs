using UnityEngine;

public class ShelfSlot : MonoBehaviour
{
    [Header("Marka")]
    [Min(0)] public int brandID;

    [Header("Kapasite")]
    [Min(1)] public int capacity = 10;

    [Header("Kitap Yerleri")]
    [Tooltip("Bu bolmedeki kitaplarin konulacagi noktalar. 10 nokta ayarla.")]
    public Transform[] placementPoints = new Transform[10];

    private BookItem[] placedBooks;
    private int ownerBookID = -1;

    public int FilledCount { get; private set; }
    public int OwnerBookID => ownerBookID;
    public bool IsAvailable => FilledCount < capacity;
    public bool IsClaimed => ownerBookID >= 0;

    void Awake()
    {
        capacity = Mathf.Max(1, capacity);
        placedBooks = new BookItem[capacity];
    }

    public bool Matches(BookItem book)
    {
        if (book == null || book.brandID != brandID || !IsAvailable)
            return false;

        return !IsClaimed || book.bookID == ownerBookID;
    }

    public Transform GetNextPlacementPoint()
    {
        int index = FindFreeIndex();
        if (index < 0 || placementPoints == null || index >= placementPoints.Length)
            return null;

        return placementPoints[index];
    }

    public bool PlaceBook(BookItem book)
    {
        if (!Matches(book))
            return false;

        int index = FindFreeIndex();
        if (index < 0)
            return false;

        if (placementPoints == null || index >= placementPoints.Length || placementPoints[index] == null)
        {
            Debug.LogWarning($"ShelfSlot '{name}': {index}. kitap noktasi ayarlanmamis.");
            return false;
        }

        if (!IsClaimed)
            ownerBookID = book.bookID;

        Transform point = placementPoints[index];

        book.transform.SetParent(null, true);
        book.transform.position = point.position;
        book.transform.rotation = point.rotation * book.NativeRotation;
        book.transform.localScale = book.OriginalScale;
        book.SetHeld(false);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        placedBooks[index] = book;
        FilledCount++;
        book.currentSlot = this;
        GameStats.RegisterPlacement(book.bookID);
        return true;
    }

    public BookItem TakeLastBook()
    {
        if (placedBooks == null || FilledCount <= 0)
            return null;

        // Rafdan her zaman en son konan kitabi al: FIFO yerine LIFO.
        for (int i = placedBooks.Length - 1; i >= 0; i--)
        {
            BookItem book = placedBooks[i];
            if (book == null)
                continue;

            placedBooks[i] = null;
            FilledCount = Mathf.Max(0, FilledCount - 1);
            GameStats.UnregisterPlacement(book.bookID);
            book.currentSlot = null;

            if (FilledCount == 0)
                ownerBookID = -1;

            return book;
        }

        return null;
    }

    public BookItem TakeFirstBook()
    {
        return TakeLastBook();
    }

    public void RemoveBook(BookItem book)
    {
        if (book == null || placedBooks == null)
            return;

        int index = System.Array.IndexOf(placedBooks, book);
        if (index < 0)
            return;

        placedBooks[index] = null;
        FilledCount = Mathf.Max(0, FilledCount - 1);
        GameStats.UnregisterPlacement(book.bookID);
        book.currentSlot = null;

        if (FilledCount == 0)
            ownerBookID = -1;
    }

    private int FindFreeIndex()
    {
        if (placedBooks == null)
            return -1;

        for (int i = 0; i < placedBooks.Length; i++)
        {
            if (placedBooks[i] == null)
                return i;
        }

        return -1;
    }
}
