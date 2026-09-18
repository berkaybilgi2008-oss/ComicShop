using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ComicShop.Rendering.Editor
{
    public static class ToonSunsetSetup
    {
        const string RootName = "ComicShop Sunset Ambience";
        const string Folder = "Assets/ComicShopToon/Sunset";
        [MenuItem("Tools/ComicShop/Apply Sunset and Lamp Shafts (Undo)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("Stop Play Mode before applying sunset."); return; }
            var scene=SceneManager.GetActiveScene();
            var rooms=UnityEngine.Object.FindObjectsByType<ToonRoom>(FindObjectsSortMode.None)
                .Where(r=>r.gameObject.scene==scene).ToArray();
            if(rooms.Length!=1 || !rooms[0].preserveShopfrontOpening || string.IsNullOrEmpty(scene.path))
            { Debug.LogError("Expected one ToonRoom with the aligned yellow shopfront opening, and a saved scene. Nothing changed."); return; }
            var shader=Shader.Find("ComicShop/Light Shaft");
            if(!shader) { Debug.LogError("Light Shaft shader missing. Wait for import."); return; }
            var room=rooms[0];
            var lamps=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true))
                .Where(l=>l.isActiveAndEnabled && l.name=="ComicShop Pendant Spot").ToArray();
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ComicShopToon","Sunset");
            var backup=AssetDatabase.GenerateUniqueAssetPath(Folder+"/BeforeSunset.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true)) throw new InvalidOperationException("Scene backup failed.");
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply shop sunset");
            try
            {
                foreach(var old in scene.GetRootGameObjects().Where(r=>r.name==RootName)) Undo.DestroyObjectImmediate(old);
                var root=new GameObject(RootName); SceneManager.MoveGameObjectToScene(root,scene); Undo.RegisterCreatedObjectUndo(root,"Sunset root");
                var lampMat=Material(shader,"LampShaft",new Color(1f,.69f,.34f),.028f);
                var sunMat=Material(shader,"SunShaft",new Color(1f,.53f,.20f),.016f);
                var b=room.roomBounds;
                bool alongX=room.shopfront==ToonRoom.Facade.NegativeZ || room.shopfront==ToonRoom.Facade.PositiveZ;
                bool positive=room.shopfront==ToonRoom.Facade.PositiveX || room.shopfront==ToonRoom.Facade.PositiveZ;
                Vector3 center=b.center;
                center.y=b.min.y+room.openingCenter.y;
                if(alongX) { center.x+=room.openingCenter.x; center.z=positive?b.max.z:b.min.z; }
                else { center.z+=room.openingCenter.x; center.x=positive?b.max.x:b.min.x; }
                Quaternion rotation=room.transform.rotation;
                Vector3 inward=rotation*(alongX?Vector3.forward:Vector3.right)*(positive?-1:1);
                Vector3 sideways=rotation*(alongX?Vector3.right:Vector3.forward);
                Vector3 window=room.transform.position+rotation*center;
                Vector3 direction=(inward+sideways*.25f+Vector3.down*.38f).normalized;
                // Window sources replace prior named window rigs; architectural blockers remain untouched.
                foreach(var l in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)))
                {
                    if(l.type==LightType.Directional || l.name.StartsWith("Window ") || l.name.IndexOf("Shopfront",StringComparison.OrdinalIgnoreCase)>=0)
                    { Undo.RecordObject(l,"Retire previous daylight"); l.enabled=false; Record(l); }
                }
                for(int k=0;k<2;k++)
                {
                    Vector3 aperture=window+sideways*((k==0?-1:1)*room.openingSize.x*.23f)+Vector3.up*(room.openingSize.y*.22f);
                    var go=new GameObject("Sunset Window "+(k+1)); go.transform.SetParent(root.transform);
                    // Just inside the opening: avoids transparent window meshes shadowing the source.
                    go.transform.position=aperture+inward*.12f;
                    go.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
                    var l=go.AddComponent<Light>(); l.type=LightType.Spot; l.lightmapBakeType=LightmapBakeType.Realtime;
                    l.lightUnit=LightUnit.Candela; l.enableSpotReflector=false; l.intensity=180;
                    l.color=Color.white; l.useColorTemperature=true; l.colorTemperature=2600;
                    l.range=30; l.spotAngle=65; l.innerSpotAngle=35; l.shadows=LightShadows.Hard;
                    l.shadowBias=.04f; l.shadowNormalBias=.06f; l.shadowNearPlane=.05f; l.shadowCustomResolution=1024;
                    var extra=go.AddComponent<UniversalAdditionalLightData>();
                    var so=new SerializedObject(extra); var tier=so.FindProperty("m_AdditionalLightsShadowResolutionTier");
                    if(tier!=null) tier.intValue=2; so.ApplyModifiedProperties();
                    Shaft(root.transform,go.transform.position,direction,9f,3f,sunMat,"Sunset Shaft "+k);
                }
                float floor=(room.transform.position+rotation*new Vector3(b.center.x,b.min.y,b.center.z)).y;
                foreach(var l in lamps)
                {
                    Undo.RecordObject(l,"Warm pendant balance"); l.color=Color.white; l.useColorTemperature=true;
                    l.colorTemperature=3000; l.intensity=24; Record(l);
                    float length=Mathf.Clamp(l.transform.position.y-floor-.15f,.3f,5f);
                    Shaft(root.transform,l.transform.position,Vector3.down,length,Mathf.Min(1.8f,length*.5f),lampMat,"Lamp Shaft");
                }
                foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
                {
                    var data=camera.GetComponent<UniversalAdditionalCameraData>();
                    if(data) { Undo.RecordObject(data,"Enable shaft depth"); data.requiresDepthTexture=true; Record(data); }
                }
                foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
                {
                    var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    Undo.RecordObject(asset,"Enable depth texture"); asset.supportsCameraDepthTexture=true; EditorUtility.SetDirty(asset);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save scene.");
                AssetDatabase.SaveAssets(); SceneView.RepaintAll();
                Debug.Log($"[SUNSET] Saved {scene.path}; 2 warm window spots; {lamps.Length} lamp shafts. Existing roof/blockers untouched. Backup: {backup}. Shafts are decorative depth-clipped volumes, not shadow-aware fog. No bake required.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }
        static Material Material(Shader shader,string name,Color color,float density)
        {
            string path=Folder+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m=new Material(shader); AssetDatabase.CreateAsset(m,path); }
            else Undo.RecordObject(m,"Shaft material");
            m.SetColor("_BaseColor",color); m.SetFloat("_Density",density); EditorUtility.SetDirty(m); return m;
        }
        static void Shaft(Transform root,Vector3 start,Vector3 direction,float length,float radius,Material material,string name)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(root);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position=start+direction*(length*.5f); go.transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)>.99f?Vector3.forward:Vector3.up);
            go.transform.localScale=new Vector3(radius*2,radius*2,length);
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off;
            r.receiveShadows=false; r.lightProbeUsage=LightProbeUsage.Off; r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        static void Record(UnityEngine.Object o)
        { EditorUtility.SetDirty(o); if(PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o); }
    }
}
