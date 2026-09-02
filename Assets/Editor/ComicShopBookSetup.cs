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
            root.name = $"Book_{i:00}_{modelName}";
            root.transform.localScale = Vector3.one;

            MeshFilter oldFilter = root.GetComponent<MeshFilter>();
            MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
            if (oldFilter != null)
                UnityEngine.Object.DestroyImmediate(oldFilter);
            if (oldRenderer != null)
                UnityEngine.Object.DestroyImmediate(oldRenderer);

            GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
            visual.name = modelName;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            BookItem bookItem = root.GetComponent<BookItem>();
            Renderer coverRenderer = visual.GetComponentInChildren<Renderer>();
            bookItem.bookID = i;
            bookItem.brandID = 0;
            bookItem.coverRenderer = coverRenderer;

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider != null && coverRenderer != null)
            {
                Bounds bounds = coverRenderer.bounds;
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            BookData asset = ScriptableObject.CreateInstance<BookData>();
            asset.BookID = i;
            asset.BrandID = 0;
            asset.bookPrefab = prefab;
            AssetDatabase.CreateAsset(asset, dataPath);
            data[i] = asset;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BookSpawner spawner = UnityEngine.Object.FindFirstObjectByType<BookSpawner>();
        if (spawner != null)
        {
            spawner.bookTypes = data;
            spawner.copiesPerBook = 10;
            spawner.testBookTypeCount = 15;
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("ComicShop: 15 VERIDIAN kitap modeli BookData + fizik prefab'i olarak hazirlandi. Her kitaptan 10 kopya spawn edilecek.");
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
