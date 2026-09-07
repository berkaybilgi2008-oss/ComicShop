using UnityEngine;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private void Awake()
    {
        BuildBlackEdgeLines();
    }

    public static void ApplyToBook(GameObject book)
    {
        if (book == null) return;
        BookToonEffect effect = book.GetComponent<BookToonEffect>();
        if (effect == null)
        {
            book.AddComponent<BookToonEffect>();
            return;
        }
        effect.BuildBlackEdgeLines();
    }

    private void BuildBlackEdgeLines()
    {
        // Kitabin mevcut material/texture'i degistirilmez.
        // Sadece gercek mesh kenarlarina siyah geometri cizgileri eklenir.
        BookEdgeLines.ApplyToBook(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToExistingBooks()
    {
        BookItem[] books = Object.FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        foreach (BookItem book in books)
            ApplyToBook(book.gameObject);
    }
}
