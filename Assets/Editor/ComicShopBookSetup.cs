#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ComicShopBookSetup
{
    private const string ModelRoot = "Assets/comics/models";
    private const string BaseBookPrefabPath = "Assets/Prefabs/Book.prefab";
    private const string GeneratedPrefabRoot = "Assets/Prefabs/Books";
    private const string GeneratedDataRoot = "Assets/BookData/Brands";
    private const string LegacyVeridianPrefabRoot = "Assets/Prefabs/VeridianBooks";
    private const string LegacyVeridianDataRoot = "Assets/BookData/VERIDIAN";
    private const string BrandCatalogPath = "Assets/Resources/BrandCatalog.asset";
    private const int BookLayer = 8;

    private static readonly Quaternion StandardBookRotation = Quaternion.Euler(270f, 0f, 180f);

    private sealed class BrandSource
    {
        public string Name;
        public string FolderPath;
        public int BrandID;
        public List<string> ModelPaths = new List<string>();
    }

    [MenuItem("ComicShop/Setup ALL Book Models")]
    public static void SetupAll()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseBookPrefabPath);
        BookItem baseBookItem = basePrefab != null ? basePrefab.GetComponent<BookItem>() : null;
        Rigidbody baseRigidbody = basePrefab != null ? basePrefab.GetComponent<Rigidbody>() : null;

        if (basePrefab == null)
        {
            Debug.LogError("ComicShop: Temel kitap prefab'i bulunamadi: " + BaseBookPrefabPath);
            return;
        }

        string[] brandFolders = AssetDatabase.GetSubFolders(ModelRoot)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (brandFolders.Length == 0)
        {
            Debug.LogError("ComicShop: " + ModelRoot + " altinda marka klasoru bulunamadi.");
            return;
        }

        BrandCatalog catalog = LoadOrCreateCatalog();
        var existingIDs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int nextBrandID = 0;

        if (catalog.brands != null)
        {
            foreach (var entry in catalog.brands)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.brandName))
                    continue;

                existingIDs[entry.brandName] = entry.brandID;
                nextBrandID = Mathf.Max(nextBrandID, entry.brandID + 1);
            }
        }

        var brands = new List<BrandSource>();
        foreach (string folder in brandFolders)
        {
            string brandName = Path.GetFileName(folder);
            if (!existingIDs.TryGetValue(brandName, out int brandID))
            {
                brandID = nextBrandID++;
                existingIDs.Add(brandName, brandID);
            }

            string[] models = AssetDatabase.FindAssets("t:Model", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
                .ToList()
                .ToArray();

            brands.Add(new BrandSource
            {
                Name = brandName,
                FolderPath = folder,
                BrandID = brandID,
                ModelPaths = models.ToList()
            });
        }

        brands = brands
            .OrderBy(b => b.BrandID)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ResetGeneratedOutput();
        EnsureFolderPath("Assets/Resources");
        EnsureFolderPath(GeneratedPrefabRoot);
        EnsureFolderPath(GeneratedDataRoot);

        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (networkPrefabs != null)
        {
            foreach (var entry in networkPrefabs.PrefabList.ToArray())
            {
                if (entry.Prefab == null)
                {
                    networkPrefabs.Remove(entry);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(entry.Prefab);
                if (IsGeneratedPrefabPath(path))
                    networkPrefabs.Remove(entry);
            }
        }

        var allData = new List<BookData>();
        var catalogEntries = new List<BrandCatalog.Entry>();
        int nextBookID = 0;
        int totalModels = 0;
        int generatedModels = 0;

        foreach (BrandSource brand in brands)
        {
            EnsureFolderPath(GeneratedPrefabRoot + "/" + brand.Name);
            EnsureFolderPath(GeneratedDataRoot + "/" + brand.Name);

            int generatedForBrand = 0;

            foreach (string modelPath in brand.ModelPaths)
            {
                totalModels++;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                {
                    Debug.LogError("ComicShop: Model yuklenemedi: " + modelPath);
                    continue;
                }

                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                string safeName = Sanitize(modelName);
                string prefabPath = GeneratedPrefabRoot + "/" + brand.Name + "/Book_" + nextBookID.ToString("000") + "_" + safeName + ".prefab";
                string dataPath = GeneratedDataRoot + "/" + brand.Name + "/BookData_" + nextBookID.ToString("000") + "_" + safeName + ".asset";

                GameObject bookRoot = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (bookRoot == null)
                {
                    Debug.LogError("ComicShop: Prefab instance olusturulamadi: " + modelPath);
                    continue;
                }

                try
                {
                    bookRoot.name = "Book_" + nextBookID.ToString("000") + "_" + modelName;
                    SetLayerRecursively(bookRoot, BookLayer);

                    Vector3 originalLocalScale = bookRoot.transform.localScale;
                    Quaternion nativeRotation = bookRoot.transform.localRotation;
                    Vector3 originalLocalPosition = bookRoot.transform.localPosition;

                    Renderer coverRenderer = bookRoot.GetComponentInChildren<Renderer>(true);
                    if (coverRenderer == null)
                    {
                        Debug.LogError("ComicShop: '" + modelName + "' modelinde Renderer bulunamadi; atlandi.");
                        continue;
                    }

                    BookItem bookItem = bookRoot.AddComponent<BookItem>();
                    BookDisplayName displayName = bookRoot.AddComponent<BookDisplayName>();
                    displayName.SetName(bookRoot.name);
                    bookRoot.AddComponent<BookToonEffect>();
                    bookItem.bookID = nextBookID;
                    bookItem.brandID = brand.BrandID;
                    bookItem.coverRenderer = coverRenderer;
                    bookItem.nativeRotation = nativeRotation;
                    bookItem.baseRotationEuler =
                        (Quaternion.Inverse(nativeRotation) * StandardBookRotation).eulerAngles;

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

                    var networkObject = bookRoot.AddComponent<NetworkObject>();
                    networkObject.AutoObjectParentSync = false;
                    networkObject.AlwaysReplicateAsRoot = true;
                    networkObject.DontDestroyWithOwner = true;
                    bookRoot.AddComponent<NetworkBook>();

                    AssetDatabase.DeleteAsset(prefabPath);
                    AssetDatabase.DeleteAsset(dataPath);
                    PrefabUtility.SaveAsPrefabAsset(bookRoot, prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bookRoot);
                }

                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (savedPrefab == null)
                {
                    Debug.LogError("ComicShop: Prefab kaydedilemedi: " + prefabPath);
                    continue;
                }

                if (networkPrefabs != null && !networkPrefabs.Contains(savedPrefab))
                    networkPrefabs.Add(new NetworkPrefab { Prefab = savedPrefab });

                BookData asset = ScriptableObject.CreateInstance<BookData>();
                asset.BookID = nextBookID;
                asset.BrandID = brand.BrandID;
                asset.bookPrefab = savedPrefab;
                AssetDatabase.CreateAsset(asset, dataPath);

                allData.Add(asset);
                nextBookID++;
                generatedForBrand++;
                generatedModels++;
            }

            catalogEntries.Add(new BrandCatalog.Entry
            {
                brandID = brand.BrandID,
                brandName = brand.Name,
                bookCount = generatedForBrand
            });
        }

        // Preserve IDs for brands that no longer have a model folder, so shelf assignments
        // do not silently change when content is temporarily removed from the repository.
        if (catalog.brands != null)
        {
            foreach (var old in catalog.brands)
            {
                if (old == null || catalogEntries.Any(x => x.brandID == old.brandID))
                    continue;

                catalogEntries.Add(new BrandCatalog.Entry
                {
                    brandID = old.brandID,
                    brandName = old.brandName,
                    bookCount = 0
                });
            }
        }

        catalog.brands = catalogEntries
            .OrderBy(x => x.brandID)
            .ToArray();
        EditorUtility.SetDirty(catalog);

        if (networkPrefabs != null)
            EditorUtility.SetDirty(networkPrefabs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BookSpawner spawner = UnityEngine.Object.FindAnyObjectByType<BookSpawner>();
        if (spawner != null)
        {
            spawner.bookTypes = allData.ToArray();
            spawner.copiesPerBook = 10;
            spawner.testBookTypeCount = Mathf.Max(1, allData.Count);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log(
            "ComicShop: TUM KITAPLAR HAZIR. " +
            generatedModels + "/" + totalModels + " model prefab + BookData oldu; " +
            catalog.BrandCount + " marka, " + allData.Count + " farkli kitap, " +
            (allData.Count * 10) + " fiziksel spawn adayi. " +
            "Marka ID'leri BrandCatalog.asset'te kalici.");
    }

    [MenuItem("ComicShop/Setup 15 VERIDIAN Books")]
    public static void SetupLegacyVeridian()
    {
        Debug.LogWarning("Bu menu artik kullanilmiyor. ComicShop > Setup ALL Book Models kullan.");
        SetupAll();
    }

    private static BrandCatalog LoadOrCreateCatalog()
    {
        EnsureFolderPath("Assets/Resources");
        BrandCatalog catalog = AssetDatabase.LoadAssetAtPath<BrandCatalog>(BrandCatalogPath);
        if (catalog != null)
            return catalog;

        catalog = ScriptableObject.CreateInstance<BrandCatalog>();
        catalog.brands = Array.Empty<BrandCatalog.Entry>();
        AssetDatabase.CreateAsset(catalog, BrandCatalogPath);
        return catalog;
    }

    private static void ResetGeneratedOutput()
    {
        DeleteFolderIfExists(GeneratedPrefabRoot);
        DeleteFolderIfExists(GeneratedDataRoot);

        // Eski VERIDIAN-only pipeline'in ciktilari stale prefab olarak network listesinde
        // kalmasin. BookData/Prefab referanslari yeni allData listesiyle yeniden yaziliyor.
        DeleteFolderIfExists(LegacyVeridianPrefabRoot);
        DeleteFolderIfExists(LegacyVeridianDataRoot);

        EnsureFolderPath(GeneratedPrefabRoot);
        EnsureFolderPath(GeneratedDataRoot);
    }

    private static bool IsGeneratedPrefabPath(string path)
    {
        return !string.IsNullOrEmpty(path) &&
               (path.StartsWith(GeneratedPrefabRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(LegacyVeridianPrefabRoot + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static void DeleteFolderIfExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            AssetDatabase.DeleteAsset(path);
    }

    private static void EnsureFolderPath(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return;

        string normalized = path.Replace("\\", "/").TrimEnd('/');
        int slash = normalized.LastIndexOf('/');
        if (slash <= 0)
            return;

        string parent = normalized.Substring(0, slash);
        string child = normalized.Substring(slash + 1);

        EnsureFolderPath(parent);
        if (!AssetDatabase.IsValidFolder(normalized))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Bounds CalculateExactLocalBounds(GameObject root)
    {
        bool initialized = false;
        Bounds bounds = new Bounds();
        Transform rootTransform = root.transform;

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
                continue;

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

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 e = worldBounds.extents;
            Vector3 c = worldBounds.center;

            Vector3[] corners =
            {
                c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3(e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, -e.z), c + new Vector3(e.x, e.y, e.z)
            };

            foreach (Vector3 corner in corners)
                EncapsulateWorldPoint(ref bounds, ref initialized, rootTransform, corner);
        }

        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private static void EncapsulateWorldPoint(ref Bounds bounds, ref bool initialized, Transform root, Vector3 worldPoint)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!initialized)
        {
            bounds = new Bounds(localPoint, Vector3.zero);
            initialized = true;
        }
        else
        {
            bounds.Encapsulate(localPoint);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c.ToString(), "_");

        return value.Replace("/", "_").Replace("\\", "_");
    }
}
#endif
