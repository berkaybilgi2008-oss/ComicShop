#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Unity.Netcode;
using UnityEngine;

public static class ComicShopBookSetup
{
    private const string ModelsRoot = "Assets/comics/models";
    private const string BaseBookPrefabPath = "Assets/Prefabs/Book.prefab";
    private const string PrefabsRoot = "Assets/Prefabs";
    private const string BookDataRoot = "Assets/BookData";
    private const string CatalogFolder = "Assets/Resources";
    private const string CatalogPath = "Assets/Resources/BookCatalog.asset";
    private const int BookLayer = 8;
    private static readonly string[] ExcludedBrands = { "ECLIPSE", "MEDIOCRE", "DULL" };
    private static readonly Quaternion StandardBookRotation = Quaternion.Euler(270f, 0f, 180f);

    [MenuItem("ComicShop/Build All Comic Book Catalog")]
    public static void Setup()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseBookPrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"ComicShop: Temel kitap prefab'i bulunamadi: {BaseBookPrefabPath}");
            return;
        }

        BookItem baseBookItem = basePrefab.GetComponent<BookItem>();
        Rigidbody baseRigidbody = basePrefab.GetComponent<Rigidbody>();
        EnsureFolder("Assets", "BookData");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Resources");

        NetworkPrefabsList networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        Dictionary<int, BookData> byId = LoadExistingBookData().GroupBy(x => x.BookID).ToDictionary(g => g.Key, g => g.First());
        HashSet<int> usedIds = new HashSet<int>(byId.Keys);
        int nextId = usedIds.Count == 0 ? 0 : usedIds.Max() + 1;
        List<BookData> allData = new List<BookData>();
        HashSet<int> seenIds = new HashSet<int>();

        string[] brandFolders = AssetDatabase.GetSubFolders(ModelsRoot)
            .Where(path => !ExcludedBrands.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (brandFolders.Length == 0)
        {
            Debug.LogError($"ComicShop: '{ModelsRoot}' altinda marka klasoru bulunamadi.");
            return;
        }

        foreach (string brandFolder in brandFolders)
        {
            string brandName = Path.GetFileName(brandFolder);
            string dataFolder = $"{BookDataRoot}/{Sanitize(brandName)}";
            string prefabFolder = $"{PrefabsRoot}/{Sanitize(brandName)}Books";
            EnsureFolder(BookDataRoot, Sanitize(brandName));
            EnsureFolder(PrefabsRoot, Sanitize(brandName) + "Books");

            List<BookData> brandExisting = LoadBookDataFromFolder(dataFolder);
            int brandId = brandExisting.Count > 0 ? brandExisting.Min(x => x.BrandID) : FindUnusedBrandId(allData);
            foreach (BookData existing in brandExisting)
                brandId = existing.BrandID;

            string[] modelPaths = AssetDatabase.FindAssets("t:Model", new[] { brandFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string modelPath in modelPaths)
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) continue;

                ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer != null && !Mathf.Approximately(importer.globalScale, 0.2f))
                {
                    importer.globalScale = 0.2f;
                    importer.SaveAndReimport();
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                }

                BookData existingData = FindExistingDataForModel(brandExisting, modelName);
                int bookId = existingData != null ? existingData.BookID : AllocateBookId(usedIds, ref nextId);
                usedIds.Add(bookId);

                if (existingData != null && existingData.bookPrefab != null)
                {
                    existingData.BrandID = brandId;
                    EditorUtility.SetDirty(existingData);
                    allData.Add(existingData);
                    seenIds.Add(bookId);
                    continue;
                }

                string safeName = Sanitize(modelName);
                string prefabPath = $"{prefabFolder}/Book_{bookId:000}_{safeName}.prefab";
                string dataPath = $"{dataFolder}/BookData_{bookId:000}_{safeName}.asset";

                GameObject bookRoot = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (bookRoot == null) continue;
                bookRoot.name = $"Book_{bookId:000}_{modelName}";
                bookRoot.layer = BookLayer;

                Quaternion nativeRotation = bookRoot.transform.localRotation;
                Vector3 originalLocalPosition = bookRoot.transform.localPosition;
                Vector3 originalLocalScale = bookRoot.transform.localScale;
                Renderer coverRenderer = bookRoot.GetComponentInChildren<Renderer>(true);
                if (coverRenderer == null)
                {
                    Debug.LogError($"ComicShop: '{modelName}' modelinde Renderer bulunamadi.");
                    UnityEngine.Object.DestroyImmediate(bookRoot);
                    continue;
                }

                BookItem bookItem = bookRoot.AddComponent<BookItem>();
                bookItem.bookID = bookId;
                bookItem.brandID = brandId;
                bookItem.coverRenderer = coverRenderer;
                bookItem.nativeRotation = nativeRotation;
                bookItem.baseRotationEuler = (Quaternion.Inverse(nativeRotation) * StandardBookRotation).eulerAngles;
                if (baseBookItem != null)
                {
                    bookItem.outlineMaterial = baseBookItem.outlineMaterial;
                    bookItem.outlineScale = baseBookItem.outlineScale;
                }

                bookRoot.transform.localPosition = originalLocalPosition;
                bookRoot.transform.localRotation = nativeRotation;
                bookRoot.transform.localScale = originalLocalScale;

                Bounds bounds = CalculateExactLocalBounds(bookRoot);
                BoxCollider collider = bookRoot.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;

                Rigidbody rb = bookRoot.AddComponent<Rigidbody>();
                if (baseRigidbody != null)
                {
                    rb.mass = baseRigidbody.mass;
                    rb.linearDamping = baseRigidbody.linearDamping;
                    rb.angularDamping = baseRigidbody.angularDamping;
                    rb.useGravity = baseRigidbody.useGravity;
                    rb.isKinematic = baseRigidbody.isKinematic;
                    rb.interpolation = baseRigidbody.interpolation;
                    rb.collisionDetectionMode = baseRigidbody.collisionDetectionMode;
                    rb.constraints = baseRigidbody.constraints;
                }
                else
                {
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }

                NetworkObject networkObject = bookRoot.AddComponent<NetworkObject>();
                networkObject.AutoObjectParentSync = false;
                networkObject.AlwaysReplicateAsRoot = true;
                networkObject.DontDestroyWithOwner = true;
                bookRoot.AddComponent<NetworkBook>();
                PrefabUtility.SaveAsPrefabAsset(bookRoot, prefabPath);
                UnityEngine.Object.DestroyImmediate(bookRoot);

                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (networkPrefabs != null && savedPrefab != null && !networkPrefabs.Contains(savedPrefab))
                    networkPrefabs.Add(new NetworkPrefab { Prefab = savedPrefab });

                BookData asset = ScriptableObject.CreateInstance<BookData>();
                asset.BookID = bookId;
                asset.BrandID = brandId;
                asset.bookPrefab = savedPrefab;
                AssetDatabase.CreateAsset(asset, dataPath);
                allData.Add(asset);
                seenIds.Add(bookId);
            }
        }

        allData = allData.Where(x => x != null && x.bookPrefab != null)
            .GroupBy(x => x.BookID)
            .Select(g => g.First())
            .OrderBy(x => x.BookID)
            .ToList();

        BookCatalog catalog = AssetDatabase.LoadAssetAtPath<BookCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BookCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.books = allData.ToArray();
        EditorUtility.SetDirty(catalog);

        if (networkPrefabs != null) EditorUtility.SetDirty(networkPrefabs);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ComicShop: Catalog hazir. {allData.Count} farkli kitap bulundu/olusturuldu. Her kitap BookID + BrandID ile tasiniyor; kopya sayisi runtime'da 10.");
    }

    private static List<BookData> LoadExistingBookData()
    {
        return AssetDatabase.FindAssets("t:BookData", new[] { BookDataRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BookData>)
            .Where(x => x != null)
            .ToList();
    }

    private static List<BookData> LoadBookDataFromFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return new List<BookData>();
        return AssetDatabase.FindAssets("t:BookData", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BookData>)
            .Where(x => x != null)
            .ToList();
    }

    private static BookData FindExistingDataForModel(List<BookData> data, string modelName)
    {
        string safeName = Sanitize(modelName);
        return data.FirstOrDefault(x => x.bookPrefab != null &&
            (x.bookPrefab.name.EndsWith("_" + modelName, StringComparison.OrdinalIgnoreCase) ||
             x.bookPrefab.name.EndsWith("_" + safeName, StringComparison.OrdinalIgnoreCase)));
    }

    private static int AllocateBookId(HashSet<int> used, ref int nextId)
    {
        while (used.Contains(nextId)) nextId++;
        return nextId++;
    }

    private static int FindUnusedBrandId(List<BookData> current)
    {
        HashSet<int> used = new HashSet<int>(current.Where(x => x != null).Select(x => x.BrandID));
        int id = 0;
        while (used.Contains(id)) id++;
        return id;
    }

    private static Bounds CalculateExactLocalBounds(GameObject root)
    {
        bool initialized = false;
        Bounds bounds = new Bounds();
        Transform rootTransform = root.transform;
        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) continue;
            Bounds meshBounds = mesh.bounds;
            Vector3 e = meshBounds.extents;
            Vector3 c = meshBounds.center;
            Vector3[] corners =
            {
                c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3(e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, -e.z), c + new Vector3(e.x, e.y, e.z)
            };
            foreach (Vector3 corner in corners)
                EncapsulateWorldPoint(ref bounds, ref initialized, rootTransform, meshFilter.transform.TransformPoint(corner));
        }
        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private static void EncapsulateWorldPoint(ref Bounds bounds, ref bool initialized, Transform root, Vector3 worldPoint)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!initialized) { bounds = new Bounds(localPoint, Vector3.zero); initialized = true; }
        else bounds.Encapsulate(localPoint);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c.ToString(), "_");
        return value.Replace("/", "_").Replace("\\", "_");
    }
}
#endif