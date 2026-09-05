using UnityEngine;

/// <summary>
/// Elde tasinan kitaplardan aktif olanin diger kitaplarin icine girmesini
/// engellemek icin onu hafifce yana acik tutar.
/// PlayerInteraction mevcut dikey stack animasyonunu yonetmeye devam eder;
/// bu script sadece aktif kitabin X konumunu duzeltir.
/// </summary>
public class HeldBookVisualSpacing : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("Mouse tekerlegiyle secilen aktif kitabin diger kitaplardan yana acilma mesafesi.")]
    public float sideOffset = 0.06f;

    private PlayerInteraction interaction;

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
    }

    void LateUpdate()
    {
        if (interaction == null || interaction.rightHandPoint == null)
            return;

        int activeIndex = interaction.ActiveHeldIndex;
        var books = interaction.HeldBooksList;

        for (int i = 0; i < books.Count; i++)
        {
            BookItem book = books[i];
            if (book == null || book.transform.parent != interaction.rightHandPoint)
                continue;

            Vector3 local = book.transform.localPosition;
            local.x = i == activeIndex ? sideOffset : 0f;
            book.transform.localPosition = local;
        }
    }
}
