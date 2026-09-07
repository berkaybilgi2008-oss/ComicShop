using UnityEngine;

/// <summary>
/// Elle ayarladigin BIR raf gozunu (template) bir kitapligin butun gozlerine cogaltir.
///
/// KULLANIM:
/// 1) Bu script'i kitapligin (orn. RAF) uzerine ekle.
/// 2) Template Slot   -> elle ayarladigin calisan slot (orn. ShelfSlot_01)
/// 3) Column Neighbor -> template'in SAGINDAKI goze koydugun ikinci slot (opsiyonel)
///    Row Neighbor    -> template'in ALTINDAKI goze koydugun ucuncu slot (opsiyonel)
///    Bunlari verirsen aradaki mesafeyi kendisi olcer. Vermezsen asagidaki
///    Column Step / Row Step degerlerini elle yazarsin.
/// 4) Columns / Rows -> kitapligin kac sutun ve kac satir gozu var.
/// 5) Component basligindaki ⋮ menusunden "Gozleri Olustur" de.
///
/// Uretilen slotlar template ile AYNI PARENT altina, ayni ayarlarla, ayni
/// rotasyon ve olcekle konur. Template'in kendisine dokunulmaz.
/// </summary>
public class ShelfSlotDuplicator : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Elle ayarladigin, duzgun calisan raf gozu.")]
    public ShelfSlot templateSlot;

    [Header("Mesafeyi Otomatik Olc (onerilen)")]
    [Tooltip("Template'in bir SAGINDAKI goze koydugun slot. Doldurursan sutun mesafesi otomatik olculur.")]
    public ShelfSlot columnNeighbor;

    [Tooltip("Template'in bir ALTINDAKI goze koydugun slot. Doldurursan satir mesafesi otomatik olculur.")]
    public ShelfSlot rowNeighbor;

    [Header("Izgara")]
    [Tooltip("Kitapligin yatayda kac gozu var (template dahil).")]
    [Min(1)] public int columns = 5;

    [Tooltip("Kitapligin dikeyde kac gozu var (template dahil).")]
    [Min(1)] public int rows = 3;

    [Header("Mesafe (Neighbor bosken kullanilir, DUNYA birimi/metre)")]
    [Tooltip("Bir gozden sagindaki goze gecis vektoru.")]
    public Vector3 columnStep = new Vector3(0.3f, 0f, 0f);

    [Tooltip("Bir gozden altindaki goze gecis vektoru.")]
    public Vector3 rowStep = new Vector3(0f, -0.55f, 0f);

    [Header("Isimlendirme")]
    public string namePrefix = "ShelfSlot";

    [ContextMenu("Gozleri Olustur")]
    public void GenerateGrid()
    {
        if (templateSlot == null)
        {
            Debug.LogError($"ShelfSlotDuplicator ({name}): Template Slot atanmamis.");
            return;
        }

        Vector3 colStep = columnNeighbor != null
            ? columnNeighbor.transform.position - templateSlot.transform.position
            : columnStep;

        Vector3 rowStepVector = rowNeighbor != null
            ? rowNeighbor.transform.position - templateSlot.transform.position
            : rowStep;

        if (columns > 1 && colStep.magnitude < 0.0001f)
        {
            Debug.LogError($"ShelfSlotDuplicator ({name}): Sutun mesafesi sifir. " +
                           $"Column Neighbor ata ya da Column Step gir.");
            return;
        }

        if (rows > 1 && rowStepVector.magnitude < 0.0001f)
        {
            Debug.LogError($"ShelfSlotDuplicator ({name}): Satir mesafesi sifir. " +
                           $"Row Neighbor ata ya da Row Step gir.");
            return;
        }

        Transform parent = templateSlot.transform.parent;
        Vector3 origin = templateSlot.transform.position;
        Quaternion rotation = templateSlot.transform.rotation;

        int created = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                // Template zaten (0,0) konumunda duruyor, onu tekrar uretme.
                if (r == 0 && c == 0)
                    continue;

                Vector3 position = origin + colStep * c + rowStepVector * r;

                // Elle koydugun komsu slotlarin uzerine ikinci bir kopya atmayalim.
                if (IsOccupied(parent, position))
                    continue;

                GameObject copy = Instantiate(templateSlot.gameObject, parent);
                copy.transform.SetPositionAndRotation(position, rotation);
                copy.transform.localScale = templateSlot.transform.localScale;
                copy.name = $"{namePrefix}_R{r + 1}C{c + 1}";
                created++;
            }
        }

        Debug.Log($"ShelfSlotDuplicator ({name}): {created} yeni raf gozu olusturuldu " +
                  $"({rows} satir x {columns} sutun). " +
                  $"Sutun mesafesi: {colStep.magnitude:0.000} m, satir mesafesi: {rowStepVector.magnitude:0.000} m.");
    }

    private bool IsOccupied(Transform parent, Vector3 position)
    {
        if (parent == null)
            return false;

        ShelfSlot[] existing = parent.GetComponentsInChildren<ShelfSlot>();
        foreach (ShelfSlot slot in existing)
        {
            if (slot == null)
                continue;

            if (Vector3.Distance(slot.transform.position, position) < 0.02f)
                return true;
        }

        return false;
    }

    [ContextMenu("Olusturulanlari Sil (template kalir)")]
    public void ClearGenerated()
    {
        if (templateSlot == null)
        {
            Debug.LogError($"ShelfSlotDuplicator ({name}): Template Slot atanmamis, " +
                           $"neyi koruyacagimi bilmiyorum. Silme iptal edildi.");
            return;
        }

        Transform parent = templateSlot.transform.parent;
        if (parent == null)
            return;

        int removed = 0;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

            if (child == templateSlot.gameObject)
                continue;

            if (columnNeighbor != null && child == columnNeighbor.gameObject)
                continue;

            if (rowNeighbor != null && child == rowNeighbor.gameObject)
                continue;

            if (!child.name.StartsWith(namePrefix + "_R"))
                continue;

#if UNITY_EDITOR
            DestroyImmediate(child);
#else
            Destroy(child);
#endif
            removed++;
        }

        Debug.Log($"ShelfSlotDuplicator ({name}): {removed} uretilmis raf gozu silindi.");
    }
}