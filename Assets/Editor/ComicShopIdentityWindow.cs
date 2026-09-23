using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ComicShopIdentityWindow : EditorWindow
{
    const string AnchorPrefix = "LOGO_BOS_KARE_";
    const string SignName = "ComicShop_PublisherFlag";
    const string Generated = "Assets/ComicShopIdentityGenerated";
    const string ShaderPath = "Assets/Shaders/ComicShopPublisherFlag.shader";
    sealed class Row
    {
        public BookData data;
        public string original, draft;
    }
    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    int tab, brand;
    Texture2D flag;
    float padding = .04f;
    [SerializeField] bool flipHorizontal;
    [SerializeField] bool flipVertical;
    string filter = "", message = "";

    [MenuItem("Tools/ComicShop/Kitap Isimleri ve Flamalar")]
    static void Open() => GetWindow<ComicShopIdentityWindow>("Kitap ve Flama");

    void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, new[] { "Kitap isimleri", "Raf flamalari" });
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (tab == 0) DrawBooks(); else DrawFlags();
        }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    void DrawBooks()
    {
        EditorGUILayout.HelpBox("Kitaplari yukle, isimleri duzenle, Kaydet'e bas. Isimler prefab'daki BookDisplayName alanina yazilir ve sonraki Play'de HUD'da gorunur. ID ve dosya adlari korunur. Ayni prefab'i kullanan kayitlar tek satirdir.", MessageType.Info);
        if (GUILayout.Button("Kitap listesini yukle / taslaklari sifirla")) LoadBooks();
        filter = EditorGUILayout.TextField("Ara (isim / marka / ID)", filter);
        if (GUILayout.Button("Gorunenlere dosya adindan isim oner (taslak)"))
            foreach (Row r in VisibleRows()) r.draft = Suggest(r.data.bookPrefab.name);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row r in VisibleRows())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("#" + r.data.BookID, GUILayout.Width(55))) EditorGUIUtility.PingObject(r.data.bookPrefab);
                GUILayout.Label(BrandConfig.GetBrandName(r.data.BrandID), GUILayout.Width(100));
                r.draft = EditorGUILayout.TextField(r.draft);
                if (GUILayout.Button("Geri", GUILayout.Width(40))) r.draft = r.original;
            }
        }
        EditorGUILayout.EndScrollView();
        int changed = rows.Count(r => r.draft != r.original);
        using (new EditorGUI.DisabledScope(changed == 0))
            if (GUILayout.Button("Degisen " + changed + " kitap ismini kaydet")) SaveBooks();
    }

    IEnumerable<Row> VisibleRows() => rows.Where(r => string.IsNullOrWhiteSpace(filter) ||
        (r.data.BookID + " " + BrandConfig.GetBrandName(r.data.BrandID) + " " + r.draft + " " + r.data.bookPrefab.name)
        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

    void LoadBooks()
    {
        rows.Clear();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:BookData", new[] { "Assets" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<BookData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!data || !data.bookPrefab) continue;
            string path = AssetDatabase.GetAssetPath(data.bookPrefab);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !seen.Add(path)) continue;
            var display = data.bookPrefab.GetComponent<BookDisplayName>();
            string title = display ? display.Name : "";
            rows.Add(new Row { data = data, original = title, draft = title });
        }
        rows.Sort((a, b) => a.data.BookID.CompareTo(b.data.BookID));
        message = rows.Count + " kitap prefab'i yuklendi.";
    }

    static string Suggest(string source)
    {
        string title = Regex.Replace(source, @"^(Book|BookData)_\d+_", "", RegexOptions.IgnoreCase);
        title = title.Replace('_', ' ').Trim();
        Match volume = Regex.Match(title, @"^(.*?)(\d+)$");
        if (volume.Success) title = volume.Groups[1].Value.Trim() + " - Cilt " + volume.Groups[2].Value;
        return CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(title.Replace('ı', 'i').ToLowerInvariant());
    }

    void SaveBooks()
    {
        var pending = rows.Where(r => r.draft != r.original).ToList();
        if (pending.Any(r => string.IsNullOrWhiteSpace(r.draft)))
        { message = "Bos isim var; once doldur. Hicbir isim kaydedilmedi."; return; }
        int saved = 0;
        try
        {
            foreach (Row r in pending)
            {
                string path = AssetDatabase.GetAssetPath(r.data.bookPrefab);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var display = root.GetComponent<BookDisplayName>();
                    string current = display ? display.Name : "";
                    if (current != r.original)
                        throw new InvalidOperationException(path + " disaridan degisti. Listeyi yeniden yukle.");
                    if (!display) display = root.AddComponent<BookDisplayName>();
                    display.SetName(r.draft.Trim());
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success) throw new InvalidOperationException("Kaydedilemedi: " + path);
                    r.original = r.draft = r.draft.Trim();
                    saved++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            message = saved + " isim kaydedildi. Play'i yeniden baslat.";
        }
        catch (Exception e) { message = saved + " isim kaydedildi; kalan islem durdu: " + e.Message; Debug.LogException(e); }
    }

    void DrawFlags()
    {
        EditorGUILayout.HelpBox("Hierarchy'den kitapligi veya LOGO_BOS_KARE nesnesini sec. PNG/JPG flamayi asagi surukle ve yerlestir. Hazir kare noktanin olcusu ve yonu kullanilir; gorsel orani korunur. Ctrl+Z geri alir, Ctrl+S sahneyi kaydeder.", MessageType.Info);
        flag = (Texture2D)EditorGUILayout.ObjectField("Flama gorseli", flag, typeof(Texture2D), false);
        padding = EditorGUILayout.Slider("Kenar boslugu", padding, 0, .3f);
        flipHorizontal = EditorGUILayout.Toggle("Yatay aynala (sag / sol)", flipHorizontal);
        flipVertical = EditorGUILayout.Toggle("Dikey cevir (ust / alt)", flipVertical);
        EditorGUILayout.HelpBox("Yazi ayna gibi tersse Yatay aynala secenegini isaretle, sonra Yerlestir / guncelle butonuna bas. Bu secenekler toplu uygulamada da kullanilir. Asagidaki onizleme kaynak gorseldir.", MessageType.None);
        if (flag) GUILayout.Label(AssetPreview.GetAssetPreview(flag) ?? flag, GUILayout.Width(100), GUILayout.Height(100));
        using (new EditorGUI.DisabledScope(!flag))
            if (GUILayout.Button("Secili raflara flamayi yerlestir / guncelle"))
                ApplyFlags(SelectedAnchors());
        EditorGUILayout.Space();
        var catalog = Resources.Load<BrandCatalog>("BrandCatalog");
        if (catalog && catalog.brands != null && catalog.brands.Length > 0)
        {
            var entries = catalog.brands.Where(e => e != null).ToArray();
            if (entries.Length > 0)
            {
                brand = Mathf.Clamp(brand, 0, entries.Length - 1);
                brand = EditorGUILayout.Popup("Toplu uygulama markasi", brand, entries.Select(e => e.brandID + " - " + e.brandName).ToArray());
                EditorGUILayout.HelpBox("Toplu uygulama yalniz tek bir marka tasiyan raflari kullanir. Marka bulunamayan veya karisik markali raflar atlanir; bunlari secerek yerlestirebilirsin.", MessageType.None);
                using (new EditorGUI.DisabledScope(!flag))
                    if (GUILayout.Button("Bu markanin acik sahnelerdeki raflarina uygula"))
                        ApplyFlags(AllAnchors().Where(a => ResolveBrand(a) == entries[brand].brandID));
            }
        }
    }

    static bool IsAnchor(Transform t) => t && t.name.StartsWith(AnchorPrefix, StringComparison.Ordinal);
    static IEnumerable<Transform> AllAnchors() => Resources.FindObjectsOfTypeAll<Transform>()
        .Where(t => IsAnchor(t) && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(t));

    static IEnumerable<Transform> SelectedAnchors()
    {
        var found = new HashSet<Transform>();
        foreach (var obj in Selection.gameObjects)
        {
            if (EditorUtility.IsPersistent(obj) || !obj.scene.IsValid()) continue;
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true)) if (IsAnchor(t)) found.Add(t);
            for (Transform t = obj.transform; t; t = t.parent)
                if (IsAnchor(t)) { found.Add(t); break; }
        }
        return found;
    }

    static int ResolveBrand(Transform anchor)
    {
        for (Transform t = anchor.parent; t; t = t.parent)
        {
            // Never infer a shelf's brand from a container holding several flag anchors.
            if (t.GetComponentsInChildren<Transform>(true).Count(IsAnchor) != 1) return -1;
            var slots = t.GetComponentsInChildren<ShelfSlot>(true);
            if (slots.Length > 0) return slots.All(s => s.brandID == slots[0].brandID) ? slots[0].brandID : -1;
        }
        return -1;
    }

    void ApplyFlags(IEnumerable<Transform> source)
    {
        var anchors = source.Distinct().ToArray();
        if (anchors.Length == 0) { message = "Uygun LOGO_BOS_KARE noktasi bulunamadi. Hierarchy'den cerceveli kitapligi sec."; return; }
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (!shader) { message = "Flama shader dosyasi bulunamadi: " + ShaderPath; return; }
        if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets", "ComicShopIdentityGenerated");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Raf flamalarini yerlestir");
        int done = 0, skipped = 0;
        try
        {
            foreach (Transform anchor in anchors)
            {
                string sizeText = anchor.name.Substring(AnchorPrefix.Length).TrimEnd('m');
                if (!float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float size) ||
                    size <= 0 || float.IsInfinity(size) || float.IsNaN(size) || Mathf.Abs(anchor.localToWorldMatrix.determinant) < 1e-10f)
                { skipped++; continue; }
                // Bake world-sized corners into anchor space: safe even under rotated 200x FBX parents.
                float side = size * (1 - padding * 2);
                float ratio = (float)flag.width / Mathf.Max(1, flag.height);
                float w = side * Mathf.Min(1, ratio), h = side * Mathf.Min(1, 1 / ratio);
                Vector3 center = anchor.position + anchor.forward * .001f;
                Vector3 right = anchor.right * w * .5f, up = anchor.up * h * .5f;
                var mesh = new Mesh { name = "PublisherFlag" };
                mesh.vertices = new[] { center-right-up, center+right-up, center+right+up, center-right+up }
                    .Select(anchor.InverseTransformPoint).ToArray();
                var uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                for (int i = 0; i < uv.Length; i++)
                {
                    if (flipHorizontal) uv[i].x = 1f - uv[i].x;
                    if (flipVertical) uv[i].y = 1f - uv[i].y;
                }
                mesh.uv = uv;
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = AssetDatabase.GenerateUniqueAssetPath(Generated + "/PublisherFlag.asset");
                AssetDatabase.CreateAsset(mesh, path);
                var material = new Material(shader) { name = "PublisherFlag" };
                material.SetTexture("_BaseMap", flag);
                AssetDatabase.AddObjectToAsset(material, mesh);
                Transform existing = anchor.Find(SignName);
                if (existing) Undo.DestroyObjectImmediate(existing.gameObject);
                var sign = new GameObject(SignName);
                Undo.RegisterCreatedObjectUndo(sign, "Flama yerlestir");
                sign.transform.SetParent(anchor, false);
                sign.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = sign.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                done++;
            }
            AssetDatabase.SaveAssets();
            message = done + " flama yerlestirildi, " + skipped + " gecersiz nokta atlandi. Ctrl+S ile sahneyi kaydet. Ctrl+Z geri alir.";
        }
        catch (Exception e) { message = done + " flama yerlestirildi; islem durdu: " + e.Message; Debug.LogException(e); }
        finally { Undo.CollapseUndoOperations(group); }
    }
}
