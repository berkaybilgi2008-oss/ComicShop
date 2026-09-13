using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
namespace ComicShopV16.Editor {
public static class ShopV16Importer {
    [Serializable] class Data { public int version; public Node[] nodes; public Geo[] geometries; public Mat[] materials; public Tex[] textures; public Lamp[] lights; public float[] ambient; public Slot[] slots; public Slot register; public Cam camera; }
    [Serializable] class Node { public string name; public int parent,geometry,renderOrder; public float[] position,quaternion,scale; public bool visible,outline,castShadow,receiveShadow,collision; public int[] materials; public float fanSpeed; }
    [Serializable] class Geo { public float[] positions,normals,uv; public int[] indices; public Group[] groups; }
    [Serializable] class Group { public int start,count,materialIndex; }
    [Serializable] class Mat { public string name; public float[] color,emissive; public int texture,side; public bool unlit,smooth,transparent,depthWrite,additive,polygonOffset; public float opacity,alphaTest,polygonOffsetFactor,polygonOffsetUnits; }
    [Serializable] class Tex { public string png; public float[] repeat,offset; public int wrapS,wrapT; public bool flipY,srgb; }
    [Serializable] class Lamp { public int node; public string type; public float[] color,target; public float intensity,distance,decay; public bool shadow; }
    [Serializable] class Slot { public int id; public float x,y,z,rotY; public string bolge; }
    [Serializable] class Cam { public float[] position,quaternion; public float fov; }
    static Vector3 V(float[] a) => new Vector3(a[0],a[1],-a[2]);
    static Quaternion Q(float[] a) => new Quaternion(-a[0],-a[1],a[2],a[3]);
    static Vector4 C(float[] a,float alpha=1) => new Vector4(a[0],a[1],a[2],alpha);
    static TextureWrapMode Wrap(int n) => n==1000?TextureWrapMode.Repeat:n==1002?TextureWrapMode.Mirror:TextureWrapMode.Clamp;
    [MenuItem("Tools/ComicShop/V16/Create Prefab and Preview")]
    public static void Import() {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) { EditorUtility.DisplayDialog("ComicShop V16", "Bu paket URP gerektirir. PC Quality profilini seçin.","Tamam"); return; }
        const string source="Assets/ComicShopV16/shop-v16.shopdata";
        if(!File.Exists(source)) throw new FileNotFoundException("Paketin Assets klasörünü proje köküne kopyalayın.",source);
        var data=JsonUtility.FromJson<Data>(File.ReadAllText(source));
        if(data.version!=1 || data.lights.Length>32) throw new InvalidDataException("Unsupported scene version or light count.");
        var shader=Shader.Find("ComicShop/V16 Source Toon");var inkShader=Shader.Find("ComicShop/V16 Ink");
        if(!shader||!inkShader) throw new InvalidOperationException("V16 shader dosyaları bulunamadı.");
        string dest=AssetDatabase.GenerateUniqueAssetPath("Assets/ComicShopV16/Generated");
        Directory.CreateDirectory(dest+"/Textures");Directory.CreateDirectory(dest+"/Materials");Directory.CreateDirectory(dest+"/Meshes");AssetDatabase.Refresh();
        var textures=new Texture2D[data.textures.Length];
        for(int i=0;i<textures.Length;i++) {
            var t=data.textures[i];string p=dest+"/Textures/T_"+i+".png";
            File.WriteAllBytes(p,Convert.FromBase64String(t.png));AssetDatabase.ImportAsset(p);
            var importer=(TextureImporter)AssetImporter.GetAtPath(p);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=t.srgb;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;
            importer.maxTextureSize=8192;importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=16;
            importer.wrapModeU=Wrap(t.wrapS);importer.wrapModeV=Wrap(t.wrapT);importer.SaveAndReimport();
            textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        var materials=new Material[data.materials.Length];
        for(int i=0;i<materials.Length;i++) {
            var d=data.materials[i];var m=new Material(shader){name=d.name};materials[i]=m;
            // Source r128 hex colors are already shader-linear; do NOT call .linear here.
            m.SetVector("_BaseColor",C(d.color,d.opacity));m.SetVector("_Emission",C(d.emissive,0));
            m.SetFloat("_Unlit",d.unlit?1:0);m.SetFloat("_Smooth",d.smooth?1:0);
            m.SetFloat("_Cull",d.side==2?0:d.side==1?1:2);m.SetFloat("_ZWrite",d.depthWrite?1:0);
            m.SetFloat("_Cutoff",d.alphaTest);
            m.SetFloat("_SrcBlend",d.transparent?(float)BlendMode.SrcAlpha:(float)BlendMode.One);
            m.SetFloat("_DstBlend",d.transparent?(d.additive?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha):(float)BlendMode.Zero);
            m.SetFloat("_OffsetFactor",d.polygonOffset?d.polygonOffsetFactor:0);m.SetFloat("_OffsetUnits",d.polygonOffset?d.polygonOffsetUnits:0);
            m.renderQueue=d.transparent?3000:2000;m.SetOverrideTag("RenderType",d.transparent?"Transparent":"Opaque");
            if(d.texture>=0) {var t=data.textures[d.texture];m.SetTexture("_BaseMap",textures[d.texture]);m.SetTextureScale("_BaseMap",new Vector2(t.repeat[0],t.repeat[1]));m.SetTextureOffset("_BaseMap",new Vector2(t.offset[0],t.offset[1]));m.SetFloat("_FlipY",t.flipY?0:1);}
            AssetDatabase.CreateAsset(m,dest+"/Materials/M_"+i+".mat");
        }
        var ink=new Material(inkShader);AssetDatabase.CreateAsset(ink,dest+"/Materials/Ink.mat");
        var objects=new GameObject[data.nodes.Length];var meshes=new Dictionary<string,Mesh>();
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(preview);
        GameObject root=null;
        try {
        for(int i=0;i<objects.Length;i++) {
            var n=data.nodes[i];var go=new GameObject(n.name);objects[i]=go;
            if(n.parent>=0)go.transform.SetParent(objects[n.parent].transform,false);else root=go;
            go.transform.localPosition=V(n.position);go.transform.localRotation=Q(n.quaternion);go.transform.localScale=new Vector3(n.scale[0],n.scale[1],n.scale[2]);go.SetActive(n.visible);
            if(n.fanSpeed!=0)go.AddComponent<ShopV16Fan>().degreesPerSecond=n.fanSpeed;
            if(n.geometry<0)continue;
            string key=n.geometry+"_"+n.materials.Length;
            if(!meshes.TryGetValue(key,out var mesh)) {
                var g=data.geometries[n.geometry];mesh=new Mesh{name="Geometry_"+key,indexFormat=IndexFormat.UInt32};
                var p=new Vector3[g.positions.Length/3];var norm=new Vector3[p.Length];var uv=new Vector2[p.Length];
                for(int k=0;k<p.Length;k++){p[k]=new Vector3(g.positions[k*3],g.positions[k*3+1],-g.positions[k*3+2]);norm[k]=new Vector3(g.normals[k*3],g.normals[k*3+1],-g.normals[k*3+2]);if(g.uv.Length>0)uv[k]=new Vector2(g.uv[k*2],g.uv[k*2+1]);}
                mesh.vertices=p;mesh.normals=norm;mesh.uv=uv;
                // Reflect Z and reverse winding to keep outward-facing Unity triangles.
                mesh.subMeshCount=n.materials.Length;
                for(int s=0;s<n.materials.Length;s++) {
                    var indices=new List<int>();
                    if(n.materials.Length==1)indices.AddRange(g.indices);
                    else foreach(var group in g.groups)if(group.materialIndex==s)for(int k=group.start;k<group.start+group.count;k++)indices.Add(g.indices[k]);
                    for(int k=0;k<indices.Count;k+=3){int swap=indices[k+1];indices[k+1]=indices[k+2];indices[k+2]=swap;}
                    mesh.SetTriangles(indices,s);
                }
                mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,dest+"/Meshes/G_"+key+".asset");meshes[key]=mesh;
            }
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();
            var ms=new Material[n.materials.Length];for(int k=0;k<ms.Length;k++)ms[k]=materials[n.materials[k]];r.sharedMaterials=ms;r.sortingOrder=n.renderOrder;
            r.shadowCastingMode=n.castShadow?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=n.receiveShadow;
            var properties=new MaterialPropertyBlock();properties.SetFloat("_Receive",n.receiveShadow?1:0);r.SetPropertyBlock(properties);
            if(n.outline){var o=new GameObject("Ink");o.transform.SetParent(go.transform,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;var ir=o.AddComponent<MeshRenderer>();var im=new Material[ms.Length];for(int k=0;k<im.Length;k++)im[k]=ink;ir.sharedMaterials=im;ir.shadowCastingMode=ShadowCastingMode.Off;ir.receiveShadows=false;}
            // Large static opaque surfaces only: floors, walls, counters. Never add colliders to animated fans or visual light cards.
            if(n.collision && mesh.bounds.size.magnitude>0.5f && !go.GetComponentInParent<ShopV16Fan>())go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        root.name="ComicShop V16";
        var appearance=root.AddComponent<ShopV16Appearance>();appearance.materials=materials;
        appearance.sources=new Transform[data.lights.Length];appearance.directional=new bool[data.lights.Length];appearance.colors=new Vector4[32];appearance.parameters=new Vector4[32];appearance.ambient=C(data.ambient,0);
        Light sun=null;
        for(int i=0;i<data.lights.Length;i++) {
            var d=data.lights[i];var t=objects[d.node].transform;bool dir=d.type=="DirectionalLight";
            appearance.sources[i]=t;appearance.directional[i]=dir;appearance.colors[i]=C(d.color,0)*d.intensity;appearance.parameters[i]=new Vector4(d.distance,d.decay,d.shadow?1:0,0);
            if(dir)t.rotation=Quaternion.LookRotation((V(d.target)-t.position).normalized,Vector3.up);
            // Only the shadow-casting sun uses a Unity Light. All source lights are evaluated by the V16 shader.
            if(d.shadow&&dir){sun=t.gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(d.color[0],d.color[1],d.color[2]);sun.intensity=d.intensity;sun.shadows=LightShadows.Soft;sun.shadowBias=0.035f;sun.shadowNormalBias=0.022f;}
        }
        var slots=new GameObject("Shelf Slots - 22");slots.transform.SetParent(root.transform,false);
        foreach(var s in data.slots)MakeSlot(slots.transform,s,"Shelf_"+s.id+"_"+s.bolge);
        MakeSlot(root.transform,data.register,"Cash Register Anchor");
        var printShader=Shader.Find("ComicShop/V16 Print Overlay");
        var printMaterial=new Material(printShader);AssetDatabase.CreateAsset(printMaterial,dest+"/Materials/Print.mat");
        var print=new GameObject("Print Overlay - Optional",typeof(RectTransform),typeof(Canvas));
        print.transform.SetParent(root.transform,false);var canvas=print.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-100;
        var panel=new GameObject("Paper",typeof(RectTransform),typeof(RawImage));panel.transform.SetParent(print.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var image=panel.GetComponent<RawImage>();image.material=printMaterial;image.raycastTarget=false;
        print.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root,dest+"/ComicShopV16.prefab");
        print.SetActive(true);

        var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.transform.position=V(data.camera.position);camera.transform.rotation=Q(data.camera.quaternion)*Quaternion.Euler(0,180,0);camera.fieldOfView=data.camera.fov;camera.nearClipPlane=0.05f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0xC2/255f,0xC9/255f,0xC6/255f);camera.tag="MainCamera";
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        RenderSettings.sun=sun;RenderSettings.fog=false;
        EditorSceneManager.SaveScene(preview,dest+"/Preview.unity");
        EditorSceneManager.CloseScene(preview,true);root=null;
        if(previous.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(dest+"/ComicShopV16.prefab");
        EditorUtility.DisplayDialog("ComicShop V16", "Hazır: "+dest+"\nÖnce Preview sahnesini açın. Prefab'ı oyun sahnesine sürükleyebilirsiniz. Kurulum mevcut sahnenizi değiştirmez.","Tamam");
        } finally {
            if(root)UnityEngine.Object.DestroyImmediate(root);
            if(preview.IsValid()&&preview.isLoaded)EditorSceneManager.CloseScene(preview,true);
            if(previous.IsValid()&&previous.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
    }
    static void MakeSlot(Transform parent,Slot s,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(s.x,s.y,-s.z);g.transform.localRotation=Quaternion.Euler(0,180-s.rotY*Mathf.Rad2Deg,0);}
}
}
