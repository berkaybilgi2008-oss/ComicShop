#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ComicGameplayFixInstaller
{
    [Serializable] public class Hunk { public string before, after; }
    [Serializable] public class FilePatch { public string path; public Hunk[] hunks; }
    [Serializable] public class Patch { public FilePatch[] files; }
    static int Count(string text,string part) { return text.Split(new[]{part},StringSplitOptions.None).Length-1; }
    [MenuItem("Tools/Comic Shop/ESC Raf ve 207 Atisi Duzelt (v11)",false,2)]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) {Debug.LogError("Once Play'i durdur.");return;}
        const string data="Assets/Editor/ComicGameplayFix/patch.json";
        var pending=new Dictionary<string,string>();
        try {
            var patch=JsonUtility.FromJson<Patch>(File.ReadAllText(data));
            foreach(var file in patch.files) {
                string text=File.ReadAllText(file.path).Replace("\r\n","\n"); string original=text;
                foreach(var h in file.hunks) {
                    // Test new text first: a new block may contain its old block as a prefix.
                    if(Count(text,h.after)==1) continue;
                    if(Count(text,h.before)!=1) throw new InvalidOperationException(file.path+": kaynak farkli. Dosyalar degistirilmedi; Console mesajini paylas.");
                    text=text.Replace(h.before,h.after);
                }
                if(text!=original)pending.Add(file.path,text);
            }
            const string front="Assets/Scripts/Polish/ShopFrontEnd.cs";
            string ui=File.ReadAllText(front).Replace("\r\n","\n"), updated=ui;
            string fixedEsc="else if (page == Page.Settings) { if (Connected) Resume(); else BackFromSettings(); }";
            string legacy="else if (page == Page.Settings) { ShopSettings.Save(); page = Page.Session; Build(); }";
            string intermediate="else if (page == Page.Settings) BackFromSettings();";
            if(!ui.Contains(fixedEsc)) {
                if(Count(ui,legacy)==1) {
                    string exit=ui.Contains("void BackFromSettings()") ? fixedEsc :
                        "else if (page == Page.Settings) { if (Connected) Resume(); else { ShopSettings.Save(); page = Page.Session; Build(); } }";
                    updated=ui.Replace(legacy,exit);
                } else if(Count(ui,intermediate)==1) updated=ui.Replace(intermediate,fixedEsc);
                else if(!ui.Contains("else if (page == Page.Settings) { if (Connected) Resume(); else { ShopSettings.Save(); page = Page.Session; Build(); } }"))
                    throw new InvalidOperationException("ShopFrontEnd ESC akisi farkli. Hicbir dosya degistirilmedi.");
            }
            if(updated!=ui)pending.Add(front,updated);
            if(pending.Count==0) {Debug.Log("[ComicShop] Uc duzeltme kaynak kodunda zaten mevcut. Sorun suruyorsa aktif sahne/prefab veya ikinci menu kontrol edilmeli; eski kod tekrar yazilmadi.");return;}
            string backup="ComicShopGameplayBackups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            // Validate everything first, then back up every target before writing anything.
            foreach(var pair in pending) {string copy=Path.Combine(backup,pair.Key);Directory.CreateDirectory(Path.GetDirectoryName(copy));File.Copy(pair.Key,copy);}
            AssetDatabase.StartAssetEditing();
            try {foreach(var pair in pending)File.WriteAllText(pair.Key,pair.Value,new UTF8Encoding(false));}
            catch {
                foreach(var pair in pending)File.Copy(Path.Combine(backup,pair.Key),pair.Key,true);
                throw;
            }
            finally {AssetDatabase.StopAssetEditing();AssetDatabase.Refresh();}
            Debug.Log("[ComicShop] ESC, raf yonu ve 207 km/sa donen atis yamasi uygulandi. Derleme bittikten sonra Play'de dene. Yedek: "+backup);
        } catch(Exception e) {Debug.LogError("[ComicShop] Kurulum tamamlanamadi: "+e.Message);}
    }
}
#endif
