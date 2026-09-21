#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Recover only missing runtime resources. Keep locally customized art/import settings.
[InitializeOnLoad]
public static class ComicMenuResourceRecovery
{
    static ComicMenuResourceRecovery() { EditorApplication.delayCall += EnsureResources; }

    static void EnsureResources()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureResources;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string folder = "Assets/Resources/ComicShopMenu";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", "ComicShopMenu");
        string[] names = { "menu_background.png", "btn_play.png", "btn_settings.png",
            "btn_credits.png", "btn_quit.png", "cursor_comic.png" };
        foreach (string name in names)
            CopyMissing("Assets/ComicShopMenu/Sprites/" + name, folder + "/" + name);
        CopyMissing("Assets/ComicShopMenu/Audio/Sunday_Morning_Vinyl.mp3",
            folder + "/Sunday_Morning_Vinyl.mp3");
    }

    static void CopyMissing(string source, string destination)
    {
        if (File.Exists(destination)) return;
        if (!AssetDatabase.CopyAsset(source, destination))
            Debug.LogError("[ComicShop] Menu resource recovery failed: " + destination);
    }
}
#endif
