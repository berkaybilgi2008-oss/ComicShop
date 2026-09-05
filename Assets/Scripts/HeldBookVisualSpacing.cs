using UnityEngine;

/// <summary>
/// Mouse tekerlegiyle aktif kitap degistiginde, yeni aktif olan kitabi
/// once saga ve one acartirir, sonra yukariya tasir ve en sonda kendi
/// normal stack konumuna tam duz sekilde oturtur.
/// </summary>
public class HeldBookVisualSpacing : MonoBehaviour
{
    [Header("Kitap Gecis Goruntusu")]
    [Min(0f)]
    [Tooltip("Yeni aktif kitap yukselmeden once ne kadar saga acilsin.")]
    public float sideOffset = 0.09f;

    [Min(0f)]
    [Tooltip("Kitap yukselirken kameraya dogru ne kadar one ciksin. Perspektifte kitaplarin ic ice gorunmesini azaltir.")]
    public float forwardOffset = 0.14f;

    [Min(0.01f)]
    [Tooltip("Aktif kitap gecisinin toplam suresi.")]
    public float cycleDuration = 0.24f;

    [Range(0.1f, 0.8f)]
    [Tooltip("Toplam animasyonun ilk kaclik kismi yana ve one acilma icin kullanilsin.")]
    public float sidePhase = 0.35f;

    [Range(0.5f, 0.95f)]
    [Tooltip("One/saga aciklik hangi noktaya kadar korunacak; son kisimda kitap normal konumuna oturur.")]
    public float settleStart = 0.78f;

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

        // Bu ozel gecis SADECE mouse tekerlegi aktif kitabi degistirdiginde calissin.
        // Kitap alma, raftan kitap alma, yere birakma veya rafa yerlestirme gibi
        // islemlerde activeHeldIndex degisse bile bu animasyon tetiklenmez.
        bool wheelChangedBook = Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;

        if (activeIndex != lastActiveIndex)
        {
            if (wheelChangedBook)
                BeginActiveBookTransition(activeIndex);
            else
                CancelTransitionWithoutMovingBook();

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
            // 1) Once saga + one acil. Kitap henuz yukselmez.
            float openT = SmoothStep01(t / sidePhase);
            local.x = Mathf.Lerp(startPosition.x, sideOffset, openT);
            local.y = startPosition.y;
            local.z = Mathf.Lerp(startPosition.z, forwardOffset, openT);
        }
        else if (t < settleStart)
        {
            // 2) Kitap acik konumunu koruyarak yukariya cikar.
            // One cikiklik burada korunur; perspektif sayesinde alttaki kitaplarla
            // ust uste binme hissi belirgin sekilde azalir.
            float moveT = SmoothStep01((t - sidePhase) / (settleStart - sidePhase));
            local.x = sideOffset;
            local.y = Mathf.Lerp(startPosition.y, targetY, moveT);
            local.z = forwardOffset;
        }
        else
        {
            // 3) Yukaridaki yerine geldikten sonra saga/one acikligi kapat.
            // Son noktada X/Z sifirlanir ve kitap tam stack konumuna oturur.
            float settleT = SmoothStep01((t - settleStart) / (1f - settleStart));
            local.x = Mathf.Lerp(sideOffset, 0f, settleT);
            local.y = Mathf.Lerp(targetY, targetY, settleT);
            local.z = Mathf.Lerp(forwardOffset, 0f, settleT);
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
