using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ComicShop.Rendering.Editor
{
    public static class BakeToonNormals
    {
        const string Folder = "Assets/ComicShopToon/BakedMeshes";

        [MenuItem("Tools/ComicShop/Bake Outline Normals For Selection")]
        static void BakeSelection()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/ComicShopToon", "BakedMeshes");
            var meshes = new HashSet<Mesh>(Selection.GetFiltered<Mesh>(SelectionMode.DeepAssets));
            var renderers = new HashSet<Component>();
            foreach (GameObject go in Selection.gameObjects)
            {
                foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null) { meshes.Add(f.sharedMesh); renderers.Add(f); }
                foreach (SkinnedMeshRenderer s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s.sharedMesh != null) { meshes.Add(s.sharedMesh); renderers.Add(s); }
            }
            var copies = new Dictionary<Mesh, Mesh>();
            foreach (Mesh original in meshes)
            {
                if (!original.isReadable)
                {
                    Debug.LogError($"Enable Read/Write on the Model Importer before baking: {original.name}");
                    continue;
                }
                Mesh copy = Object.Instantiate(original);
                copy.name = original.name + "_ToonNormals";
                Vector3[] vertices = copy.vertices;
                if (copy.normals.Length != vertices.Length) copy.RecalculateNormals();
                if (copy.tangents.Length != vertices.Length && copy.uv.Length == vertices.Length)
                    copy.RecalculateTangents();
                Vector3[] normals = copy.normals;
                Vector4[] tangents = copy.tangents;
                if (tangents.Length != vertices.Length)
                {
                    Debug.LogError($"Mesh needs UV0 and valid tangents: {original.name}");
                    Object.DestroyImmediate(copy);
                    continue;
                }
                var sums = new Dictionary<Vector3Int, Vector3>();
                Vector3Int Key(Vector3 p) => new Vector3Int(
                    Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000),
                    Mathf.RoundToInt(p.z * 100000));
                int[] triangles = copy.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    Vector3 weighted = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    Add(a, weighted); Add(b, weighted); Add(c, weighted);
                }
                void Add(int index, Vector3 value)
                {
                    Vector3Int key = Key(vertices[index]);
                    sums.TryGetValue(key, out Vector3 old);
                    sums[key] = old + value;
                }
                var colors = new Color[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 n = normals[i].normalized;
                    Vector3 t = ((Vector3)tangents[i]).normalized;
                    Vector3 b = Vector3.Cross(n, t) * tangents[i].w;
                    sums.TryGetValue(Key(vertices[i]), out Vector3 smooth);
                    smooth = smooth.sqrMagnitude > 1e-12f ? smooth.normalized : n;
                    Vector3 ts = new Vector3(Vector3.Dot(smooth, t), Vector3.Dot(smooth, b), Vector3.Dot(smooth, n));
                    colors[i] = new Color(ts.x * .5f + .5f, ts.y * .5f + .5f, ts.z * .5f + .5f, 1);
                }
                copy.colors = colors;
                // Extra hull geometry must remain inside conservative culling bounds.
                Bounds bounds = copy.bounds;
                bounds.Expand(1f);
                copy.bounds = bounds;
                string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/ToonMesh.asset");
                AssetDatabase.CreateAsset(copy, path);
                copies.Add(original, copy);
                Debug.Log($"Baked {original.name} -> {path}. Source asset was preserved; clone color channel contains tangent-space normals.");
            }
            foreach (Component component in renderers)
            {
                Mesh source = component is MeshFilter f ? f.sharedMesh : ((SkinnedMeshRenderer)component).sharedMesh;
                if (!copies.TryGetValue(source, out Mesh copy)) continue;
                Undo.RecordObject(component, "Assign Toon Normal Mesh");
                if (component is MeshFilter filter) filter.sharedMesh = copy;
                else
                {
                    var skin = (SkinnedMeshRenderer)component;
                    skin.sharedMesh = copy;
                    Bounds b = skin.localBounds; b.Expand(1f); skin.localBounds = b;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
