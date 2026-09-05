using UnityEngine;

/// <summary>
/// Mouse tekerlegiyle aktif kitap degistiginde, yeni aktif olan kitabi
/// once yiginin yanina cikartir, sonra yukariya tasir ve en sonda kendi
/// normal stack konumuna tam duz sekilde oturtur.
/// </summary>
public class HeldBookVisualSpacing : MonoBehaviour
{
    [Header("Kitap Gecis Goruntusu")]
    [Min(0f)]
    [Tooltip("Yeni aktif kitap yukselmeden once ne kadar yana acilsin.")]
    public float sideOffset = 0.09f;

    [Min(0.01f)]
    [Tooltip("Aktif kitap gecisinin toplam suresi.")]
    public float cycleDuration = 0.24f;

    [Range(0.1f, 0.8f)]
    [Tooltip("Toplam animasyonun ilk kaclik kismi yana acilma icin kullanilsin.")]
    public float sidePhase = 0.35f;

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
            if (player == null)
                continue;

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

        if (activeIndex != lastActiveIndex)
        {
            BeginActiveBookTransition(activeIndex);
            lastActiveIndex = activeIndex;
        }

        if (!animating)
            return;

        if (animatingBook == null || !IsBookStillHeld(animatingBook))
        {
            animating = false;
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
            // 1) Once yana acil: diger kitaplarin icinden gecmeden temiz bosluk ac.
            float sideT = SmoothStep01(t / sidePhase);
            local.x = Mathf.Lerp(startPosition.x, sideOffset, sideT);
            local.y = startPosition.y;
        }
        else
        {
            // 2) Yana cikmis halde yukari ilerle.
            // 3) Son kisimda X tekrar 0'a gelir ve kitap tam stack noktasina oturur.
            float moveT = SmoothStep01((t - sidePhase) / (1f - sidePhase));
            local.y = Mathf.Lerp(startPosition.y, targetY, moveT);
            local.x = Mathf.Lerp(sideOffset, 0f, moveT);
        }

        animatingBook.transform.SetParent(interaction.rightHandPoint, false);
        animatingBook.transform.localPosition = local;
        animatingBook.transform.localRotation = animatingBook.NativeRotation;
        animatingBook.transform.localScale = animatingBook.OriginalScale * interaction.heldScaleMultiplier;

        if (t >= 1f)
        {
            // Son karede kesin olarak normal stack konumuna oturt.
            animatingBook.transform.localPosition = new Vector3(0f, targetY, 0f);
            animatingBook.transform.localRotation = animatingBook.NativeRotation;
            animatingBook.transform.localScale = animatingBook.OriginalScale * interaction.heldScaleMultiplier;
            animating = false;
            animatingBook = null;
        }
    }

    bool IsBookStillHeld(BookItem book)
    {
        var books = interaction.HeldBooksList;
        for (int i = 0; i < books.Count; i++)
        {
            if (books[i] == book)
                return true;
        }

        return false;
    }

    void BeginActiveBookTransition(int activeIndex)
    {
        animating = false;
        animatingBook = null;

        if (activeIndex < 0 || activeIndex >= interaction.HeldBooksList.Count)
            return;

        BookItem book = interaction.HeldBooksList[activeIndex];
        if (book == null || book.transform.parent != interaction.rightHandPoint)
            return;

        animatingBook = book;
        startPosition = book.transform.localPosition;
        animationTime = 0f;
        animating = true;
    }

    static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
