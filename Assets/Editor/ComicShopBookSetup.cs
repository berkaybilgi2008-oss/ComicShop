#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

public static class ComicShopBookSetup
{
    private const string ModelsRoot = "Assets/comics/models";
    private const string BaseBookPrefabPath = "Assets/Prefabs/Book.prefab";
    private const string PrefabsRoot = "Assets/Prefabs";
    private const string BookDataRoot = "Assets/BookData";
    private const string CatalogPath = "Assets/Resources/BookCatalog.asset";
    private const int BookLayer = 8;
    private static readonly string[] ExcludedBrands = { "ECLIPSE", "MEDIOCRE", "DULL" };
    private static readonly Quaternion StandardBookRotation = Quaternion.Euler(270f, 0f, 180f);

    [MenuItem("ComicShop/Build All Comic Book Catalog")]
    public static void Setup()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseBookPrefabPath);
        if (basePrefab == null) { Debug.LogError($"ComicShop: Base prefab bulunamadi: {BaseBookPrefabPath}"); return; }

        BookItem baseBookItem = basePrefab.GetComponent<BookItem>();
        Rigidbody baseRigidbody = basePrefab.GetComponent<Rigidbody>();
        EnsureFolder("Assets", "BookData"); EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets", "Resources");

        NetworkPrefabsList networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        List<BookData> existingData = LoadExistingBookData();
        HashSet<int> usedIds = new HashSet<int>(existingData.Select(x => x.BookID));
        int nextId = usedIds.Count == 0 ? 0 : usedIds.Max() + 1;
        List<BookData> allData = new List<BookData>();

        string[] brandFolders = AssetDatabase.GetSubFolders(ModelsRoot)
            .Where(path => !ExcludedBrands.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (string brandFolder in brandFolders)
        {
            string brandName = Path.GetFileName(brandFolder);
            string dataFolder = $"{BookDataRoot}/{Sanitize(brandName)}";
            string prefabFolder = $"{PrefabsRoot}/{Sanitize(brandName)}Books";
            EnsureFolder(BookDataRoot, Sanitize(brandName)); EnsureFolder(PrefabsRoot, Sanitize(brandName) + "Books");

            List<BookData> brandExisting = LoadBookDataFromFolder(dataFolder);
            int brandId = brandExisting.Count > 0 ? brandExisting[0].BrandID : FindUnusedBrandId(existingData);

            string[] modelPaths = AssetDatabase.FindAssets("t:Model", new[] { brandFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase).ToArray();

            foreach (string modelPath in modelPaths)
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) continue;

                // IMPORTANT: never modify ModelImporter.globalScale or the model's transform.
                BookData data = FindExistingDataForModel(brandExisting, modelName);
                if (data != null && data.bookPrefab != null)
                {
                    data.BrandID = brandId;
                    EditorUtility.SetDirty(data);
                    allData.Add(data);
                    continue;
                }

                int bookId = AllocateBookId(usedIds, ref nextId);
                string safeName = Sanitize(modelName);
                string prefabPath = $"{prefabFolder}/Book_{bookId:000}_{safeName}.prefab";
                string dataPath = $"{dataFolder}/BookData_{bookId:000}_{safeName}.asset";

                GameObject bookRoot = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (bookRoot == null) continue;
                bookRoot.name = $"Book_{bookId:000}_{modelName}";
                bookRoot.layer = BookLayer;

                Vector3 originalScale = bookRoot.transform.localScale;
                Quaternion originalRotation = bookRoot.transform.localRotation;
                Vector3 originalPosition = bookRoot.transform.localPosition;
                Renderer coverRenderer = bookRoot.GetComponentInChildren<Renderer>(true);
                if (coverRenderer == null) { UnityEngine.Object.DestroyImmediate(bookRoot); continue; }

                BookItem item = bookRoot.AddComponent<BookItem>();
                item.bookID = bookId; item.brandID = brandId; item.coverRenderer = coverRenderer;
                item.nativeRotation = originalRotation;
                item.baseRotationEuler = (Quaternion.Inverse(originalRotation) * StandardBookRotation).eulerAngles;
                if (baseBookItem != null) { item.outlineMaterial = baseBookItem.outlineMaterial; item.outlineScale = baseBookItem.outlineScale; }

                // Restore the exact imported transform; no size adjustment.
                bookRoot.transform.localPosition = originalPosition;
                bookRoot.transform.localRotation = originalRotation;
                bookRoot.transform.localScale = originalScale;

                Bounds bounds = CalculateExactLocalBounds(bookRoot);
                BoxCollider collider = bookRoot.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                Rigidbody rb = bookRoot.AddComponent<Rigidbody>();
                if (baseRigidbody != null)
                {
                    rb.mass = baseRigidbody.mass; rb.linearDamping = baseRigidbody.linearDamping; rb.angularDamping = baseRigidbody.angularDamping;
                    rb.useGravity = baseRigidbody.useGravity; rb.isKinematic = baseRigidbody.isKinematic; rb.interpolation = baseRigidbody.interpolation;
                    rb.collisionDetectionMode = baseRigidbody.collisionDetectionMode; rb.constraints = baseRigidbody.constraints;
                }
                else { rb.useGravity = true; rb.collisionDetectionMode = CollisionDetectionMode.Continuous; }

                NetworkObject networkObject = bookRoot.AddComponent<NetworkObject>();
                networkObject.AutoObjectParentSync = false; networkObject.AlwaysReplicateAsRoot = true; networkObject.DontDestroyWithOwner = true;
                bookRoot.AddComponent<NetworkBook>();
                PrefabUtility.SaveAsPrefabAsset(bookRoot, prefabPath);
                UnityEngine.Object.DestroyImmediate(bookRoot);

                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (networkPrefabs != null && savedPrefab != null && !networkPrefabs.Contains(savedPrefab)) networkPrefabs.Add(new NetworkPrefab { Prefab = savedPrefab });
                BookData asset = ScriptableObject.CreateInstance<BookData>(); asset.BookID = bookId; asset.BrandID = brandId; asset.bookPrefab = savedPrefab;
                AssetDatabase.CreateAsset(asset, dataPath); allData.Add(asset); existingData.Add(asset); brandExisting.Add(asset);
            }
        }

        allData = allData.Where(x => x != null && x.bookPrefab != null).GroupBy(x => x.BookID).Select(g => g.First()).OrderBy(x => x.BookID).ToList();
        BookCatalog catalog = AssetDatabase.LoadAssetAtPath<BookCatalog>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<BookCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        catalog.books = allData.ToArray(); EditorUtility.SetDirty(catalog);
        if (networkPrefabs != null) EditorUtility.SetDirty(networkPrefabs);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        Debug.Log($"ComicShop: {allData.Count} kitap kataloglandi. MODEL SCALE DEGISTIRILMEDI. Her kitap runtime'da 10 kopya.");
    }

    private static List<BookData> LoadExistingBookData() => AssetDatabase.FindAssets("t:BookData", new[] { BookDataRoot }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BookData>).Where(x => x != null).ToList();
    private static List<BookData> LoadBookDataFromFolder(string folder) => !AssetDatabase.IsValidFolder(folder) ? new List<BookData>() : AssetDatabase.FindAssets("t:BookData", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BookData>).Where(x => x != null).ToList();
    private static BookData FindExistingDataForModel(List<BookData> data, string modelName) => data.FirstOrDefault(x => x.bookPrefab != null && (x.bookPrefab.name.EndsWith("_" + modelName, StringComparison.OrdinalIgnoreCase) || x.bookPrefab.name.EndsWith("_" + Sanitize(modelName), StringComparison.OrdinalIgnoreCase)));
    private static int AllocateBookId(HashSet<int> used, ref int next) { while (used.Contains(next)) next++; int result = next++; used.Add(result); return result; }
    private static int FindUnusedBrandId(List<BookData> data) { HashSet<int> used = new HashSet<int>(data.Select(x => x.BrandID)); int id = 0; while (used.Contains(id)) id++; return id; }

    private static Bounds CalculateExactLocalBounds(GameObject root)
    {
        bool initialized = false; Bounds bounds = new Bounds(); Transform rootTransform = root.transform;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh; if (mesh == null) continue; Bounds b = mesh.bounds; Vector3 e = b.extents, c = b.center;
            foreach (Vector3 p in GetCorners(c, e)) EncapsulateWorldPoint(ref bounds, ref initialized, rootTransform, filter.transform.TransformPoint(p));
        }
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Bounds b = renderer.bounds; foreach (Vector3 p in GetCorners(b.center, b.extents)) EncapsulateWorldPoint(ref bounds, ref initialized, rootTransform, p);
        }
        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }
    private static IEnumerable<Vector3> GetCorners(Vector3 c, Vector3 e)
    {
        yield return c + new Vector3(-e.x, -e.y, -e.z); yield return c + new Vector3(-e.x, -e.y, e.z); yield return c + new Vector3(-e.x, e.y, -e.z); yield return c + new Vector3(-e.x, e.y, e.z);
        yield return c + new Vector3(e.x, -e.y, -e.z); yield return c + new Vector3(e.x, -e.y, e.z); yield return c + new Vector3(e.x, e.y, -e.z); yield return c + new Vector3(e.x, e.y, e.z);
    }
    private static void EncapsulateWorldPoint(ref Bounds bounds, ref bool initialized, Transform root, Vector3 point)
    {
        Vector3 local = root.InverseTransformPoint(point); if (!initialized) { bounds = new Bounds(local, Vector3.zero); initialized = true; } else bounds.Encapsulate(local);
    }
    private static void EnsureFolder(string parent, string child) { string path = $"{parent}/{child}"; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child); }
    private static string Sanitize(string value) { foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c.ToString(), "_"); return value.Replace("/", "_").Replace("\\", "_"); }
}
#endif