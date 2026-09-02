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

            GameObject root = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (root == null)
                continue;

            root.name = $"Book_{i:00}_{modelName}";
            root.transform.localScale = Vector3.one;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
                UnityEngine.Object.DestroyImmediate(filter);

            MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in meshRenderers)
                UnityEngine.Object.DestroyImmediate(renderer);

            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
                UnityEngine.Object.DestroyImmediate(renderer);

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
            visual.transform.localScale = Vector3.one;

            BookItem bookItem = root.GetComponent<BookItem>();
            Renderer coverRenderer = visual.GetComponentInChildren<Renderer>(true);

            if (bookItem == null || coverRenderer == null)
            {
                Debug.LogError($"ComicShop: '{modelName}' modelinde BookItem veya Renderer bulunamadi.");
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            bookItem.bookID = i;
            bookItem.brandID = 0;
            bookItem.coverRenderer = coverRenderer;

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider != null)
            {
                Bounds bounds = CalculateWorldBounds(visual);
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
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

        Debug.Log("ComicShop: 15 VERIDIAN kitap modeli temiz Book prefab + fizik prefab'i olarak hazirlandi. Her kitaptan 10 kopya spawn edilecek.");
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
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
