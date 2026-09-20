#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ComicProjectAutoBind
{
    const string Front = "Assets/Scripts/Polish/ShopFrontEnd.cs";
    const string Template = "Assets/ComicShopMenu/Editor/Integration/ShopFrontEnd.ComicTheme.cs.txt";
    const string Extension = "Assets/Scripts/Polish/ShopFrontEnd.ComicTheme.cs";
    const string Pending = "ComicShop.AutoBind.PendingScene";

    [MenuItem("Tools/Comic Shop/Projeye Otomatik Bagla (v12)", false, 0)]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Play'i durdurup tekrar calistir."); return; }
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path) || !scene.GetRootGameObjects().Any(r => r.GetComponentsInChildren<BookSpawner>(true).Length > 0))
        { Debug.LogError("Once oyun sahneni ac: Assets/Settings/ne.unity. MainMenu sahnesinde calistirma."); return; }
        if (!File.Exists(Front) || !File.Exists(Template)) { Debug.LogError("ShopFrontEnd veya entegrasyon sablonu eksik. Hicbir sey degistirilmedi."); return; }
        string source = File.ReadAllText(Front).Replace("\r\n","\n");
        var upgrades = new System.Collections.Generic.Dictionary<string,string>();
        try
        {
            var plan=JsonUtility.FromJson<UpgradePlan>(File.ReadAllText("Assets/ComicShopMenu/Editor/Integration/upgrade.json"));
            foreach(var file in plan.files)
            {
                string text=File.ReadAllText(file.path).Replace("\r\n","\n"), original=text;
                if(file.path==Front && !text.Contains("CreateTitleArt(overlay)"))
                {
                    // v11 may already have fixed ESC on the older front-end. Normalize
                    // that one known variant before upgrading the whole menu flow.
                    text=text.Replace("else if (page == Page.Settings) { if (Connected) Resume(); else { ShopSettings.Save(); page = Page.Session; Build(); } }",
                        "else if (page == Page.Settings) { ShopSettings.Save(); page = Page.Session; Build(); }");
                    text=Upgrade(text,file);
                }
                else if(file.path!=Front) text=Upgrade(text,file);
                if(text!=original) upgrades[file.path]=text;
                if(file.path==Front) source=text;
            }
            const string feedback="Assets/Scripts/Polish/ComicShopMenuFeedback.cs";
            if(!File.Exists(feedback)) upgrades[feedback]=File.ReadAllText("Assets/ComicShopMenu/Editor/Integration/ComicShopMenuFeedback.cs.txt");
        }
        catch(Exception e) { Debug.LogError("[ComicShop] Kaynak degistirilmedi: "+e.Message+" Guncel ShopFrontEnd.cs ve ShopSettings.cs dosyalarini paylas.");return; }
        string[] required = { "void Build()", "void Title(string eyebrow", "ShopSettings.Apply()", "ConnectionManager.SetCursor(true)", "CreateTitleArt(overlay)", "void BuildSettings()" };
        if (required.Any(x => !source.Contains(x))) { Debug.LogError("ShopFrontEnd surumu beklenen yapiyla uyusmuyor. Kaynak degistirilmedi; Console mesajini paylas."); return; }
        string patched = source;
        if (!patched.Contains("public sealed partial class ShopFrontEnd"))
        {
            const string declaration = "public sealed class ShopFrontEnd";
            if (patched.Split(new[] { declaration }, StringSplitOptions.None).Length != 2)
            { Debug.LogError("ShopFrontEnd sinif bildirimi uyusmuyor. Kaynak degistirilmedi."); return; }
            patched = patched.Replace(declaration, "public sealed partial class ShopFrontEnd");
        }
        if (!patched.Contains("ApplyComicTheme();"))
        {
            // Insert at the end of Build, directly before the next method.
            int next = patched.IndexOf("    void Title(string eyebrow", StringComparison.Ordinal);
            int close = patched.LastIndexOf('}', next - 1);
            if (close < 0) { Debug.LogError("Build sonu bulunamadi. Kaynak degistirilmedi."); return; }
            patched = patched.Insert(close, "    ApplyComicTheme();\n    ");
        }
        // Older gameplay-polish revisions did not re-lock the cursor when leaving settings with ESC.
        const string oldEsc = "else if (page == Page.Settings) BackFromSettings();";
        patched = patched.Replace(oldEsc, "else if (page == Page.Settings) { if (Connected) Resume(); else BackFromSettings(); }");
        string[] names = { "menu_background.png", "btn_play.png", "btn_settings.png", "btn_credits.png", "btn_quit.png" };
        foreach (string name in names)
            if (!File.Exists("Assets/ComicShopMenu/Sprites/" + name)) { Debug.LogError("Eksik gorsel: " + name); return; }
        const string music = "Assets/ComicShopMenu/Audio/Sunday_Morning_Vinyl.mp3";
        if (!File.Exists(music)) { Debug.LogError("Menu muzik dosyasi eksik."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string backup = "ComicShopMenuBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(Front, Path.Combine(backup, "ShopFrontEnd.cs"));
        foreach(var entry in upgrades)
            if(entry.Key!=Front && File.Exists(entry.Key)) File.Copy(entry.Key,Path.Combine(backup,Path.GetFileName(entry.Key)));
        File.Copy(scene.path, Path.Combine(backup, Path.GetFileName(scene.path)));
        if (File.Exists("ProjectSettings/EditorBuildSettings.asset")) File.Copy("ProjectSettings/EditorBuildSettings.asset", Path.Combine(backup,"EditorBuildSettings.asset"));
        if (File.Exists(Extension)) File.Copy(Extension,Path.Combine(backup,"ShopFrontEnd.ComicTheme.cs"));
        string resources = "Assets/Resources/ComicShopMenu";
        Directory.CreateDirectory(resources);
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string name in names) CopyWithBackup("Assets/ComicShopMenu/Sprites/" + name, resources + "/" + name, backup);
            CopyWithBackup(music, resources + "/Sunday_Morning_Vinyl.mp3", backup);
            foreach(var entry in upgrades) if(entry.Key!=Front) File.WriteAllText(entry.Key,entry.Value,new UTF8Encoding(false));
            File.WriteAllText(Front, patched, new UTF8Encoding(false));
            File.WriteAllText(Extension, File.ReadAllText(Template), new UTF8Encoding(false));
            SessionState.SetString(Pending, scene.path);
        }
        finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
        EditorApplication.delayCall += Finish;
        Debug.Log("[ComicShop] Entegrasyon kaynaklari hazir. Derleme bittiginde sahne otomatik baglanacak. Yedek: " + backup);
    }
    [Serializable] public class UpgradeHunk { public string before,after; }
    [Serializable] public class UpgradeFile { public string path; public UpgradeHunk[] hunks; }
    [Serializable] public class UpgradePlan { public UpgradeFile[] files; }
    static int Occurrences(string text,string part) => text.Split(new[]{part},StringSplitOptions.None).Length-1;
    static string Upgrade(string text,UpgradeFile file)
    {
        foreach(var h in file.hunks)
        {
            if(Occurrences(text,h.after)==1)continue;
            if(Occurrences(text,h.before)!=1)throw new InvalidOperationException(file.path+": yukseltilecek kod blogu farkli.");
            text=text.Replace(h.before,h.after);
        }
        return text;
    }
    static void CopyWithBackup(string source,string target,string backup)
    {
        if(File.Exists(target)) File.Copy(target,Path.Combine(backup,Path.GetFileName(target)),true);
        File.Copy(source,target,true); // Retain existing .meta GUIDs.
    }
    [InitializeOnLoadMethod]
    static void AfterReload() { EditorApplication.delayCall += Finish; }
    static void Finish()
    {
        string path=SessionState.GetString(Pending, ""); if(string.IsNullOrEmpty(path)) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Finish; return; }
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=path) { Debug.LogWarning("Oyun sahnesine don ve Tools > Comic Shop > Otomatik Baglantiyi Tamamla'ya bas."); return; }
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var pause in root.GetComponentsInChildren<ComicShop.ComicPauseMenu>(true))
            { Undo.RecordObject(pause.gameObject,"Disable duplicate pause");pause.gameObject.SetActive(false); }
            foreach(var menu in root.GetComponentsInChildren<ComicShop.MainMenuController>(true))
            {
                var canvas=menu.GetComponentInParent<Canvas>(true);
                if(canvas) {Undo.RecordObject(canvas.gameObject,"Disable duplicate title");canvas.gameObject.SetActive(false);}
            }
        }
        // Existing ShopFrontEnd opens the title inside the gameplay scene, then offers host/join.
        // A separate MainMenu scene is no longer required.
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=path && s.path!="Assets/ComicShopMenu/Scenes/MainMenu.unity").ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        SessionState.EraseString(Pending);
        Debug.Log("[ComicShop] BAGLANDI. Local Input Scripts / Camera / Mixer / UnityEvent atamana gerek yok. ShopSettings, NetworkPlayerSetup ve ConnectionManager'in mevcut akisi kullaniliyor. Play'e bas. Ozel Build Profile sahne listesi kullaniyorsan bu oyun sahnesini ilk siraya koy.");
    }
    [MenuItem("Tools/Comic Shop/Otomatik Baglantiyi Tamamla", false, 1)]
    static void FinishManually() { Finish(); }
}
#endif
