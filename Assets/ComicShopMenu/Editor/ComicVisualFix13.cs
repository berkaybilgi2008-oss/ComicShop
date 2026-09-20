#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ComicVisualFix13
{
    const string Front="Assets/Scripts/Polish/ShopFrontEnd.cs";
    const string Templates="Assets/ComicShopMenu/Editor/Visual13/";
    [MenuItem("Tools/Comic Shop/Netlik Imlec ve Ornek Ayarlar (v13)",false,3)]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Once Play'i durdur.");return;}
        try
        {
            string source=File.ReadAllText(Front).Replace("\r\n","\n");
            if(!source.Contains("partial class ShopFrontEnd") || !source.Contains("CreateTitleArt(overlay)"))
                throw new Exception("Once v12 Projeye Otomatik Bagla islemini tamamla.");
            var writes=new Dictionary<string,string>();
            string patched=ReplaceMethod(source,"    void BuildSettings()","    void BuildSettings()\n    {\n        BuildComicSettings();\n    }");
            if(!patched.Contains("        PrepareComicFrame();"))
            {
                const string anchor="        statusText = null;";
                if(patched.Split(new[]{anchor},StringSplitOptions.None).Length!=2)throw new Exception("Build girisi farkli; ShopFrontEnd.cs dosyasini paylas.");
                patched=patched.Replace(anchor,"        PrepareComicFrame();\n"+anchor);
            }
            writes[Front]=patched;
            foreach(string name in new[]{"ShopFrontEnd.ComicTheme","ComicInterfaceOptions","ComicProjectCursor","ComicPillGraphic","ComicShopMenuFeedback"})
                writes["Assets/Scripts/Polish/"+name+".cs"]=File.ReadAllText(Templates+name+".cs.txt");
            const string cursor="Assets/ComicShopMenu/Sprites/cursor_comic.png";
            if(!File.Exists(cursor))throw new Exception("V9 cursor_comic.png dosyasi bulunamadi.");
            string backup="ComicShopMenuBackups/Visual13-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            foreach(var pair in writes)
            {
                if(!File.Exists(pair.Key))continue;
                string dest=Path.Combine(backup,pair.Key);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(pair.Key,dest);
            }
            var texturePaths=new List<string>();
            foreach(string folder in new[]{"Assets/Resources/ComicShopMenu","Assets/ComicShopMenu/Sprites"})
                if(Directory.Exists(folder))texturePaths.AddRange(Directory.GetFiles(folder,"*.png"));
            foreach(string path in texturePaths)if(File.Exists(path+".meta"))
            {string dest=Path.Combine(backup,path+".meta");Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(path+".meta",dest);}
            Directory.CreateDirectory("Assets/Resources/ComicShopMenu");
            File.Copy(cursor,"Assets/Resources/ComicShopMenu/cursor_comic.png",true);
            AssetDatabase.StartAssetEditing();
            try{foreach(var pair in writes)File.WriteAllText(pair.Key,pair.Value,new UTF8Encoding(false));}
            finally{AssetDatabase.StopAssetEditing();}
            AssetDatabase.Refresh();
            texturePaths.Add("Assets/Resources/ComicShopMenu/cursor_comic.png");
            foreach(string path in texturePaths)
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(importer==null)continue;
                importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.crunchedCompression=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.alphaIsTransparency=true;
                foreach(string platform in new[]{"Standalone","Android","iPhone","WebGL"})importer.ClearPlatformTextureSettings(platform);
                var defaults=importer.GetDefaultPlatformTextureSettings();defaults.maxTextureSize=4096;defaults.textureCompression=TextureImporterCompression.Uncompressed;defaults.crunchedCompression=false;importer.SetPlatformTextureSettings(defaults);
                importer.SaveAndReimport();
            }
            Debug.Log("[ComicShop] v13 uygulandi: menu dokulari sikistirmasiz ve yeniden boyutlandirmasiz; ozel imlec + ornege gore 5 sekmeli ayarlar. Derleme bitince Play. Yedek: "+backup);
        }
        catch(Exception e){Debug.LogError("[ComicShop v13] "+e.Message);}
    }
    static string ReplaceMethod(string text,string signature,string replacement)
    {
        int start=text.IndexOf(signature,StringComparison.Ordinal);
        if(start<0 || text.IndexOf(signature,start+signature.Length,StringComparison.Ordinal)>=0)throw new Exception("BuildSettings metodu farkli.");
        int open=text.IndexOf('{',start);int depth=0;bool quoted=false,escaped=false;
        for(int i=open;i<text.Length;i++)
        {
            char c=text[i];
            if(quoted){if(escaped){escaped=false;continue;}if(c=='\\'){escaped=true;continue;}if(c=='"')quoted=false;continue;}
            if(c=='"'){quoted=true;continue;}if(c=='{')depth++;if(c=='}' && --depth==0)return text.Substring(0,start)+replacement+text.Substring(i+1);
        }
        throw new Exception("Metot sonu bulunamadi.");
    }
}
#endif
