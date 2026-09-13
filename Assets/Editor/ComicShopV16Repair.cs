#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

// Install in Assets/Editor. No dependency on the V16 package's folder path.
public static class ComicShopV16Repair
{
    const string Field = "    public Transform playerSpawnPoint; // V16 scene spawn anchor";
    static readonly string[] ShaderNames = { "ShopV16.shader", "ShopV16Ink.shader", "ShopV16Print.shader" };

    [MenuItem("Tools/ComicShop/V16/1 - Fix Shaders and Spawn Code")]
    public static void FixCode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Once Play'i durdurun."); return; }
        // Preflight all code edits before writing anything; never overwrite an unknown version.
        var paths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        string connection = Unique(paths, "ConnectionManager.cs");
        string books = Unique(paths, "BookSpawner.cs");
        string c = File.ReadAllText(connection);
        string b = File.ReadAllText(books);
        const string oldSpawn = "Transform spawn = networkManager.NetworkConfig.PlayerPrefab.transform;";
        const string newSpawn = "Transform spawn = playerSpawnPoint != null ? playerSpawnPoint : networkManager.NetworkConfig.PlayerPrefab.transform;";
        const string oldBook = "Vector3 pos = transform.position + new Vector3(x, spawnHeight, z);";
        const string previousBook = "Vector3 pos = transform.TransformPoint(new Vector3(x, spawnHeight, z));";
        const string newBook = "Vector3 pos = (v16SpawnArea != null ? v16SpawnArea : transform).TransformPoint(new Vector3(x, spawnHeight, z));";
        if (!c.Contains(newSpawn)) {
            if (!c.Contains(oldSpawn) || !c.Contains("public class ConnectionManager : MonoBehaviour"))
                throw new InvalidOperationException("ConnectionManager surumu farkli. Dosya degistirilmedi; bu dosyayi paylasin.");
            if (c.Contains("playerSpawnPoint"))
                throw new InvalidOperationException("Mevcut playerSpawnPoint kodu farkli. Otomatik degisiklik durduruldu.");
            string marker = "public class ConnectionManager : MonoBehaviour";
            int brace = c.IndexOf('{', c.IndexOf(marker, StringComparison.Ordinal));
            c = c.Insert(brace+1, "\n    [Header(\"Scene Spawn\")]\n" + Field + "\n");
            c = c.Replace(oldSpawn, newSpawn);
        }
        if (!b.Contains(newBook)) {
            if ((!b.Contains(oldBook) && !b.Contains(previousBook)) || b.Contains("v16SpawnArea") || !b.Contains("public class BookSpawner : MonoBehaviour"))
                throw new InvalidOperationException("BookSpawner surumu farkli. Dosya degistirilmedi; bu dosyayi paylasin.");
            int brace = b.IndexOf('{',b.IndexOf("public class BookSpawner : MonoBehaviour",StringComparison.Ordinal));
            b = b.Insert(brace+1,"\n    public Transform v16SpawnArea; // V16 scaled scene spawn area\n");
            b = b.Replace(oldBook,newBook).Replace(previousBook,newBook);
        }
        int shaders = 0;
        foreach (string path in Directory.GetFiles(Application.dataPath,"*.shader",SearchOption.AllDirectories)) {
            if (!ShaderNames.Contains(Path.GetFileName(path))) continue;
            string s=File.ReadAllText(path);
            string fixedText=s.Replace("SRGBToLinear(c)","lerp(c / 12.92, pow(max((c + 0.055) / 1.055, 0.0), 2.4), step(0.04045, c))")
                .Replace("LinearToSRGB(c)","lerp(c * 12.92, 1.055 * pow(max(c, 0.0), 1.0 / 2.4) - 0.055, step(0.0031308, c))");
            if(fixedText!=s) { WriteBackup(path,fixedText); shaders++; }
        }
        if(File.ReadAllText(connection)!=c) WriteBackup(connection,c);
        if(File.ReadAllText(books)!=b) WriteBackup(books,b);
        AssetDatabase.Refresh();
        Debug.Log("V16: Shader renk donusumu ve spawn kodu hazir. Degisen shader: "+shaders+". Derleme bitince asil oyun sahnesinde dukkani secin ve Tools/ComicShop/V16/2 - Move Spawns To Selected Shop secin. Eski kaynaklar ComicShopV16RepairBackups klasorunde.");
    }
    static string Unique(string[] paths,string name) {
        var found=paths.Where(p=>Path.GetFileName(p)==name).ToArray();
        if(found.Length!=1)throw new InvalidOperationException(name+" icin "+found.Length+" kopya bulundu. Tek kaynak dosya gerekli.");
        return found[0];
    }
    static void WriteBackup(string path,string contents) {
        string relative=path.Substring(Application.dataPath.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        string backup=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"ComicShopV16RepairBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"),relative);
        Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup,false);File.WriteAllText(path,contents);
    }

    [MenuItem("Tools/ComicShop/V16/2 - Move Spawns To Selected Shop")]
    public static void MoveSpawns()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Once Play'i durdurun.");return;}
        var shop=Selection.activeTransform;
        if(!shop || !shop.gameObject.scene.IsValid() || EditorUtility.IsPersistent(shop)){
            Debug.LogError("Hierarchy'de asil oyun sahnesindeki ComicShop V16 ana nesnesini secin; Project prefab'ini degil.");return;
        }
        if(!shop.GetComponents<Component>().Any(x=>x!=null&&x.GetType().Name=="ShopV16Appearance")){
            Debug.LogError("Secim dukkanin ana nesnesi degil. ShopV16Appearance bileseni olan ComicShop V16 nesnesini secin.");return;
        }
        var scene=shop.gameObject.scene;
        var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x=>x!=null).ToArray();
        var managers=all.Where(x=>x.GetType().Name=="ConnectionManager"&&x.gameObject.activeInHierarchy&&x.enabled).ToArray();
        var spawners=all.Where(x=>x.GetType().Name=="BookSpawner"&&x.gameObject.activeInHierarchy&&x.enabled).ToArray();
        if(managers.Length!=1 || spawners.Length!=1){
            Debug.LogError("Asil oyun sahnesinde tam 1 aktif ConnectionManager ve 1 aktif BookSpawner gerekli. Bulunan: "+managers.Length+" / "+spawners.Length+". Preview sahnesinde bu arac calismaz; birden fazla spawner varsa hangisinin tasinacagini belirleyin.");return;
        }
        Vector3 axisX=shop.TransformVector(Vector3.right), axisY=shop.TransformVector(Vector3.up), axisZ=shop.TransformVector(Vector3.forward);
        if(axisX.magnitude<0.00001f || axisY.magnitude<0.00001f || axisZ.magnitude<0.00001f ||
           Vector3.Dot(axisY.normalized,Vector3.up)<0.999f || Mathf.Abs(axisX.normalized.y)>0.001f || Mathf.Abs(axisZ.normalized.y)>0.001f){
            Debug.LogError("Dukkanin zemini yatay, tavan yonu yukari olmali; sifir olcek kullanilamaz. Mevcut olcek: "+shop.lossyScale+". Olcegin 1 olmasi gerekmiyor.");return;
        }
        var manager=managers[0];var spawner=spawners[0];
        var managerData=new SerializedObject(manager);var point=managerData.FindProperty("playerSpawnPoint");
        if(point==null){Debug.LogError("Once 1 - Fix Shaders and Spawn Code komutunu calistirin ve derlemeyi bekleyin.");return;}
        var bookData=new SerializedObject(spawner);
        if(bookData.FindProperty("areaSize")==null||bookData.FindProperty("spawnHeight")==null){Debug.LogError("BookSpawner alanlari uyusmuyor.");return;}
        var spawnAreaProperty=bookData.FindProperty("v16SpawnArea");
        if(spawnAreaProperty==null){Debug.LogError("Guncel aracta once 1 - Fix Shaders and Spawn Code komutunu tekrar calistirin ve derlemeyi bekleyin.");return;}
        var nm=manager.GetComponent<NetworkManager>();
        if(!nm || !nm.NetworkConfig.PlayerPrefab){Debug.LogError("NetworkManager PlayerPrefab atanmamis.");return;}
        var cc=nm.NetworkConfig.PlayerPrefab.GetComponent<CharacterController>();
        if(!cc){Debug.LogError("Oyuncu prefab kokunde CharacterController bulunamadi; dogus yuksekligi elle belirlenmeli.");return;}
        float scaleY=nm.NetworkConfig.PlayerPrefab.transform.lossyScale.y;
        float height=Mathf.Max(0.15f,(cc.height*0.5f-cc.center.y)*scaleY+cc.skinWidth+0.1f);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Move V16 Spawns");
        try {
            var anchor=shop.Find("Player Spawn - V16");
            if(!anchor){var go=new GameObject("Player Spawn - V16");Undo.RegisterCreatedObjectUndo(go,"Create player spawn");anchor=go.transform;Undo.SetTransformParent(anchor,shop,"Parent spawn");}
            Undo.RecordObject(anchor,"Move player spawn");anchor.position=shop.TransformPoint(new Vector3(0,0,12f))+Vector3.up*height+axisX.normalized*1.8f;
            anchor.rotation=Quaternion.LookRotation(-axisZ.normalized,Vector3.up);anchor.localScale=Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            point.objectReferenceValue=anchor;managerData.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            var bookAnchor=shop.Find("Book Spawn Area - V16");
            if(!bookAnchor){var go=new GameObject("Book Spawn Area - V16");Undo.RegisterCreatedObjectUndo(go,"Create book spawn area");bookAnchor=go.transform;Undo.SetTransformParent(bookAnchor,shop,"Parent book area");}
            Undo.RecordObject(bookAnchor,"Position book area");bookAnchor.localPosition=Vector3.zero;bookAnchor.localRotation=Quaternion.identity;bookAnchor.localScale=Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bookAnchor);
            spawnAreaProperty.objectReferenceValue=bookAnchor;
            bookData.FindProperty("areaSize").vector2Value=new Vector2(12,18);
            // World drop height stays within the scaled room; unrelated BookSpawner scale is ignored.
            bookData.FindProperty("spawnHeight").floatValue=Mathf.Min(1.5f,2.1f*axisY.y)/axisY.y;
            bookData.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
            Selection.activeGameObject=anchor.gameObject;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(new Bounds(anchor.position,Vector3.one*5),false);
            Debug.Log("V16: Kitap dogus alani orta salona baglandi (dukkan olcegine gore 12 x 18 yerel birim). Player Spawn - V16, ConnectionManager'a baglandi. Ctrl+S ile sahneyi kaydedin. Play > Host ve ikinci oyuncuyla test edin. Ctrl+Z bu sahne tasimasini geri alir. Preview Camera varsa oyun sahnesinde kapatin; eski zemin/duvarlari ayri kontrol edin.");
        } catch {Undo.RevertAllDownToGroup(group);throw;}
    }
}
#endif
