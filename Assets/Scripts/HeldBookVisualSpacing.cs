using UnityEngine;

/// <summary>
/// Elde tasinan kitaplarin goruntusunu duzenler: aktif kitap gecisinde
/// once acilir, sonra yukselir ve en sonunda stack konumuna oturur.
/// Elde kitaplar kameraya kapak yerine yan donuk tutulur; boylece kitap
/// sirti (yazilarin oldugu kisim) kameraya bakar.
/// </summary>
public class HeldBookVisualSpacing : MonoBehaviour
{
    [Header("Kitap Gecis Goruntusu")]
    [Min(0f)] public float sideOffset = 0.09f;
    [Min(0f)] public float forwardOffset = 0.14f;
    [Min(0.01f)] public float cycleDuration = 0.24f;
    [Range(0.1f, 0.8f)] public float sidePhase = 0.35f;
    [Range(0.5f, 0.95f)] public float settleStart = 0.78f;

    [Header("Elde Kitap Yonu")]
    [Tooltip("Kitabi elde 90 derece yan dondurur. Pozitif deger sirti kameraya getirir.")]
    public float spineViewRotation = 90f;

    private PlayerInteraction interaction;
    private int lastActiveIndex = -1;
    private BookItem animatingBook;
    private Vector3 startPosition;
    private float animationTime;
    private bool animating;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallOnPlayers()
    {
        PlayerInteraction[] players = Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
        foreach (PlayerInteraction player in players)
        {
            if (player == null) continue;
            if (player.GetComponent<HeldBookVisualSpacing>() == null)
                player.gameObject.AddComponent<HeldBookVisualSpacing>();
        }
    }

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        lastActiveIndex = interaction != null ? interaction.ActiveHeldIndex : -1;
    }

    void LateUpdate()
    {
        if (interaction == null || interaction.rightHandPoint == null)
            return;

        int activeIndex = interaction.ActiveHeldIndex;
        bool wheelChangedBook = Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;

        if (activeIndex != lastActiveIndex)
        {
            if (wheelChangedBook)
                BeginActiveBookTransition(activeIndex);
            else
                CancelTransitionWithoutMovingBook();
            lastActiveIndex = activeIndex;
        }

        // Normal elde tutma durumunda tum kitaplari yan dondur.
        // Atis/yerlestirme sirasinda kitap artik rightHandPoint altinda olmadigi icin etkilenmez.
        if (!animating)
        {
            ApplySpineViewToHeldBooks();
            return;
        }

        if (animatingBook == null || !IsBookStillHeld(animatingBook))
        {
            animating = false;
            ApplySpineViewToHeldBooks();
            return;
        }

        animationTime += Time.deltaTime;
        float duration = Mathf.Max(0.01f, cycleDuration);
        float t = Mathf.Clamp01(animationTime / duration);

        int count = interaction.HeldBooksList.Count;
        int targetDisplayIndex = Mathf.Max(0, count - 1);
        float targetY = targetDisplayIndex * interaction.stackSpacing;
        Vector3 local = startPosition;

        if (t < sidePhase)
        {
            float openT = SmoothStep01(t / sidePhase);
            local.x = Mathf.Lerp(startPosition.x, sideOffset, openT);
            local.y = startPosition.y;
            local.z = Mathf.Lerp(startPosition.z, forwardOffset, openT);
        }
        else if (t < settleStart)
        {
            float moveT = SmoothStep01((t - sidePhase) / (settleStart - sidePhase));
            local.x = sideOffset;
            local.y = Mathf.Lerp(startPosition.y, targetY, moveT);
            local.z = forwardOffset;
        }
        else
        {
            float settleT = SmoothStep01((t - settleStart) / (1f - settleStart));
            local.x = Mathf.Lerp(sideOffset, 0f, settleT);
            local.y = targetY;
            local.z = Mathf.Lerp(forwardOffset, 0f, settleT);
        }

        animatingBook.transform.SetParent(interaction.rightHandPoint, false);
        animatingBook.transform.localPosition = local;
        animatingBook.transform.localRotation = GetSpineViewRotation(animatingBook);
        animatingBook.transform.localScale = animatingBook.OriginalScale * interaction.heldScaleMultiplier;

        if (t >= 1f)
        {
            animatingBook.transform.localPosition = new Vector3(0f, targetY, 0f);
            animatingBook.transform.localRotation = GetSpineViewRotation(animatingBook);
            animatingBook.transform.localScale = animatingBook.OriginalScale * interaction.heldScaleMultiplier;
            animating = false;
            animatingBook = null;
        }
    }

    void ApplySpineViewToHeldBooks()
    {
        var books = interaction.HeldBooksList;
        for (int i = 0; i < books.Count; i++)
        {
            BookItem book = books[i];
            if (book == null || book.transform.parent != interaction.rightHandPoint)
                continue;
            book.transform.localRotation = GetSpineViewRotation(book);
        }
    }

    Quaternion GetSpineViewRotation(BookItem book)
    {
        return book.NativeRotation * Quaternion.AngleAxis(spineViewRotation, Vector3.forward);
    }

    bool IsBookStillHeld(BookItem book)
    {
        var books = interaction.HeldBooksList;
        for (int i = 0; i < books.Count; i++)
            if (books[i] == book) return true;
        return false;
    }

    void BeginActiveBookTransition(int activeIndex)
    {
        animating = false;
        animatingBook = null;
        if (activeIndex < 0 || activeIndex >= interaction.HeldBooksList.Count) return;

        BookItem book = interaction.HeldBooksList[activeIndex];
        if (book == null || book.transform.parent != interaction.rightHandPoint) return;

        animatingBook = book;
        startPosition = book.transform.localPosition;
        animationTime = 0f;
        animating = true;
    }

    void CancelTransitionWithoutMovingBook()
    {
        animating = false;
        animatingBook = null;
    }

    static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
