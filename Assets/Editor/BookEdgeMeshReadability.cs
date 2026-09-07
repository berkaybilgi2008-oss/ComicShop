using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BookEdgeMeshReadability
{
    static BookEdgeMeshReadability()
    {
        EditorApplication.delayCall += EnsureReadableDelayed;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void EnsureReadableDelayed()
    {
        EnsureReadable();
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
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/comics/models" });
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
            importer.SaveAndReimport();
            changed++;
        }

        if (forceLog || changed > 0)
        {
            Debug.Log($"BookEdgeLines: {found} kitap modeli bulundu, {changed} model Read/Write icin yeniden import edildi.");
        }
    }
}
