using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ComicShop.Rendering.Editor
{
    public static class BakeToonNormals
    {
        const string Folder = "Assets/ComicShopToon/BakedMeshes";
        const string HullName = "ToonHull";
        [MenuItem("Tools/ComicShop/Step 2/Bake and Install Hero Outlines on Selection")]
        public static void BakeSelection()
        {
            if (Application.isPlaying) return;
            var selected = new List<GameObject>();
            foreach (var go in Selection.gameObjects) if (go.scene.IsValid()) selected.Add(go);
            var roots = selected.ToArray();
            if (roots.Length == 0) { Debug.LogWarning("Select hero roots in the Hierarchy: mascot, counter, cash register."); return; }
            var importers = new Dictionary<string, bool>();
            foreach (var root in roots)
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                Mesh mesh = GetMesh(renderer);
                if (mesh == null || renderer.GetComponent<ToonHullBinding>() != null) continue;
                string path = AssetDatabase.GetAssetPath(mesh);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer) importers[path] = importer.isReadable;
            }
            int installed = 0, retired = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install hero outlines");
            try
            {
                // Temporary importer changes are batched per model, never per object.
                foreach (var item in importers)
                    if (!item.Value && AssetImporter.GetAtPath(item.Key) is ModelImporter importer)
                    { importer.isReadable = true; importer.SaveAndReimport(); }
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ComicShopToon", "BakedMeshes");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ComicShopToon/Resources/ComicShopToon/ToonHull.mat");
                if (material == null) throw new InvalidOperationException("ToonHull.mat is missing.");
                var copies = new Dictionary<Mesh, Mesh>();
                var processed = new HashSet<Renderer>();
                foreach (GameObject root in roots)
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    if (!processed.Add(renderer) || renderer.GetComponent<ToonHullBinding>() != null) continue;
                    // Never generate 3,600 book hulls when a shop root was selected by mistake.
                    if (renderer.GetComponentInParent<BookItem>() != null) continue;
                    if (IsLegacyOutline(renderer))
                    {
                        Undo.RecordObject(renderer, "Disable legacy hero outline");
                        renderer.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        EditorUtility.SetDirty(renderer); retired++;
                        continue;
                    }
                    Mesh source = GetMesh(renderer);
                    if (source == null) continue;
                    if (!copies.TryGetValue(source, out Mesh copy))
                    {
                        copy = Bake(source);
                        if (copy == null) continue;
                        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(Folder + "/HeroOutlineMesh.asset"));
                        copies.Add(source, copy);
                    }
                    Transform old = renderer.transform.Find(HullName);
                    if (old != null && old.GetComponent<ToonHullBinding>() != null) Undo.DestroyObjectImmediate(old.gameObject);
                    var go = new GameObject(HullName);
                    Undo.RegisterCreatedObjectUndo(go, "Create hero hull");
                    go.transform.SetParent(renderer.transform, false);
                    go.layer = renderer.gameObject.layer;
                    Renderer hull;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        var s = go.AddComponent<SkinnedMeshRenderer>();
                        s.sharedMesh = copy; s.bones = skin.bones; s.rootBone = skin.rootBone;
                        s.localBounds = skin.localBounds; s.updateWhenOffscreen = skin.updateWhenOffscreen;
                        hull = s;
                    }
                    else
                    {
                        go.AddComponent<MeshFilter>().sharedMesh = copy;
                        hull = go.AddComponent<MeshRenderer>();
                    }
                    var slots = new Material[copy.subMeshCount];
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    hull.sharedMaterials = slots;
                    hull.shadowCastingMode = ShadowCastingMode.Off;
                    hull.receiveShadows = false;
                    hull.lightProbeUsage = LightProbeUsage.Off;
                    hull.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    hull.enabled = renderer.enabled;
                    go.AddComponent<ToonHullBinding>().source = renderer;
                    EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                    installed++;
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                foreach (var item in importers)
                    if (AssetImporter.GetAtPath(item.Key) is ModelImporter importer && importer.isReadable != item.Value)
                    { importer.isReadable = item.Value; importer.SaveAndReimport(); }
                Undo.CollapseUndoOperations(group);
            }
            Debug.Log($"Hero outlines: installed/rebuilt {installed} renderers; disabled {retired} legacy outline renderers. Source meshes/colors/UVs preserved; UV3 on hull copies holds tangent-space smoothed normals. Importer Read/Write flags restored. Undo restores scene objects; generated mesh assets remain.");
        }
        static bool IsLegacyOutline(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) return false;
            foreach (var material in materials)
            {
                if (material == null || material.shader == null) return false;
                string name = material.shader.name;
                if (name != "ToastRanger/Outline URP" && name != "Custom/OutlineOnly" && name != "ComicShop/ToonOutline") return false;
            }
            return true;
        }
        static Mesh GetMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer skin) return skin.sharedMesh;
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter)) return filter.sharedMesh;
            return null;
        }
        static Mesh Bake(Mesh source)
        {
            if (!source.isReadable) { Debug.LogError($"Cannot read non-model mesh {source.name}; no outline generated.", source); return null; }
            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + "_HullNormals";
            Vector3[] v = copy.vertices;
            if (copy.normals.Length != v.Length) copy.RecalculateNormals();
            if (copy.tangents.Length != v.Length && copy.uv.Length == v.Length) copy.RecalculateTangents();
            var n = copy.normals; var t = copy.tangents;
            if (t.Length != v.Length)
            {
                t = new Vector4[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 tangent = Vector3.Cross(Mathf.Abs(n[i].y) < 0.9f ? Vector3.up : Vector3.right, n[i]).normalized;
                    t[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1);
                }
                copy.tangents = t;
            }
            float epsilon = Mathf.Max(1e-6f, copy.bounds.size.magnitude * 1e-6f);
            Vector3Int Key(int i) => new Vector3Int(Mathf.RoundToInt(v[i].x / epsilon), Mathf.RoundToInt(v[i].y / epsilon), Mathf.RoundToInt(v[i].z / epsilon));
            var sums = new Dictionary<Vector3Int, Vector3>();
            void Add(int i, Vector3 face) { var key = Key(i); sums.TryGetValue(key, out var sum); sums[key] = sum + face; }
            for (int sub = 0; sub < copy.subMeshCount; sub++)
            {
                if (copy.GetTopology(sub) != MeshTopology.Triangles) continue;
                var indices = copy.GetTriangles(sub);
                for (int j = 0; j < indices.Length; j += 3)
                {
                    int a = indices[j], b = indices[j + 1], c = indices[j + 2];
                    Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
                    Add(a, face * Vector3.Angle(v[b] - v[a], v[c] - v[a]));
                    Add(b, face * Vector3.Angle(v[a] - v[b], v[c] - v[b]));
                    Add(c, face * Vector3.Angle(v[a] - v[c], v[b] - v[c]));
                }
            }
            var smooth = new List<Vector4>(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                sums.TryGetValue(Key(i), out Vector3 normal);
                if (normal.sqrMagnitude < 1e-12f) normal = n[i];
                normal.Normalize();
                Vector3 tangent = ((Vector3)t[i]).normalized;
                Vector3 bitangent = Vector3.Cross(n[i].normalized, tangent) * t[i].w;
                smooth.Add(new Vector4(Vector3.Dot(normal, tangent), Vector3.Dot(normal, bitangent), Vector3.Dot(normal, n[i].normalized), 1));
            }
            copy.SetUVs(2, smooth); // UV3/TEXCOORD2. Only the separate hull mesh is changed.
            return copy;
        }
    }
}
