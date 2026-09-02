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
        if (basePrefab == null)
        {
            Debug.LogError($"ComicShop: Temel kitap prefab'i bulunamadi: {BaseBookPrefabPath}");
            return;
        }

        BookItem baseBookItem = basePrefab.GetComponent<BookItem>();
        Rigidbody baseRigidbody = basePrefab.GetComponent<Rigidbody>();

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

            // Book.prefab'in cube gorunusunu miras almak yerine tamamen bos bir root olusturuyoruz.
            // Boylece FBX'in kendi Unity import transform/hiyerarsisi oldugu gibi korunuyor.
            GameObject root = new GameObject($"Book_{i:00}_{modelName}");

            GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (visual == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            visual.name = modelName;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // IMPORTANT: FBX localScale'e dokunma. Unity'de gorunen mevcut boyut aynen korunur.

            BookItem bookItem = root.AddComponent<BookItem>();
            Renderer coverRenderer = visual.GetComponentInChildren<Renderer>(true);

            if (coverRenderer == null)
            {
                Debug.LogError($"ComicShop: '{modelName}' modelinde Renderer bulunamadi.");
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            bookItem.bookID = i;
            bookItem.brandID = 0;
            bookItem.coverRenderer = coverRenderer;

            if (baseBookItem != null)
            {
                bookItem.outlineMaterial = baseBookItem.outlineMaterial;
                bookItem.outlineScale = baseBookItem.outlineScale;
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            Bounds bounds = CalculateLocalBounds(visual, root.transform);
            collider.center = bounds.center;
            collider.size = bounds.size;

            Rigidbody rb = root.AddComponent<Rigidbody>();
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

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

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

        Debug.Log("ComicShop: 15 VERIDIAN kitap modeli, FBX'in Unity'deki mevcut boyutu korunarak bos root prefab yapisinda hazirlandi. Her kitaptan 10 kopya spawn edilecek.");
    }

    private static Bounds CalculateLocalBounds(GameObject visual, Transform root)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        bool initialized = false;
        Bounds bounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 center = root.InverseTransformPoint(worldBounds.center);
            Vector3 extents = worldBounds.extents;

            Vector3[] corners =
            {
                new Vector3(-extents.x, -extents.y, -extents.z),
                new Vector3(-extents.x, -extents.y,  extents.z),
                new Vector3(-extents.x,  extents.y, -extents.z),
                new Vector3(-extents.x,  extents.y,  extents.z),
                new Vector3( extents.x, -extents.y, -extents.z),
                new Vector3( extents.x, -extents.y,  extents.z),
                new Vector3( extents.x,  extents.y, -extents.z),
                new Vector3( extents.x,  extents.y,  extents.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.InverseTransformPoint(worldBounds.center + corner);
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
        }

        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one);
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
