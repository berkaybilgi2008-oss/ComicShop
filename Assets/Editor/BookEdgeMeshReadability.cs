using UnityEditor;
using UnityEditor.Callbacks;

[InitializeOnLoad]
public static class BookEdgeMeshReadability
{
    static BookEdgeMeshReadability()
    {
        EditorApplication.delayCall += EnsureReadable;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += EnsureReadable;
    }

    [MenuItem("Tools/Comic Shop/Enable Book Edge Mesh Read/Write")]
    private static void MenuEnsureReadable()
    {
        EnsureReadable();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureReadable();
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

        UnityEngine.Debug.Log(
            changed > 0
                ? $"BookEdgeLines: {changed} kitap modeli Read/Write icin yeniden import edildi."
                : "BookEdgeLines: Kitap modellerinin Read/Write ayari zaten acik."
        );
    }
}
