#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VeridianBookRotationTool
{
    private const string OutputPrefabFolder = "Assets/Prefabs/VeridianBooks";

    [MenuItem("ComicShop/Apply VERIDIAN Book Rotations NOW", priority = 1)]
    public static void Apply()
    {
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { OutputPrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int changed = 0;

        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                continue;

            try
            {
                BookItem book = root.GetComponent<BookItem>();
                if (book == null || book.brandID != 0)
                    continue;

                Vector3 target;
                if (book.baseRotationEuler.x < 0f)
                    target = new Vector3(270f, 0f, 180f);
                else if (Mathf.Approximately(book.baseRotationEuler.x, 270f))
                    target = new Vector3(0f, 0f, 180f);
                else
                    continue;

                Quaternion rotation = Quaternion.Euler(target);
                book.baseRotationEuler = target;
                book.nativeRotation = rotation;
                root.transform.localRotation = rotation;

                EditorUtility.SetDirty(book);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ComicShop: {changed} VERIDIAN prefab rotasyonu uygulandi. -4... -> (270, 0, 180), 270... -> (0, 0, 180).");
    }
}
#endif
