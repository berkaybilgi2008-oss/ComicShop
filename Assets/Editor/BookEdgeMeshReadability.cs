using UnityEditor;

[InitializeOnLoad]
public static class BookEdgeMeshReadability
{
    static BookEdgeMeshReadability()
    {
        EditorApplication.delayCall += EnsureReadable;
    }

    private static void EnsureReadable()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/comics/models" });
        int changed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.isReadable)
                continue;

            importer.isReadable = true;
            importer.SaveAndReimport();
            changed++;
        }

        if (changed > 0)
            UnityEngine.Debug.Log("BookEdgeLines: Kitap model meshleri Read/Write icin otomatik yeniden import edildi.");
    }
}
