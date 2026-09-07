using UnityEngine;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static Shader toonShader;

    private void Awake()
    {
        // Kitabin kaplama rengini degistirmiyoruz. Bu script sadece mevcut
        // materyali oldugu gibi birakir ve fiziksel mesh kenar cizgilerini kurar.
        BuildBlackEdgeLines();
    }

    public static void ApplyToBook(GameObject book)
    {
        if (book == null)
            return;

        BookToonEffect effect = book.GetComponent<BookToonEffect>();
        if (effect == null)
        {
            effect = book.AddComponent<BookToonEffect>();
            return;
        }

        effect.BuildBlackEdgeLines();
    }

    private void BuildBlackEdgeLines()
    {
        // BookToon shader artik kitap materyalinin rengini zorlamiyor.
        // Asil comic gorunumu mesh'in gercek sinir/katlanma kenarlarindan geliyor.
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
