using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kitap modelleri tek parca kutu: kapak resmi yan yuzlere de gerilip koyu bir serit olarak
/// gorunuyordu. Ust uste yiginda bu koyu yanlar kitaplar arasinda BOSLUK varmis gibi duruyordu.
/// Bu sinif her kitabin mesh'ini (kopya; asset degismez) ikiye ayirir:
///   * kapak + arka kapak + sirt -> kitabin kendi kapak malzemesi
///   * diger uc yan yuz          -> krem renkli, ince sayfa cizgili "kagit" malzemesi
/// Boylece yigin gercek kitaplar gibi gorunur. Ayni kaynak mesh tum kitaplarda paylasilir.
/// </summary>
public static class BookPaperEdges
{
    const string Suffix = "_Pages";
    static readonly Dictionary<(Mesh, int, int), Mesh> meshes = new Dictionary<(Mesh, int, int), Mesh>();
    static Material paper;
    static Texture2D pageTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        meshes.Clear();
        paper = null;
        pageTexture = null;
    }

    public static void Apply(BookItem book)
    {
        if (book == null) return;
        try
        {
            var renderer = book.coverRenderer as MeshRenderer;
            if (renderer == null) renderer = book.GetComponentInChildren<MeshRenderer>();
            if (renderer == null) return;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            Mesh source = filter.sharedMesh;
            if (source.name.EndsWith(Suffix) || !source.isReadable || source.subMeshCount != 1) return;

            Vector3 scale = renderer.transform.lossyScale;
            Vector3 ext = Vector3.Scale(source.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int thin = 0, longest = 0;
            for (int i = 1; i < 3; i++) { if (ext[i] < ext[thin]) thin = i; if (ext[i] > ext[longest]) longest = i; }
            if (thin == longest) return;
            int wide = 3 - thin - longest;

            var key = (source, thin, wide);
            if (!meshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = Build(source, thin, wide);
                meshes[key] = mesh;
            }
            if (mesh == null) return;
            var slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0 || slots[0] == null) return;
            var material = PaperMaterial(slots[0]);
            if (material == null) return;
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { slots[0], material };
        }
        catch (System.Exception error)
        {
            Debug.LogWarning($"BookPaperEdges: {book.name} icin sayfa kenari uygulanamadi: {error.Message}");
        }
    }

    static Mesh Build(Mesh source, int thin, int wide)
    {
        var positions = new List<Vector3>(source.vertices);
        var normals = new List<Vector3>(source.normals);
        var tangents = new List<Vector4>(source.tangents);
        var uvs = new List<Vector2>(source.uv);
        bool hasNormals = normals.Count == positions.Count, hasTangents = tangents.Count == positions.Count, hasUv = uvs.Count == positions.Count;
        if (!hasUv) { uvs.Clear(); for (int i = 0; i < positions.Count; i++) uvs.Add(Vector2.zero); hasUv = true; }
        int[] tris = source.triangles;
        Bounds b = source.bounds;
        var cover = new List<int>(tris.Length);
        var pages = new List<int>(tris.Length);
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            Vector3 n = Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]);
            int axis = Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z) ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);
            // Kapak/arka kapak ve bir uzun kenar (sirt) kapak malzemesinde kalir.
            if (axis == thin || (axis == wide && n[wide] < 0f)) { cover.Add(i0); cover.Add(i1); cover.Add(i2); continue; }
            // Sayfa yuzu: kose paylasimi kapagi bozmasin diye kendi kopya koseleri + sayfa UV'si.
            int along = 3 - thin - axis;
            for (int c = 0; c < 3; c++)
            {
                int src = tris[t + c];
                Vector3 p = positions[src];
                float u = b.size[along] > 1e-8f ? (p[along] - b.min[along]) / b.size[along] : 0f;
                float v = b.size[thin] > 1e-8f ? (p[thin] - b.min[thin]) / b.size[thin] : 0f;
                pages.Add(positions.Count);
                positions.Add(p);
                if (hasNormals) normals.Add(normals[src]);
                if (hasTangents) tangents.Add(tangents[src]);
                uvs.Add(new Vector2(u, v));
            }
        }
        if (pages.Count == 0) return null;
        var mesh = new Mesh { name = source.name + Suffix };
        if (positions.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(positions);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasTangents) mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(cover, 0);
        mesh.SetTriangles(pages, 1);
        mesh.bounds = source.bounds;
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.UploadMeshData(false);
        return mesh;
    }

    static Material PaperMaterial(Material template)
    {
        if (paper != null) return paper;
        if (pageTexture == null)
        {
            // Dikey: sayfa katmanlari (ince cizgiler); ust/alt kenarda karton kapak payi.
            const int H = 64;
            pageTexture = new Texture2D(4, H, TextureFormat.RGBA32, false) { name = "BookPageEdges", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var cream = new Color(0.96f, 0.92f, 0.82f);
            var line = new Color(0.82f, 0.76f, 0.64f);
            var board = new Color(0.55f, 0.47f, 0.40f);
            var pixels = new Color[4 * H];
            for (int y = 0; y < H; y++)
            {
                Color c = (y < 3 || y >= H - 3) ? board : (y % 5 == 0 ? line : Color.Lerp(cream, line, ((y * 37) % 11) / 40f));
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = c;
            }
            pageTexture.SetPixels(pixels);
            pageTexture.Apply(false, true);
        }
        // Kapak malzemesinden kopya: ayni (build'e dahil) shader ve toon ayarlari, sadece doku/renk farkli.
        paper = new Material(template) { name = "BookPaperEdge" };
        if (paper.HasProperty("_BaseMap")) { paper.SetTexture("_BaseMap", pageTexture); paper.SetTextureScale("_BaseMap", Vector2.one); paper.SetTextureOffset("_BaseMap", Vector2.zero); }
        if (paper.HasProperty("_MainTex")) { paper.SetTexture("_MainTex", pageTexture); paper.SetTextureScale("_MainTex", Vector2.one); paper.SetTextureOffset("_MainTex", Vector2.zero); }
        if (paper.HasProperty("_BaseColor")) paper.SetColor("_BaseColor", Color.white);
        if (paper.HasProperty("_Color")) paper.SetColor("_Color", Color.white);
        return paper;
    }
}
