using UnityEditor;
using UnityEngine;

public static class BookPresentationSetup
{
    private const string Root = "Assets/Prefabs/Books";

    [MenuItem("Tools/ComicShop/Apply Book Names + Toon To All Book Prefabs")]
    public static void ApplyToAllBookPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Root });
        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    if (root.GetComponent<BookItem>() == null)
                        continue;

                    bool dirty = false;

                    if (root.GetComponent<BookDisplayName>() == null)
                    {
                        BookDisplayName name = root.AddComponent<BookDisplayName>();
                        name.SetName(root.name);
                        dirty = true;
                    }

                    if (root.GetComponent<BookToonEffect>() == null)
                    {
                        root.AddComponent<BookToonEffect>();
                        dirty = true;
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ComicShop] Book presentation setup tamamlandi. Guncellenen prefab: " + changed);
    }
}
