using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.IO;
public static class ToastRangerSetup {
 static readonly Dictionary<string,string> Palette=new Dictionary<string,string>{
 {"Crust","B87532"},{"Bread","F3CE8C"},{"ToastMarks","CC9650"},{"Olive","737650"},{"OliveDark","454B36"},{"Red","C7442D"},{"Sauce","AA291D"},{"Cheese","FFD074"},{"Leather","65452F"},{"Pants","3F3631"},{"Sole","B29A6B"},{"Gold","D7A14E"},{"Ink","211C19"},{"White","FFF4D8"},{"Blush","F0CCA0"}};
 [MenuItem("Tools/Toast Ranger/Create Cel Prefab from selected OBJ")]
 static void Build(){
 var path=AssetDatabase.GetAssetPath(Selection.activeObject);
 if(Path.GetExtension(path).ToLowerInvariant()!=".obj") {EditorUtility.DisplayDialog("Toast Ranger","Select ToastRanger.obj in the Project window.","OK");return;}
 var cel=Shader.Find("ToastRanger/Cel URP");var ink=Shader.Find("ToastRanger/Outline URP");
 if(cel==null||ink==null){Debug.LogError("Import both ToastRanger shaders first. URP is required.");return;}
 var importer=AssetImporter.GetAtPath(path) as ModelImporter;
 if(importer!=null){importer.globalScale=1;importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();}
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 var instance=Object.Instantiate(source);instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
 var folder=AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\','/')+"/ToastRangerGenerated");
 AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\','/'),Path.GetFileName(folder));
 var groups=new Dictionary<string,List<CombineInstance>>();
 foreach(var mf in instance.GetComponentsInChildren<MeshFilter>()) {
 var mr=mf.GetComponent<MeshRenderer>();if(mr==null)continue;
 for(int s=0;s<mf.sharedMesh.subMeshCount;s++) {
 var mat=s<mr.sharedMaterials.Length?mr.sharedMaterials[s]:null;
 string key=mat==null?"Bread":mat.name.Replace(" (Instance)","");
 if(!Palette.ContainsKey(key)) {foreach(var candidate in Palette.Keys)if(key.EndsWith(candidate)){key=candidate;break;}}
 if(!groups.ContainsKey(key))groups[key]=new List<CombineInstance>();
 groups[key].Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=s,transform=instance.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix});
 }}
 var combined=new List<CombineInstance>();var materials=new List<Material>();var temporary=new List<Mesh>();
 foreach(var kv in groups){
 var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(kv.Value.ToArray(),true,true);temporary.Add(m);
 combined.Add(new CombineInstance{mesh=m,transform=Matrix4x4.identity});
 var material=new Material(cel){name=kv.Key};Color color;
 ColorUtility.TryParseHtmlString("#"+(Palette.ContainsKey(kv.Key)?Palette[kv.Key]:"F3CE8C"),out color);material.SetColor("_BaseColor",color);
 AssetDatabase.CreateAsset(material,folder+"/"+kv.Key+".mat");materials.Add(material);
 }
 var mesh=new Mesh{name="ToastRanger",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combined.ToArray(),false,true);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/ToastRanger.asset");
 var hull=new Mesh{name="ToastRangerOutline",indexFormat=IndexFormat.UInt32};hull.CombineMeshes(combined.ToArray(),true,true);hull.RecalculateBounds();AssetDatabase.CreateAsset(hull,folder+"/OutlineMesh.asset");
 foreach(var t in temporary)Object.DestroyImmediate(t);Object.DestroyImmediate(instance);
 var root=new GameObject("ToastRanger");root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
 var outline=new GameObject("Outline");outline.transform.SetParent(root.transform,false);outline.AddComponent<MeshFilter>().sharedMesh=hull;
 var om=new Material(ink){name="Outline"};AssetDatabase.CreateAsset(om,folder+"/Outline.mat");var renderer=outline.AddComponent<MeshRenderer>();renderer.sharedMaterial=om;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
 var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/ToastRanger.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);
 }
}
