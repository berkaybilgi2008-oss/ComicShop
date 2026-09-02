#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ComicShopBookSetup
{
    private const string ModelFolder = "Assets/comics/models/VERIDIAN";
    private const string BaseBookPrefabPath = "Assets/Prefabs/Book.prefab";
    private const string OutputPrefabFolder = "Assets/Prefabs/VeridianBooks";
    private const string OutputDataFolder = "Assets/BookData/VERIDIAN";

    [MenuItem("ComicShop/Setup 15 VERIDIAN Books")]
    public static void Setup()
    {
        EnsureFolder("Assets/Prefabs", "VeridianBooks");
        EnsureFolder("Assets", "BookData");
        EnsureFolder("Assets/BookData", "VERIDIAN");

        string[] modelPaths = AssetDatabase.FindAssets("t:Model", new[] { ModelFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToArray();

        if (modelPaths.Length != 15)
        {
            Debug.LogError($"ComicShop: VERIDIAN klasorunde 15 FBX bekleniyordu, {modelPaths.Length} bulundu.");
            return;
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseBookPrefabPath);
        BookItem baseBookItem = basePrefab != null ? basePrefab.GetComponent<BookItem>() : null;
        Rigidbody baseRigidbody = basePrefab != null ? basePrefab.GetComponent<Rigidbody>() : null;

        if (basePrefab == null)
        {
            Debug.LogError($"ComicShop: Temel kitap prefab'i bulunamadi: {BaseBookPrefabPath}");
            return;
        }

        BookData[] data = new BookData[15];

        for (int i = 0; i < modelPaths.Length; i++)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[i]);
            if (model == null)
                continue;

            string modelName = Path.GetFileNameWithoutExtension(modelPaths[i]);
            string prefabPath = $"{OutputPrefabFolder}/Book_{i:00}_{Sanitize(modelName)}.prefab";
            string dataPath = $"{OutputDataFolder}/BookData_{i:00}_{Sanitize(modelName)}.asset";

            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.DeleteAsset(dataPath);

            GameObject bookRoot = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (bookRoot == null)
                continue;

            bookRoot.name = $"Book_{i:00}_{modelName}";

            Vector3 originalLocalScale = bookRoot.transform.localScale;
            Quaternion originalLocalRotation = bookRoot.transform.localRotation;
            Vector3 originalLocalPosition = bookRoot.transform.localPosition;

            Renderer coverRenderer = bookRoot.GetComponentInChildren<Renderer>(true);
            if (coverRenderer == null)
            {
                Debug.LogError($"ComicShop: '{modelName}' modelinde Renderer bulunamadi.");
                UnityEngine.Object.DestroyImmediate(bookRoot);
                continue;
            }

            BookItem bookItem = bookRoot.AddComponent<BookItem>();
            bookItem.bookID = i;
            bookItem.brandID = 0;
            bookItem.coverRenderer = coverRenderer;

            if (baseBookItem != null)
            {
                bookItem.outlineMaterial = baseBookItem.outlineMaterial;
                bookItem.outlineScale = baseBookItem.outlineScale;
            }

            bookRoot.transform.localPosition = originalLocalPosition;
            bookRoot.transform.localRotation = originalLocalRotation;
            bookRoot.transform.localScale = originalLocalScale;

            Bounds bounds = CalculateExactLocalBounds(bookRoot);
            bookItem.orientationCorrection = CalculateFlatOrientationCorrection(bounds);

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

            PrefabUtility.SaveAsPrefabAsset(bookRoot, prefabPath);
            UnityEngine.Object.DestroyImmediate(bookRoot);

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            BookData asset = ScriptableObject.CreateInstance<BookData>();
            asset.BookID = i;
            asset.BrandID = 0;
            asset.bookPrefab = savedPrefab;
            AssetDatabase.CreateAsset(asset, dataPath);
            data[i] = asset;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BookSpawner spawner = UnityEngine.Object.FindAnyObjectByType<BookSpawner>();
        if (spawner != null)
        {
            spawner.bookTypes = data;
            spawner.copiesPerBook = 10;
            spawner.testBookTypeCount = 15;
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("ComicShop: 15 VERIDIAN kitap hazirlandi. FBX olcekleri korunuyor; modelin en ince ekseni yatay kitap icin yukariya hizalanacak. Spawn random donusu korunuyor.");
    }

    private static Quaternion CalculateFlatOrientationCorrection(Bounds bounds)
    {
        Vector3 size = bounds.size;

        int thinAxis = 0;
        if (size.y < size.x && size.y <= size.z)
            thinAxis = 1;
        else if (size.z < size.x && size.z < size.y)
            thinAxis = 2;

        Vector3 localAxis = thinAxis == 0 ? Vector3.right : thinAxis == 1 ? Vector3.up : Vector3.forward;
        return Quaternion.FromToRotation(localAxis, Vector3.up);
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
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z),
                c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(e.x, e.y, e.z)
            };

            foreach (Vector3 corner in corners)
                EncapsulateWorldPoint(ref bounds, ref initialized, rootTransform, meshFilter.transform.TransformPoint(corner));
        }

        SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 e = worldBounds.extents;
            Vector3 c = worldBounds.center;

            Vector3[] corners =
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z),
                c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(e.x, e.y, e.z)
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static string Sanitize(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c.ToString(), "_");
        return value.Replace("/", "_").Replace("\\", "_");
    }
}
#endif
