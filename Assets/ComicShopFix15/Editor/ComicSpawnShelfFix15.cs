#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ComicSpawnShelfFix15
{
    const string Net="Assets/Scripts/Net/ConnectionManager.cs", Interaction="Assets/Scripts/PlayerInteraction.cs";
    [MenuItem("Tools/Comic Shop/Spawn ve Raf Bosluklarini Duzelt (v15)")]
    static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Play'i durdur.");return;}
        var scene=SceneManager.GetActiveScene();
        if(string.IsNullOrEmpty(scene.path)||scene.isDirty){Debug.LogError("Once oyun sahnesini Ctrl+S ile kaydet.");return;}
        try
        {
            string net=File.ReadAllText(Net).Replace("\r\n","\n");
            const string old="response.Position = spawn.position + spawn.right * (1.2f * (request.ClientNetworkId % (ulong)maxPlayers));";
            const string replacement="if (response.Approved)\n        {\n            if (ComicSafeSpawn.TryFind(spawn, networkManager.NetworkConfig.PlayerPrefab, out var safePosition)) response.Position = safePosition;\n            else { response.Approved = false; response.CreatePlayerObject = false; response.Reason = \"Baslangic yakininda guvenli bos alan bulunamadi.\"; }\n        }";
            if(!net.Contains("ComicSafeSpawn.TryFind"))net=Once(net,old,replacement);
            string input=File.ReadAllText(Interaction).Replace("\r\n","\n");
            if(!input.Contains("// ComicFix15 trigger filter"))
            {
                input=Once(input,"Physics.RaycastNonAlloc(ray, hits, interactRange, Physics.AllLayers, QueryTriggerInteraction.Ignore)","Physics.RaycastNonAlloc(ray, hits, interactRange, Physics.AllLayers, QueryTriggerInteraction.Collide)");
                input=Once(input,"Physics.RaycastAll(ray, interactRange, Physics.AllLayers, QueryTriggerInteraction.Ignore)","Physics.RaycastAll(ray, interactRange, Physics.AllLayers, QueryTriggerInteraction.Collide)");
                input=Once(input,"RaycastHit hit = hits[hitIndex];","RaycastHit hit = hits[hitIndex];\n            // ComicFix15 trigger filter\n            if (hit.collider.isTrigger && !hit.collider.GetComponentInParent<ShelfSlot>()) continue;");
            }
            string backup="ComicShopMenuBackups/SpawnShelf15-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Backup(Net,backup);Backup(Interaction,backup);Backup(scene.path,backup);
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!asset || (!asset.GetComponentInChildren<ShelfSlot>(true) && !HasShelfMesh(asset)))continue;
                Backup(path,backup);
                var root=PrefabUtility.LoadPrefabContents(path);
                try{int n=Repair(root);if(n>0){PrefabUtility.SaveAsPrefabAsset(root,path);count+=n;}}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(var root in scene.GetRootGameObjects())count+=Repair(root);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Net,net);File.WriteAllText(Interaction,input);AssetDatabase.Refresh();
            Debug.Log("[ComicShop v15] Spawn kontrolu + raf fizigi uygulandi. Duzenlenen nesne: "+count+". Yedek: "+backup+". Derleme bitince host + ikinci oyuncuyla dene.");
        }
        catch(Exception e){Debug.LogError("[ComicShop v15] "+e.Message+" — Console mesajini paylas.");}
    }
    static string Once(string s,string a,string b)
    {int i=s.IndexOf(a,StringComparison.Ordinal);if(i<0||s.IndexOf(a,i+a.Length,StringComparison.Ordinal)>=0)throw new Exception("Kaynak surumu farkli; ConnectionManager.cs ve PlayerInteraction.cs gerekli");return s.Substring(0,i)+b+s.Substring(i+a.Length);}
    static void Backup(string p,string folder){string dest=Path.Combine(folder,p);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(p,dest,true);}
    static bool ShelfMesh(MeshFilter f)
    {string path=AssetDatabase.GetAssetPath(f.sharedMesh);return path=="Assets/comics/raf/RAF.fbx" || path.StartsWith("Assets/ComicShopShelfFrame/Generated/",StringComparison.Ordinal);}
    static bool HasShelfMesh(GameObject root){foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(ShelfMesh(f))return true;return false;}
    static int Repair(GameObject root)
    {
        int count=0;
        foreach(var slot in root.GetComponentsInChildren<ShelfSlot>(true))
        foreach(var c in slot.GetComponents<Collider>())if(!c.isTrigger){Undo.RecordObject(c,"Raf gozu tetikleyici");c.isTrigger=true;PrefabUtility.RecordPrefabInstancePropertyModifications(c);count++;}
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!ShelfMesh(f)||f.GetComponentInParent<Rigidbody>())continue;
            // Exact visible triangles preserve openings, including rotated/scaled shelves.
            foreach(var c in f.GetComponents<Collider>())if(!(c is MeshCollider)&&!c.isTrigger&&c.enabled){Undo.RecordObject(c,"Raf kutusunu kapat");c.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
            var mesh=f.GetComponent<MeshCollider>();if(!mesh)mesh=Undo.AddComponent<MeshCollider>(f.gameObject);
            Undo.RecordObject(mesh,"Ahsap raf carpismasi");mesh.sharedMesh=f.sharedMesh;mesh.convex=false;mesh.isTrigger=false;mesh.enabled=true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);count++;
        }
        return count;
    }
}
#endif
