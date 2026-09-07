using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kitap prefablarindaki BookItem.baseRotationEuler degerlerini yonetir.
///
/// Iki yontem sunar:
///
/// A) KURAL YONTEMI  ->  base = inverse(nativeRotation) x OrtakDuzeltme
///    BookSpawner kitabin kok rotasyonunu ezdigi icin FBX'in import acisi
///    runtime'da kayboluyor. Kural once o aciyi geri alir, sonra butun kitaplar
///    icin AYNI olan duzeltmeyi uygular.
///
/// B) OLCU YONTEMI  ->  kitabin gercek en/boy/kalinlik oranlarindan hesaplar.
///    Bir cizgi roman yassi bir kutudur: en ince eksen kalinlik, en uzun eksen boy.
///    ONEMLI: olcu, mesh verisi ile kok objenin SCALE'i carpilarak bulunur. Cunku
///    modellerde "Apply Scale" yapilmamis; mesh verisi kup, kitap seklini scale veriyor.
///
/// Her yazma isleminden ONCE otomatik yedek alinir. Yanlis giderse geri donebilirsin.
///
/// Acmak icin: ust menu > ComicShop > Kitap Rotasyonlari
/// </summary>
public class BookRotationTool : EditorWindow
{
    private string searchFolder = "Assets/Prefabs/VeridianBooks";
    private Vector3 standardCorrection = new Vector3(270f, 0f, 180f);
    private GameObject referenceBook;
    private string pastedValues = "";

    private Vector2 scroll;
    private string report = "Once 'TAM RAPOR' butonuna bas.";

    private static string BackupFolder => Path.Combine(Application.dataPath, "../BookRotationBackups");

    [MenuItem("ComicShop/Kitap Rotasyonlari")]
    public static void Open()
    {
        BookRotationTool window = GetWindow<BookRotationTool>("Kitap Rotasyonlari");
        window.minSize = new Vector2(520f, 480f);
        window.Show();
    }

    // ==================================================================
    // Arayuz
    // ==================================================================

    void OnGUI()
    {
        searchFolder = EditorGUILayout.TextField("Aranacak Klasor", searchFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1 - TESHIS", EditorStyles.boldLabel);

        if (GUILayout.Button("TAM RAPOR (olculer, scale, native, base)", GUILayout.Height(26f)))
            FullReport();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2 - KURAL YONTEMI (native'i geri al)", EditorStyles.boldLabel);

        standardCorrection = EditorGUILayout.Vector3Field("Ortak Duzeltme", standardCorrection);

        if (GUILayout.Button("Simulasyon [kural]"))
            RunPlan(BuildPlanFromNative(), false, "KURAL YONTEMI");

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("Uygula [kural]", GUILayout.Height(26f)))
            Confirm("Kural yontemi", () => RunPlan(BuildPlanFromNative(), true, "KURAL YONTEMI"));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3 - OLCU YONTEMI (en/boy/kalinliktan)", EditorStyles.boldLabel);

        referenceBook = (GameObject)EditorGUILayout.ObjectField(
            "Dogru Duran Kitap", referenceBook, typeof(GameObject), false);

        EditorGUILayout.HelpBox(
            "Oyunda DOGRU duran bir kitap prefabini yukari surukle. Arac onun " +
            "en/boy/kalinlik eksenlerinin nereye baktigini ogrenir, butun kitaplari " +
            "ayni yone getirir. Olcu = mesh boyutu x kok scale.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(referenceBook == null))
        {
            if (GUILayout.Button("Simulasyon [olcu]"))
                RunPlan(BuildPlanFromSize(), false, "OLCU YONTEMI");

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Uygula [olcu]", GUILayout.Height(26f)))
                Confirm("Olcu yontemi", () => RunPlan(BuildPlanFromSize(), true, "OLCU YONTEMI"));
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4 - TEK TEK DUZELTME", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Project panelinden prefabi SEC, sonra bir butona bas.",
            EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("90 X")) FlipSelected(new Vector3(90f, 0f, 0f));
            if (GUILayout.Button("90 Y")) FlipSelected(new Vector3(0f, 90f, 0f));
            if (GUILayout.Button("90 Z")) FlipSelected(new Vector3(0f, 0f, 90f));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("180 X")) FlipSelected(new Vector3(180f, 0f, 0f));
            if (GUILayout.Button("180 Y")) FlipSelected(new Vector3(0f, 180f, 0f));
            if (GUILayout.Button("180 Z")) FlipSelected(new Vector3(0f, 0f, 180f));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("5 - YEDEK / GERI ALMA", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Son Yedegi Geri Yukle"))
                RestoreLatestBackup();

            if (GUILayout.Button("Yedek Klasorunu Ac"))
                EditorUtility.RevealInFinder(EnsureBackupFolder());
        }

        EditorGUILayout.LabelField("Elle liste (her satir:  KitapAdi = x, y, z)");
        pastedValues = EditorGUILayout.TextArea(pastedValues, GUILayout.Height(60f));
        if (GUILayout.Button("Listeyi Uygula"))
            ApplyPastedValues();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SONUC", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Confirm(string what, System.Action action)
    {
        bool ok = EditorUtility.DisplayDialog(
            "Kitap Rotasyonlari",
            $"{what} uygulanacak.\n\nDegisiklikten once otomatik yedek alinacak, " +
            $"istersen 5. bolumden geri donebilirsin.\n\nDevam?",
            "Evet, uygula", "Vazgec");

        if (ok)
            action();
    }

    // ==================================================================
    // Kitap toplama ve olcum
    // ==================================================================

    private class Book
    {
        public string path;
        public string name;
        public Vector3 baseEuler;
        public Quaternion native;
        public Vector3 rootScale;
        public Vector3 meshSize;
        public Vector3 realSize;
        public bool measured;

        public int thickAxis;
        public int heightAxis;
        public int widthAxis;
        public bool ambiguous;
    }

    private List<Book> Collect(out string error)
    {
        error = null;
        List<Book> books = new List<Book>();

        if (!AssetDatabase.IsValidFolder(searchFolder))
        {
            error = $"HATA: '{searchFolder}' diye bir klasor yok.";
            return books;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                continue;

            BookItem item = asset.GetComponentInChildren<BookItem>(true);
            if (item == null)
                continue;

            Book book = new Book
            {
                path = path,
                name = asset.name,
                baseEuler = item.baseRotationEuler,
                native = IsValid(item.nativeRotation) ? item.nativeRotation : asset.transform.localRotation,
                rootScale = asset.transform.localScale
            };

            if (TryGetLocalBounds(asset, out Bounds bounds))
            {
                book.meshSize = bounds.size;
                book.realSize = new Vector3(
                    Mathf.Abs(bounds.size.x * book.rootScale.x),
                    Mathf.Abs(bounds.size.y * book.rootScale.y),
                    Mathf.Abs(bounds.size.z * book.rootScale.z));

                ResolveAxisRoles(book.realSize, out book.thickAxis, out book.heightAxis, out book.widthAxis);

                float longest = book.realSize[book.heightAxis];
                float middle = book.realSize[book.widthAxis];
                float thin = book.realSize[book.thickAxis];

                book.ambiguous = longest <= 0f
                    || (longest - middle) / longest < 0.06f
                    || (middle - thin) / Mathf.Max(middle, 1e-6f) < 0.06f;

                book.measured = true;
            }

            books.Add(book);
        }

        books.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return books;
    }

    private static bool IsValid(Quaternion q)
    {
        return q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f;
    }

    /// <summary>Mesh sinirlari, prefab kokunun local uzayinda (kokun kendi scale'i HARIC).</summary>
    private static bool TryGetLocalBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;

        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Bounds mb = filter.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    mb.center.x + ((i & 1) == 0 ? -mb.extents.x : mb.extents.x),
                    mb.center.y + ((i & 2) == 0 ? -mb.extents.y : mb.extents.y),
                    mb.center.z + ((i & 4) == 0 ? -mb.extents.z : mb.extents.z));

                Vector3 p = m.MultiplyPoint3x4(corner);

                if (!found)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        return found;
    }

    private static void ResolveAxisRoles(Vector3 size, out int thick, out int height, out int width)
    {
        thick = 0;
        height = 0;

        for (int i = 1; i < 3; i++)
        {
            if (size[i] < size[thick]) thick = i;
            if (size[i] > size[height]) height = i;
        }

        if (thick == height)
            height = (thick + 1) % 3;

        width = 3 - thick - height;
    }

    private static Vector3 AxisVector(int index)
    {
        return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
    }

    private static Vector3 SnapToAxis(Vector3 v)
    {
        int best = 0;
        for (int i = 1; i < 3; i++)
        {
            if (Mathf.Abs(v[i]) > Mathf.Abs(v[best]))
                best = i;
        }

        Vector3 result = Vector3.zero;
        result[best] = Mathf.Sign(v[best]);
        return result;
    }

    private static Quaternion BasisRotation(
        Vector3 a1, Vector3 a2, Vector3 a3,
        Vector3 b1, Vector3 b2, Vector3 b3)
    {
        Matrix4x4 src = Matrix4x4.identity;
        src.SetColumn(0, new Vector4(a1.x, a1.y, a1.z, 0f));
        src.SetColumn(1, new Vector4(a2.x, a2.y, a2.z, 0f));
        src.SetColumn(2, new Vector4(a3.x, a3.y, a3.z, 0f));

        Matrix4x4 dst = Matrix4x4.identity;
        dst.SetColumn(0, new Vector4(b1.x, b1.y, b1.z, 0f));
        dst.SetColumn(1, new Vector4(b2.x, b2.y, b2.z, 0f));
        dst.SetColumn(2, new Vector4(b3.x, b3.y, b3.z, 0f));

        return (dst * src.transpose).rotation;
    }

    // ==================================================================
    // Plan uretimi
    // ==================================================================

    private class Plan
    {
        public List<Book> books = new List<Book>();
        public Dictionary<string, Vector3> targets = new Dictionary<string, Vector3>();
        public string error;
        public string note = "";
    }

    private Plan BuildPlanFromNative()
    {
        Plan plan = new Plan();
        plan.books = Collect(out plan.error);
        if (plan.error != null)
            return plan;

        Quaternion correction = Quaternion.Euler(standardCorrection);
        plan.note = $"Kural: base = inverse(native) x Euler{Fmt(standardCorrection)}";

        foreach (Book book in plan.books)
            plan.targets[book.path] = Normalize((Quaternion.Inverse(book.native) * correction).eulerAngles);

        return plan;
    }

    private Plan BuildPlanFromSize()
    {
        Plan plan = new Plan();
        plan.books = Collect(out plan.error);
        if (plan.error != null)
            return plan;

        string referencePath = AssetDatabase.GetAssetPath(referenceBook);
        Book reference = plan.books.Find(b => b.path == referencePath);

        if (reference == null)
        {
            plan.error = $"HATA: Referans '{referenceBook.name}' bu klasorde bulunamadi.";
            return plan;
        }

        if (!reference.measured)
        {
            plan.error = "HATA: Referans kitabin mesh'i olculemedi.";
            return plan;
        }

        Quaternion refRotation = Quaternion.Euler(reference.baseEuler);
        Vector3 targetThick = SnapToAxis(refRotation * AxisVector(reference.thickAxis));
        Vector3 targetHeight = SnapToAxis(refRotation * AxisVector(reference.heightAxis));

        plan.note = $"Referans: {reference.name} {Fmt(reference.baseEuler)}\n" +
                    $"Gercek olcu: {FmtSize(reference.realSize)}\n" +
                    $"Hedef -> kalinlik ekseni: {targetThick}, boy ekseni: {targetHeight}";

        foreach (Book book in plan.books)
        {
            if (!book.measured)
                continue;

            Vector3 aThick = AxisVector(book.thickAxis);
            Vector3 aHeight = AxisVector(book.heightAxis);
            Vector3 aWidth = AxisVector(book.widthAxis);

            float handedness = Mathf.Sign(Vector3.Dot(Vector3.Cross(aThick, aHeight), aWidth));
            Vector3 targetWidth = Vector3.Cross(targetThick, targetHeight) * handedness;

            Quaternion rotation = BasisRotation(
                aThick, aHeight, aWidth,
                targetThick, targetHeight, targetWidth);

            plan.targets[book.path] = Normalize(rotation.eulerAngles);
        }

        return plan;
    }

    // ==================================================================
    // Plan calistirma
    // ==================================================================

    private void RunPlan(Plan plan, bool apply, string title)
    {
        if (plan.error != null)
        {
            report = plan.error;
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{title} -- {(apply ? "UYGULANDI" : "SIMULASYON (hicbir sey degismedi)")}");
        if (!string.IsNullOrEmpty(plan.note))
            sb.AppendLine(plan.note);
        sb.AppendLine();

        if (apply)
        {
            string backup = WriteBackup(plan.books);
            sb.AppendLine($"Yedek alindi: {backup}");
            sb.AppendLine();
        }

        int changed = 0, same = 0, skipped = 0;

        foreach (Book book in plan.books)
        {
            if (!plan.targets.TryGetValue(book.path, out Vector3 target))
            {
                sb.AppendLine($"  ATLANDI  {book.name}  (olculemedi)");
                skipped++;
                continue;
            }

            string flag = book.ambiguous ? "   [olculer birbirine yakin, gozle kontrol et]" : "";

            if (SameAngle(target, book.baseEuler))
            {
                sb.AppendLine($"  AYNI     {book.name}  {Fmt(book.baseEuler)}{flag}");
                same++;
                continue;
            }

            sb.AppendLine($"  DEGISTI  {book.name}  {Fmt(book.baseEuler)} -> {Fmt(target)}{flag}");
            changed++;

            if (apply)
                WriteRotation(book.path, target);
        }

        if (apply)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        sb.AppendLine();
        sb.AppendLine($"Degisen: {changed}   Ayni kalan: {same}   Atlanan: {skipped}");
        report = sb.ToString();
    }

    // ==================================================================
    // Rapor
    // ==================================================================

    private void FullReport()
    {
        List<Book> books = Collect(out string error);
        if (error != null) { report = error; return; }

        string[] axisNames = { "X", "Y", "Z" };
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"TAM RAPOR -- {searchFolder}   ({books.Count} kitap)");
        sb.AppendLine();

        foreach (Book book in books)
        {
            sb.AppendLine(book.name);
            sb.AppendLine($"   base       : {Fmt(book.baseEuler)}");
            sb.AppendLine($"   native     : {Fmt(book.native.eulerAngles)}");
            sb.AppendLine($"   kok scale  : {FmtSize(book.rootScale)}");
            sb.AppendLine($"   mesh olcu  : {FmtSize(book.meshSize)}");

            if (book.measured)
            {
                sb.AppendLine($"   GERCEK OLCU: {FmtSize(book.realSize)}");
                sb.AppendLine($"   roller     : kalinlik={axisNames[book.thickAxis]}  " +
                              $"boy={axisNames[book.heightAxis]}  genislik={axisNames[book.widthAxis]}"
                              + (book.ambiguous ? "   [BELIRSIZ]" : ""));
            }
            else
            {
                sb.AppendLine("   GERCEK OLCU: olculemedi");
            }

            sb.AppendLine();
        }

        report = sb.ToString();
    }

    // ==================================================================
    // Yazma, yedek, geri alma
    // ==================================================================

    private static string EnsureBackupFolder()
    {
        string folder = Path.GetFullPath(BackupFolder);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return folder;
    }

    private string WriteBackup(List<Book> books)
    {
        string folder = EnsureBackupFolder();
        string file = Path.Combine(folder, $"rotations_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (Book book in books)
            sb.AppendLine($"{book.name} = {book.baseEuler.x:0}, {book.baseEuler.y:0}, {book.baseEuler.z:0}");

        File.WriteAllText(file, sb.ToString());
        return Path.GetFileName(file);
    }

    private void RestoreLatestBackup()
    {
        string folder = EnsureBackupFolder();
        string[] files = Directory.GetFiles(folder, "rotations_*.txt");

        if (files.Length == 0)
        {
            report = "Hic yedek yok. Yedekler ilk 'Uygula' isleminde olusmaya baslar.";
            return;
        }

        System.Array.Sort(files);
        string latest = files[files.Length - 1];

        pastedValues = File.ReadAllText(latest);
        ApplyPastedValues();
        report = $"Yedekten geri yuklendi: {Path.GetFileName(latest)}\n\n" + report;
    }

    private void ApplyPastedValues()
    {
        if (string.IsNullOrWhiteSpace(pastedValues))
        {
            report = "Once liste kutusuna deger yapistir.";
            return;
        }

        Dictionary<string, string> pathByName = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentInChildren<BookItem>(true) != null)
                pathByName[asset.name] = path;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int applied = 0, failed = 0;

        foreach (string rawLine in pastedValues.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                sb.AppendLine($"  OKUNAMADI  {line}");
                failed++;
                continue;
            }

            string bookName = line.Substring(0, eq).Trim();
            string[] parts = line.Substring(eq + 1)
                .Split(new[] { ',', ';', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3
                || !ParseFloat(parts[0], out float x)
                || !ParseFloat(parts[1], out float y)
                || !ParseFloat(parts[2], out float z))
            {
                sb.AppendLine($"  SAYI OKUNAMADI  {line}");
                failed++;
                continue;
            }

            if (!pathByName.TryGetValue(bookName, out string path))
            {
                foreach (KeyValuePair<string, string> pair in pathByName)
                {
                    if (pair.Key.StartsWith(bookName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        path = pair.Value;
                        break;
                    }
                }
            }

            if (path == null)
            {
                sb.AppendLine($"  BULUNAMADI  {bookName}");
                failed++;
                continue;
            }

            WriteRotation(path, new Vector3(x, y, z));
            sb.AppendLine($"  {bookName} = {x:0}, {y:0}, {z:0}");
            applied++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        report = $"Uygulanan: {applied}, basarisiz: {failed}\n\n" + sb;
    }

    private void FlipSelected(Vector3 flipEuler)
    {
        Object[] selection = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
        if (selection.Length == 0)
        {
            report = "Once Project panelinden bir kitap prefabi sec.";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int count = 0;

        foreach (Object obj in selection)
        {
            GameObject asset = obj as GameObject;
            if (asset == null)
                continue;

            BookItem item = asset.GetComponentInChildren<BookItem>(true);
            if (item == null)
                continue;

            Vector3 before = item.baseRotationEuler;
            Vector3 after = Normalize(
                (Quaternion.Euler(flipEuler) * Quaternion.Euler(before)).eulerAngles);

            WriteRotation(AssetDatabase.GetAssetPath(asset), after);
            sb.AppendLine($"  {asset.name}: {Fmt(before)} -> {Fmt(after)}");
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report = count == 0
            ? "Secilenler arasinda kitap prefabi yok."
            : $"{count} kitap cevrildi ({Fmt(flipEuler)}):\n" + sb;
    }

    private static void WriteRotation(string path, Vector3 euler)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BookItem item = contents.GetComponentInChildren<BookItem>(true);
            if (item != null)
            {
                item.baseRotationEuler = Normalize(euler);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ==================================================================
    // Yardimcilar
    // ==================================================================

    private static bool ParseFloat(string text, out float value)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static Vector3 Normalize(Vector3 euler)
    {
        return new Vector3(
            Mathf.Repeat(Mathf.Round(euler.x), 360f),
            Mathf.Repeat(Mathf.Round(euler.y), 360f),
            Mathf.Repeat(Mathf.Round(euler.z), 360f));
    }

    private static bool SameAngle(Vector3 a, Vector3 b)
    {
        // Ayni donusun birden fazla Euler yazilisi olabilir:
        // (270, 0, 180) ile (270, 180, 0) AYNI donustur.
        // Bu yuzden sayilari degil, donusun kendisini karsilastiriyoruz.
        return Quaternion.Angle(Quaternion.Euler(a), Quaternion.Euler(b)) < 1f;
    }

    private static string Fmt(Vector3 euler)
    {
        return $"({euler.x:0}, {euler.y:0}, {euler.z:0})";
    }

    private static string FmtSize(Vector3 size)
    {
        return $"({size.x:0.####}, {size.y:0.####}, {size.z:0.####})";
    }
}