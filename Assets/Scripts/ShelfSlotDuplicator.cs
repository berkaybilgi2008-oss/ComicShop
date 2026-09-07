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

    [Header("Yon Duzeltme")]
    [Tooltip("Aciksa, komsu slotlardan olculen yonlerden rafa dogru gideni otomatik secer. " +
             "Boylece slotlar rafin ters tarafina cogalmaz.")]
    public bool autoChooseDirections = true;

    [Header("Isimlendirme")]
    public string namePrefix = "ShelfSlot";

    [ContextMenu("Gozleri Olustur")]
    public void GenerateGrid()
    {
        if (!CanEditGrid())
            return;

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

        if (autoChooseDirections)
            ChooseDirectionsTowardsShelf(parent, origin, ref colStep, ref rowStepVector);

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

#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(copy, "Raf gozlerini olustur");
#endif
                created++;
            }
        }

        MarkSceneDirty();

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

    /// <summary>
    /// Kaynak komsular bazen template'in solunda veya ustunde kalabiliyor. Eski kod
    /// bu vektoru oldugu gibi uzattigi icin tum izgara rafin disina dogru buyuyordu.
    /// Raf modelinin Renderer sinirlarina en iyi oturan dort yon kombinasyonunu secer.
    /// </summary>
    private void ChooseDirectionsTowardsShelf(
        Transform parent,
        Vector3 origin,
        ref Vector3 colStep,
        ref Vector3 rowStepVector)
    {
        if (!TryGetShelfBounds(parent, out Bounds shelfBounds))
            return;

        Vector3 originalColumn = colStep;
        Vector3 originalRow = rowStepVector;
        Vector3 bestColumn = originalColumn;
        Vector3 bestRow = originalRow;
        float bestScore = float.PositiveInfinity;

        int columnChoices = columns > 1 ? 2 : 1;
        int rowChoices = rows > 1 ? 2 : 1;

        for (int columnChoice = 0; columnChoice < columnChoices; columnChoice++)
        {
            float columnSign = columnChoice == 0 ? 1f : -1f;

            for (int rowChoice = 0; rowChoice < rowChoices; rowChoice++)
            {
                float rowSign = rowChoice == 0 ? 1f : -1f;
                Vector3 candidateColumn = originalColumn * columnSign;
                Vector3 candidateRow = originalRow * rowSign;
                float score = ScoreGridFit(
                    shelfBounds,
                    origin,
                    candidateColumn,
                    candidateRow);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestColumn = candidateColumn;
                    bestRow = candidateRow;
                }
            }
        }

        colStep = bestColumn;
        rowStepVector = bestRow;

        bool columnFlipped = Vector3.Dot(originalColumn, bestColumn) < 0f;
        bool rowFlipped = Vector3.Dot(originalRow, bestRow) < 0f;
        if (columnFlipped || rowFlipped)
        {
            Debug.Log($"ShelfSlotDuplicator ({name}): Izgaranin rafa dogru buyumesi icin " +
                      $"yon otomatik duzeltildi (sutun ters: {columnFlipped}, satir ters: {rowFlipped}).");
        }
    }

    private bool TryGetShelfBounds(Transform parent, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;

        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            // Slot'un kendisinde ileride bir debug renderer olursa raf olcusunu bozmasin.
            if (renderer.GetComponentInParent<ShelfSlot>() != null)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private float ScoreGridFit(
        Bounds shelfBounds,
        Vector3 origin,
        Vector3 colStep,
        Vector3 rowStepVector)
    {
        Vector3 extents = shelfBounds.extents;
        extents.x = Mathf.Max(extents.x, 0.01f);
        extents.y = Mathf.Max(extents.y, 0.01f);
        extents.z = Mathf.Max(extents.z, 0.01f);

        float score = 0f;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 point = origin + colStep * c + rowStepVector * r;
                Vector3 normalized = new Vector3(
                    (point.x - shelfBounds.center.x) / extents.x,
                    (point.y - shelfBounds.center.y) / extents.y,
                    (point.z - shelfBounds.center.z) / extents.z);

                // Once raf sinirlarinin disina cikmayi agir cezalandir, esitlikte
                // raf merkezine daha yakin kalan izgara kazansin.
                float outsideX = Mathf.Max(0f, Mathf.Abs(normalized.x) - 1.05f);
                float outsideY = Mathf.Max(0f, Mathf.Abs(normalized.y) - 1.05f);
                float outsideZ = Mathf.Max(0f, Mathf.Abs(normalized.z) - 1.05f);
                float outsidePenalty = outsideX * outsideX
                                     + outsideY * outsideY
                                     + outsideZ * outsideZ;

                score += outsidePenalty * 1000f + normalized.sqrMagnitude;
            }
        }

        return score;
    }

    [ContextMenu("Olusturulanlari Sil (template kalir)")]
    public void ClearGenerated()
    {
        if (!CanEditGrid())
            return;

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
            UnityEditor.Undo.DestroyObjectImmediate(child);
#else
            Destroy(child);
#endif
            removed++;
        }

        MarkSceneDirty();

        Debug.Log($"ShelfSlotDuplicator ({name}): {removed} uretilmis raf gozu silindi.");
    }

    /// <summary>
    /// Ayarlar veya kaynak slotlar degistiginde eski uretilen gozleri temizleyip
    /// guncel olculerle tekrar kurar.
    /// </summary>
    [ContextMenu("Gozleri Yeniden Olustur")]
    public void RegenerateGrid()
    {
        if (!CanEditGrid())
            return;

#if UNITY_EDITOR
        UnityEditor.Undo.SetCurrentGroupName("Raf gozlerini yeniden olustur");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
#endif

        ClearGenerated();
        GenerateGrid();

#if UNITY_EDITOR
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
#endif
    }

    /// <summary>Inspector'un anlasilir bir hata gosterebilmesi icin ortak kontrol.</summary>
    public bool TryGetValidationError(out string error)
    {
        if (templateSlot == null)
        {
            error = "Template Slot atanmamis.";
            return true;
        }

        Transform parent = templateSlot.transform.parent;
        if (parent == null)
        {
            error = "Template Slot bir parent altinda olmali.";
            return true;
        }

        if (columnNeighbor != null && columnNeighbor.transform.parent != parent)
        {
            error = "Column Neighbor, Template Slot ile ayni parent altinda olmali.";
            return true;
        }

        if (rowNeighbor != null && rowNeighbor.transform.parent != parent)
        {
            error = "Row Neighbor, Template Slot ile ayni parent altinda olmali.";
            return true;
        }

        error = null;
        return false;
    }

    private bool CanEditGrid()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning($"ShelfSlotDuplicator ({name}): Bu arac Edit Mode'da kullanilmali. " +
                             "Play Mode'da uretilen nesneler oyun durunca kaybolur.");
            return false;
        }

        if (TryGetValidationError(out string error))
        {
            Debug.LogError($"ShelfSlotDuplicator ({name}): {error}");
            return false;
        }

        return true;
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
