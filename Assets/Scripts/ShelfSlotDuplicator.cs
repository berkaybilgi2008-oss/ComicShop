using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Elle ayarladigin BIR raf gozunu (template) bir kitapligin butun gozlerine cogaltir.
///
/// KULLANIM:
/// 1) Bu script'i kitapligin (orn. RAF) uzerine ekle.
/// 2) Template Slot   -> elle ayarladigin calisan slot.
/// 3) Column Neighbor -> template'in bir SAGINDAKI goze koydugun slot.
///    Row Neighbor    -> template'in bir ALTINDAKI goze koydugun slot.
///    Bunlari verirsen aradaki mesafeyi kendisi olcer.
/// 4) Template Column / Template Row -> template izgaranin KACINCI hucresi.
///    Sol ust kose ise 1 / 1 birak. Ortadaki bir gozu template yaptiysan
///    gercek yerini yaz, yoksa izgara yanlis yone buyur.
/// 5) Columns / Rows -> kitapligin kac sutun ve kac satir gozu var.
/// 6) ⋮ menusu -> once "0) Olcumu Raporla", sayilar mantikliysa "1) Gozleri Olustur".
///
/// NOT (v2): Eski surum konumlari DUNYA uzayinda hesapliyordu. RAF gibi 200x/215x
/// olceklenmis bir parent altinda dunya <-> local donusumu her adimda hata biriktiriyor
/// ve kitapligi kimildatinca/dondurunce izgara kayiyordu. Bu surum her seyi parent'in
/// LOCAL uzayinda yapar; kitapligi tasisan da dondursen de izgara bozulmaz.
/// </summary>
public class ShelfSlotDuplicator : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Elle ayarladigin, duzgun calisan raf gozu.")]
    public ShelfSlot templateSlot;

    [Header("Mesafeyi Otomatik Olc (onerilen)")]
    [Tooltip("Template'in bir SAGINDAKI goze koydugun slot. Template ile AYNI parent altinda olmali.")]
    public ShelfSlot columnNeighbor;

    [Tooltip("Template'in bir ALTINDAKI goze koydugun slot. Template ile AYNI parent altinda olmali.")]
    public ShelfSlot rowNeighbor;

    [Header("Izgara")]
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows = 3;

    [Header("Sablon Izgaranin Neresinde? (1 tabanli)")]
    [Tooltip("Template sol ustteki gozse 1 birak. 3. sutundaki bir gozu template " +
             "yaptiysan 3 yaz -- yoksa izgara yanlis yone dogru buyur.")]
    [Min(1)] public int templateColumn = 1;

    [Tooltip("Template en ustteki satirdaysa 1 birak.")]
    [Min(1)] public int templateRow = 1;

    [Header("Mesafe (Neighbor bosken kullanilir, DUNYA birimi/metre)")]
    public Vector3 columnStep = new Vector3(0.3f, 0f, 0f);
    public Vector3 rowStep = new Vector3(0f, -0.55f, 0f);

    [Header("Uretim Ayarlari")]
    public string namePrefix = "ShelfSlot";

    [Tooltip("Template'in izgaradaki yerini ve komsu yonlerini RAF modelinin " +
             "sinirlarina gore otomatik bulur. Slotlar ters tarafa cikiyorsa acik birak.")]
    public bool autoFitGridToShelf = true;

    [Tooltip("Uretmeden once eski uretilmis gozleri sil. Kapaliyken tekrar " +
             "calistirirsan eski ve yeni izgara ust uste biner.")]
    public bool clearBeforeGenerate = true;

    [Tooltip("Bir hucrede zaten slot var mi kontrolunun toleransi -- hucre " +
             "adiminin orani olarak. 0.25 = adimin dortte biri.")]
    [Range(0.05f, 0.49f)] public float occupancyTolerance = 0.25f;

    private int resolvedTemplateColumn = 1;
    private int resolvedTemplateRow = 1;

    // ==================================================================
    // 0) RAPOR
    // ==================================================================

    [ContextMenu("0) Olcumu Raporla (hicbir sey uretmez)")]
    public void ReportPlan()
    {
        if (!Hazirla(out Transform parent, out Vector3 originLocal,
                     out Vector3 colLocal, out Vector3 rowLocal))
            return;

        float tol = Tolerans(colLocal, rowLocal);

        string log =
            $"[ShelfSlotDuplicator] '{name}' PLAN\n" +
            $"  Parent           : {(parent != null ? parent.name : "(sahne koku)")}\n" +
            $"  Parent lossyScale: {Fmt(parent != null ? parent.lossyScale : Vector3.one)}\n" +
            $"  Template         : {templateSlot.name}  (izgarada R{resolvedTemplateRow}C{resolvedTemplateColumn})\n" +
            $"  Template local   : {Fmt(originLocal)}\n" +
            $"  Sutun adimi      : local {Fmt(colLocal)} -> dunyada {Dunya(parent, colLocal):0.000} m " +
            $"({(columnNeighbor != null ? "olculdu: " + columnNeighbor.name : "elle girildi")})\n" +
            $"  Satir adimi      : local {Fmt(rowLocal)} -> dunyada {Dunya(parent, rowLocal):0.000} m " +
            $"({(rowNeighbor != null ? "olculdu: " + rowNeighbor.name : "elle girildi")})\n" +
            $"  Izgara           : {rows} satir x {columns} sutun = {rows * columns} goz\n" +
            $"  Dolu-hucre toleransi: {tol:0.######} (local)\n";

        int dolu = 0, bos = 0;
        for (int r = 1; r <= rows; r++)
        {
            for (int c = 1; c <= columns; c++)
            {
                Vector3 hedef = HucreLocal(originLocal, colLocal, rowLocal, r, c);
                if (BuluHucreDolu(parent, hedef, tol, out ShelfSlot mevcut))
                {
                    dolu++;
                    if (dolu <= 6)
                        log += $"    R{r}C{c}: zaten dolu -> {mevcut.name}\n";
                }
                else
                {
                    bos++;
                }
            }
        }

        log += $"  SONUC: {bos} yeni goz uretilecek, {dolu} hucre zaten dolu (atlanacak).";
        Debug.Log(log, this);
    }

    // ==================================================================
    // 1) URET
    // ==================================================================

    [ContextMenu("1) Gozleri Olustur")]
    public void GenerateGrid()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogError("[ShelfSlotDuplicator] Play modunda calistirma.", this);
            return;
        }

        if (!Hazirla(out Transform parent, out Vector3 originLocal,
                     out Vector3 colLocal, out Vector3 rowLocal))
            return;

        if (clearBeforeGenerate)
            ClearGenerated();

        float tol = Tolerans(colLocal, rowLocal);

        Quaternion rotLocal = templateSlot.transform.localRotation;
        Vector3 scaleLocal = templateSlot.transform.localScale;

        int created = 0;
        int skipped = 0;

        for (int r = 1; r <= rows; r++)
        {
            for (int c = 1; c <= columns; c++)
            {
                Vector3 hedef = HucreLocal(originLocal, colLocal, rowLocal, r, c);

                if (BuluHucreDolu(parent, hedef, tol, out _))
                {
                    skipped++;
                    continue;
                }

                GameObject copy = Instantiate(templateSlot.gameObject);
                Undo.RegisterCreatedObjectUndo(copy, "Raf gozu olustur");

                // Dunya uzayina hic ugramadan, dogrudan local degerleri yaz.
                copy.transform.SetParent(parent, false);
                copy.transform.localPosition = hedef;
                copy.transform.localRotation = rotLocal;
                copy.transform.localScale = scaleLocal;

                copy.name = $"{namePrefix}_R{r}C{c}";
                created++;
            }
        }

        MarkDirty();

        Debug.Log($"[ShelfSlotDuplicator] '{name}': {created} yeni raf gozu olusturuldu, " +
                  $"{skipped} hucre zaten doluydu ({rows} satir x {columns} sutun).\n" +
                  $"  Sutun adimi: {Dunya(parent, colLocal):0.000} m, " +
                  $"satir adimi: {Dunya(parent, rowLocal):0.000} m.", this);
#else
        Debug.LogError("[ShelfSlotDuplicator] Bu islem sadece Editor'de calisir.");
#endif
    }

    // ==================================================================
    // 2) TEMIZLE
    // ==================================================================

    [ContextMenu("2) Olusturulanlari Sil (template ve komsular kalir)")]
    public void ClearGenerated()
    {
#if UNITY_EDITOR
        if (templateSlot == null)
        {
            Debug.LogError("[ShelfSlotDuplicator] Template Slot atanmamis, neyi " +
                           "koruyacagimi bilmiyorum. Silme iptal edildi.", this);
            return;
        }

        Transform parent = templateSlot.transform.parent;
        if (parent == null)
            return;

        int removed = 0;
        string onEk = namePrefix + "_R";

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

            if (child == templateSlot.gameObject) continue;
            if (columnNeighbor != null && child == columnNeighbor.gameObject) continue;
            if (rowNeighbor != null && child == rowNeighbor.gameObject) continue;
            if (!child.name.StartsWith(onEk)) continue;

            Undo.DestroyObjectImmediate(child);
            removed++;
        }

        MarkDirty();
        Debug.Log($"[ShelfSlotDuplicator] '{name}': {removed} uretilmis raf gozu silindi.", this);
#endif
    }

    // ==================================================================
    // Hesap
    // ==================================================================

    /// <summary>Ortak dogrulama + olcum. Bir sey ters giderse false doner ve sebebini basar.</summary>
    private bool Hazirla(out Transform parent, out Vector3 originLocal,
                         out Vector3 colLocal, out Vector3 rowLocal)
    {
        parent = null;
        originLocal = Vector3.zero;
        colLocal = Vector3.zero;
        rowLocal = Vector3.zero;

        if (templateSlot == null)
        {
            Debug.LogError("[ShelfSlotDuplicator] Template Slot atanmamis.", this);
            return false;
        }

        parent = templateSlot.transform.parent;
        originLocal = templateSlot.transform.localPosition;

        // Komsular ayni parent altinda degilse local fark anlamsiz olur.
        if (!AyniParent(columnNeighbor, parent, "Column Neighbor")) return false;
        if (!AyniParent(rowNeighbor, parent, "Row Neighbor")) return false;

        colLocal = columnNeighbor != null
            ? columnNeighbor.transform.localPosition - originLocal
            : DunyadanLocale(parent, columnStep);

        rowLocal = rowNeighbor != null
            ? rowNeighbor.transform.localPosition - originLocal
            : DunyadanLocale(parent, rowStep);

        if (columns > 1 && colLocal.sqrMagnitude < 1e-12f)
        {
            Debug.LogError("[ShelfSlotDuplicator] Sutun mesafesi sifir. Column Neighbor " +
                           "ata ya da Column Step gir.", this);
            return false;
        }

        if (rows > 1 && rowLocal.sqrMagnitude < 1e-12f)
        {
            Debug.LogError("[ShelfSlotDuplicator] Satir mesafesi sifir. Row Neighbor " +
                           "ata ya da Row Step gir.", this);
            return false;
        }

        if (templateColumn < 1 || templateRow < 1 ||
            templateColumn > columns || templateRow > rows)
        {
            Debug.LogError($"[ShelfSlotDuplicator] Template izgaranin disinda: " +
                           $"R{templateRow}C{templateColumn} ama izgara {rows}x{columns}.", this);
            return false;
        }

        resolvedTemplateColumn = templateColumn;
        resolvedTemplateRow = templateRow;

        if (autoFitGridToShelf)
            AutoFitGrid(parent, originLocal, ref colLocal, ref rowLocal);

        return true;
    }

    private bool AyniParent(ShelfSlot komsu, Transform parent, string alanAdi)
    {
        if (komsu == null || komsu.transform.parent == parent)
            return true;

        Debug.LogError($"[ShelfSlotDuplicator] '{alanAdi}' ({komsu.name}) template ile AYNI " +
                       $"parent altinda degil. Mesafe olcumu anlamsiz olurdu, islem iptal. " +
                       $"Ikisi de '{(parent != null ? parent.name : "sahne koku")}' altinda olmali.", this);
        return false;
    }

    private Vector3 HucreLocal(Vector3 originLocal, Vector3 colLocal, Vector3 rowLocal, int row, int col)
    {
        return originLocal
             + colLocal * (col - resolvedTemplateColumn)
             + rowLocal * (row - resolvedTemplateRow);
    }

    /// <summary>
    /// Template'in hangi hucre oldugunu ve iki eksenin isaretini otomatik dener.
    /// RAF Renderer sinirlarinin disina en az tasan, merkezine en iyi oturan
    /// kombinasyon kazanir. Boylece kullanici komsuyu ters tarafta secse bile
    /// izgara rafin disina dogru cogalmaz.
    /// </summary>
    private void AutoFitGrid(
        Transform parent,
        Vector3 originLocal,
        ref Vector3 colLocal,
        ref Vector3 rowLocal)
    {
        if (!TryGetShelfBounds(parent, out Bounds shelfBounds))
            return;

        Vector3 originalColumn = colLocal;
        Vector3 originalRow = rowLocal;
        Vector3 bestColumn = originalColumn;
        Vector3 bestRow = originalRow;
        int bestTemplateColumn = resolvedTemplateColumn;
        int bestTemplateRow = resolvedTemplateRow;
        float bestScore = float.PositiveInfinity;

        int columnDirectionCount = columns > 1 ? 2 : 1;
        int rowDirectionCount = rows > 1 ? 2 : 1;

        for (int columnDirection = 0; columnDirection < columnDirectionCount; columnDirection++)
        {
            Vector3 candidateColumn = originalColumn * (columnDirection == 0 ? 1f : -1f);

            for (int rowDirection = 0; rowDirection < rowDirectionCount; rowDirection++)
            {
                Vector3 candidateRow = originalRow * (rowDirection == 0 ? 1f : -1f);

                for (int anchorRow = 1; anchorRow <= rows; anchorRow++)
                {
                    for (int anchorColumn = 1; anchorColumn <= columns; anchorColumn++)
                    {
                        float score = ScoreGridFit(
                            parent,
                            shelfBounds,
                            originLocal,
                            candidateColumn,
                            candidateRow,
                            anchorRow,
                            anchorColumn);

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestColumn = candidateColumn;
                            bestRow = candidateRow;
                            bestTemplateColumn = anchorColumn;
                            bestTemplateRow = anchorRow;
                        }
                    }
                }
            }
        }

        colLocal = bestColumn;
        rowLocal = bestRow;
        resolvedTemplateColumn = bestTemplateColumn;
        resolvedTemplateRow = bestTemplateRow;
    }

    private bool TryGetShelfBounds(Transform parent, out Bounds bounds)
    {
        bounds = new Bounds();
        if (parent == null)
            return false;

        bool found = false;

        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

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
        Transform parent,
        Bounds shelfBounds,
        Vector3 originLocal,
        Vector3 colLocal,
        Vector3 rowLocal,
        int anchorRow,
        int anchorColumn)
    {
        Vector3 extents = shelfBounds.extents;
        extents.x = Mathf.Max(extents.x, 0.01f);
        extents.y = Mathf.Max(extents.y, 0.01f);
        extents.z = Mathf.Max(extents.z, 0.01f);

        float score = 0f;
        for (int row = 1; row <= rows; row++)
        {
            for (int column = 1; column <= columns; column++)
            {
                Vector3 localPoint = originLocal
                    + colLocal * (column - anchorColumn)
                    + rowLocal * (row - anchorRow);
                Vector3 point = parent != null ? parent.TransformPoint(localPoint) : localPoint;
                Vector3 normalized = new Vector3(
                    (point.x - shelfBounds.center.x) / extents.x,
                    (point.y - shelfBounds.center.y) / extents.y,
                    (point.z - shelfBounds.center.z) / extents.z);

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

    private float Tolerans(Vector3 colLocal, Vector3 rowLocal)
    {
        float a = colLocal.magnitude;
        float b = rowLocal.magnitude;
        float adim = (a > 1e-9f && b > 1e-9f) ? Mathf.Min(a, b) : Mathf.Max(a, b);
        return Mathf.Max(1e-7f, adim * occupancyTolerance);
    }

    private bool BuluHucreDolu(Transform parent, Vector3 hedefLocal, float tolerans, out ShelfSlot mevcut)
    {
        mevcut = null;
        if (parent == null)
            return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            ShelfSlot slot = child.GetComponent<ShelfSlot>();
            if (slot == null)
                continue;

            if (Vector3.Distance(child.localPosition, hedefLocal) < tolerans)
            {
                mevcut = slot;
                return true;
            }
        }

        return false;
    }

    private static Vector3 DunyadanLocale(Transform parent, Vector3 dunyaVektoru)
    {
        return parent != null ? parent.InverseTransformVector(dunyaVektoru) : dunyaVektoru;
    }

    private static float Dunya(Transform parent, Vector3 localVektor)
    {
        return parent != null
            ? parent.TransformVector(localVektor).magnitude
            : localVektor.magnitude;
    }

    private static string Fmt(Vector3 v)
    {
        return $"({v.x:0.######}, {v.y:0.######}, {v.z:0.######})";
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
