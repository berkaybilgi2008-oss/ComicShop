using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Fits the visible shelf mesh, never interaction slots or spawned books.
public sealed class ComicShopShelfFrameWindow : EditorWindow
{
    const string Folder = "Assets/ComicShopShelfFrame/Generated";
    const string FrameName = "ComicShop_OuterFrame";
    MeshFilter shelf;
    Material wood;
    bool reverse;
    string result;

    [MenuItem("Tools/ComicShop/Raf Dis Cercevesi")]
    static void Open() { GetWindow<ComicShopShelfFrameWindow>("Raf Cercevesi"); }

    void OnEnable()
    {
        foreach (string id in AssetDatabase.FindAssets("M_6 t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));
            if (m && m.shader && m.shader.name == "ComicShop/V16 Source Toon" &&
                m.GetTexture("_BaseMap") && m.GetTexture("_BaseMap").name == "T_6")
            { wood = m; break; }
        }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("15 gozlu RAF'in gorunur mesh'ini sec. Cerceve disina eklenir; kitaplar, slotlar ve sahne ayarlari korunur. Logo alani BOS KARE olarak birakilir.", MessageType.Info);
        if (GUILayout.Button("Secili raftan mesh al"))
        {
            var selected = Selection.activeGameObject;
            shelf = selected ? selected.GetComponent<MeshFilter>() : null;
            if (!shelf && selected)
            {
                float largest = 0;
                foreach (var f in selected.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!f.sharedMesh || IsFrame(f.transform)) continue;
                    float size = f.sharedMesh.bounds.size.sqrMagnitude;
                    if (size > largest) { largest = size; shelf = f; }
                }
            }
        }
        shelf = (MeshFilter)EditorGUILayout.ObjectField("Gorunur RAF mesh", shelf, typeof(MeshFilter), true);
        wood = (Material)EditorGUILayout.ObjectField("Koyu ahsap TOON", wood, typeof(Material), false);
        reverse = EditorGUILayout.Toggle("On / arka yonunu cevir", reverse);
        EditorGUILayout.HelpBox("On yon, Scene kamerasinin baktigi raf yuzune gore secilir. Scene gorunumunu rafin onune getir; ters olursa Ctrl+Z ile geri alip yonu cevir. M_6, dukkanin koyu ahsap malzemesidir.", MessageType.None);
        using (new EditorGUI.DisabledScope(!shelf || !wood || EditorApplication.isPlaying))
            if (GUILayout.Button("Secili rafa cerceve ekle"))
            {
                try { Build(); }
                catch (Exception e) { result = e.Message; Debug.LogError("Raf cercevesi: " + e.Message); }
            }
        if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    static bool IsFrame(Transform t)
    {
        for (; t; t = t.parent) if (t.name == FrameName) return true;
        return false;
    }

    void Build()
    {
        if (!shelf.sharedMesh || !shelf.gameObject.scene.IsValid() || EditorUtility.IsPersistent(shelf))
            throw new InvalidOperationException("Project dosyasi yerine sahnedeki RAF mesh'ini sec.");
        if (IsFrame(shelf.transform) || shelf.transform.Find(FrameName))
            throw new InvalidOperationException("Bu rafta cerceve var. Yeniden yapmak icin once Ctrl+Z veya yalniz ComicShop_OuterFrame nesnesini sil.");
        if (!wood.shader || wood.shader.name != "ComicShop/V16 Source Toon" || wood.GetFloat("_Smooth") > .5f || wood.GetFloat("_Unlit") > .5f)
            throw new InvalidOperationException("Dukkanin ComicShop/V16 Source Toon, Smooth=0 olan isikli koyu ahsap malzemesini sec (M_6).");
        if (Mathf.Abs(shelf.transform.localToWorldMatrix.determinant) < 1e-10f)
            throw new InvalidOperationException("Raf eksenlerinden biri sifir olcekli; mesh boyutu olculemiyor.");

        Transform target = shelf.transform;
        Bounds mb = shelf.sharedMesh.bounds;
        Vector3[] axes = { target.TransformVector(Vector3.right), target.TransformVector(Vector3.up), target.TransformVector(Vector3.forward) };
        int upIndex = 0;
        for (int i = 1; i < 3; i++) if (Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up)) > Mathf.Abs(Vector3.Dot(axes[upIndex].normalized, Vector3.up))) upIndex = i;
        int a = (upIndex + 1) % 3, b = (upIndex + 2) % 3;
        int depthIndex = axes[a].magnitude * mb.size[a] < axes[b].magnitude * mb.size[b] ? a : b;
        Vector3 forward = Vector3.ProjectOnPlane(axes[depthIndex], Vector3.up).normalized;
        if (forward.sqrMagnitude < .9f) throw new InvalidOperationException("Rafin on yuzu belirlenemedi.");
        Vector3 center = target.TransformPoint(mb.center);
        var view = SceneView.lastActiveSceneView;
        if (view && view.camera && Vector3.Dot(forward, view.camera.transform.position - center) < 0) forward = -forward;
        if (reverse) forward = -forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Bounds bounds = new Bounds();
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = target.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))) - center;
            p = new Vector3(Vector3.Dot(p, right), p.y, Vector3.Dot(p, forward));
            if (i == 0) bounds = new Bounds(p, Vector3.zero); else bounds.Encapsulate(p);
        }
        float w = bounds.size.x, h = bounds.size.y, d = bounds.size.z;
        if (w < .01f || h < .01f || d < .001f) throw new InvalidOperationException("Mesh boyutlari gecersiz.");
        Vector3 origin = center + right * bounds.center.x + Vector3.up * bounds.min.y + forward * bounds.center.z;
        Func<Vector3, Vector3> local = p => target.InverseTransformPoint(origin + right * p.x + Vector3.up * p.y + forward * p.z);
        var geo = new Geometry(local, target.localToWorldMatrix.determinant < 0);
        float post = w * .048f, beam = h * .045f, gap = w * .003f, front = d * .5f + post * .22f;
        float inkWidth = w * .0011f;
        Action<Vector3, Vector3> box = (p, s) => geo.Box(p, s, 0, inkWidth);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (w * .5f + gap + post * .5f);
            box(new Vector3(x, h * .5f, 0), new Vector3(post, h, d + post * .44f));
            box(new Vector3(x, beam * .62f, 0), new Vector3(post * 1.35f, beam * 1.24f, d + post * .65f));
            box(new Vector3(x, h + beam * .12f, 0), new Vector3(post * 1.42f, beam * .55f, d + post * .8f));
            // Recess-like dark pinstripes on the front pilasters.
            for (int k = -1; k <= 1; k += 2)
                geo.Box(new Vector3(x + k * post * .29f, h * .5f, front + inkWidth * .5f), new Vector3(inkWidth, h - beam * 2.8f, inkWidth), 1, 0);
        }
        float outer = w + 2 * (gap + post);
        box(new Vector3(0, h + gap + beam * .5f, 0), new Vector3(outer, beam, d + post * .44f));
        box(new Vector3(0, h + gap + beam * 1.12f, 0), new Vector3(outer + post * .35f, beam * .24f, d + post * .7f));
        float logo = Mathf.Min(w * .17f, h * .27f), baseY = h + gap + beam * 1.24f;
        Vector2[] silhouette = { new Vector2(-1.15f, 0), new Vector2(1.15f, 0), new Vector2(.8f, .2f), new Vector2(.58f, .88f), new Vector2(.43f, 1), new Vector2(-.43f, 1), new Vector2(-.58f, .88f), new Vector2(-.8f, .2f) };
        geo.Profile(silhouette, logo, baseY, front - post * .55f, post * .5f, inkWidth);
        float square = logo * .70f, rim = logo * .035f, cy = baseY + logo * .52f;
        float squareFront = front + inkWidth * 2;
        box(new Vector3(0, cy, squareFront - rim), new Vector3(square, square, rim));
        for (int sign = -1; sign <= 1; sign += 2)
        {
            box(new Vector3(sign * (square + rim) * .5f, cy, squareFront), new Vector3(rim, square + rim * 2, rim));
            box(new Vector3(0, cy + sign * (square + rim) * .5f, squareFront), new Vector3(square, rim, rim));
        }

        EnsureFolder(Folder);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/ShelfFrame.asset");
        Mesh mesh = geo.ToMesh();
        AssetDatabase.CreateAsset(mesh, assetPath);
        var ink = new Material(wood) { name = "Frame_Ink" };
        ink.SetTexture("_BaseMap", Texture2D.whiteTexture);
        ink.SetColor("_BaseColor", new Color(.006f, .003f, .0015f, 1));
        ink.SetFloat("_Unlit", 1); ink.SetFloat("_Smooth", 0);
        AssetDatabase.AddObjectToAsset(ink, mesh);
        var root = new GameObject(FrameName);
        Undo.RegisterCreatedObjectUndo(root, "Raf dis cercevesi ekle");
        root.transform.SetParent(target, false);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { wood, ink };
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        var logoAnchor = new GameObject("LOGO_BOS_KARE_" + square.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "m");
        logoAnchor.transform.SetParent(root.transform, false);
        logoAnchor.transform.localPosition = local(new Vector3(0, cy, squareFront + rim));
        logoAnchor.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        // Deliberately no Collider, Rigidbody, spawner, camera, or renderer material replacement.
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        result = string.Format("Hazir. Raf: {0:F3} x {1:F3} x {2:F3} m. BOS kare logo: {3:F3} m. Ctrl+S ile sahneyi kaydet. Ctrl+Z geri alir.", w, h, d, square);
        Debug.Log(result, root);
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/'); string current = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
    }

    sealed class Geometry
    {
        readonly Func<Vector3, Vector3> convert;
        readonly bool mirrored;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles = { new List<int>(), new List<int>() };
        public Geometry(Func<Vector3, Vector3> convert, bool mirrored) { this.convert = convert; this.mirrored = mirrored; }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat)
        {
            int i = vertices.Count;
            vertices.Add(convert(a)); vertices.Add(convert(b)); vertices.Add(convert(c)); vertices.Add(convert(d));
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.one); uvs.Add(Vector2.up);
            int[] indices = mirrored ? new[] { i, i + 2, i + 1, i, i + 3, i + 2 } : new[] { i, i + 1, i + 2, i, i + 2, i + 3 };
            triangles[mat].AddRange(indices);
        }
        public void Box(Vector3 p, Vector3 size, int mat, float line)
        {
            Vector3 lo = p - size * .5f, hi = p + size * .5f;
            Vector3[] v = { new Vector3(lo.x,lo.y,lo.z),new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(lo.x,hi.y,lo.z),new Vector3(lo.x,lo.y,hi.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(lo.x,hi.y,hi.z) };
            int[,] faces = { {0,3,2,1},{4,5,6,7},{0,4,7,3},{1,2,6,5},{0,1,5,4},{3,7,6,2} };
            for (int i = 0; i < 6; i++) Face(v[faces[i,0]],v[faces[i,1]],v[faces[i,2]],v[faces[i,3]],mat);
            if (line <= 0) return;
            int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
            for (int i = 0; i < 12; i++)
            {
                Vector3 a = v[edges[i,0]], b = v[edges[i,1]], s = b-a;
                Box((a+b)*.5f, new Vector3(Mathf.Abs(s.x)+line,Mathf.Abs(s.y)+line,Mathf.Abs(s.z)+line),1,0);
            }
        }
        public void Profile(Vector2[] outline, float scale, float y, float z, float depth, float line)
        {
            Vector3 cf = new Vector3(0,y+scale*.4f,z+depth), cb = new Vector3(0,y+scale*.4f,z);
            for (int i=0;i<outline.Length;i++)
            {
                Vector2 a=outline[i]*scale, b=outline[(i+1)%outline.Length]*scale;
                Vector3 af=new Vector3(a.x,y+a.y,z+depth), bf=new Vector3(b.x,y+b.y,z+depth), ab=af-Vector3.forward*depth, bb=bf-Vector3.forward*depth;
                Face(cf,af,bf,bf,0); Face(cb,bb,ab,ab,0); Face(ab,bb,bf,af,0);
                Vector3 offset = new Vector3(-(bf-af).y,(bf-af).x,0).normalized*line*.5f;
                Face(af-offset+Vector3.forward*line,bf-offset+Vector3.forward*line,bf+offset+Vector3.forward*line,af+offset+Vector3.forward*line,1);
            }
        }
        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = "ComicShop_OuterFrame", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.subMeshCount=2;
            for(int i=0;i<2;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
