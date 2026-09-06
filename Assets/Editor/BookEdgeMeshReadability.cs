using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BookEdgeMeshReadability
{
    static BookEdgeMeshReadability()
    {
        EditorApplication.delayCall += EnsureReadable;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += EnsureReadable;
    }

    [MenuItem("Tools/Comic Shop/Enable Book Edge Mesh Read/Write")]
    private static void EnableReadWrite()
    {
        EnsureReadable(true);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureReadable(true);
    }

    private static void EnsureReadable(bool forceLog = false)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model");
        int changed = 0;
        int found = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/comics/models/", System.StringComparison.OrdinalIgnoreCase))
                continue;

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            found++;
            if (importer.isReadable)
                continue;

            importer.isReadable = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            changed++;
        }

        if (forceLog || changed > 0)
        {
            Debug.Log($"BookEdgeLines: {found} kitap modeli bulundu, {changed} model Read/Write icin yeniden import edildi.");
        }
    }
}
