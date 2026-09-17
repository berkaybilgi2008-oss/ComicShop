using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Unity.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ComicShop.Rendering.Editor
{
    public static class ToonFinalSetup
    {
        const string Menu = "Tools/ComicShop/Step 3/";
        const string RootName = "Final Toon Lighting";
        const string Folder = "Assets/ComicShopToon/FinalSetup";
        static IEnumerable<T> SceneObjects<T>() where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Where(x => x.gameObject.scene == SceneManager.GetActiveScene());
        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create final toon setup");
            return go;
        }
        static string AssetPath(string name) => AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name);
        static void Set(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException("URP serialized field missing: " + name);
            p.intValue = value;
        }
        static void Set(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null) throw new InvalidOperationException("URP serialized field missing: " + name);
            p.boolValue = value;
        }
        [MenuItem(Menu + "1 - Apply Final Interior Setup (Undo)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play Mode or the lighting bake first.");
            var rooms = SceneObjects<ToonRoom>().ToArray();
            if (rooms.Length != 1) throw new InvalidOperationException("Exactly one Step 2 ToonRoom is required in the active scene. Keep your aligned yellow opening.");
            var room = rooms[0];
            if (!room.preserveShopfrontOpening) throw new InvalidOperationException("Enable Preserve Shopfront Opening first.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("Assign an active URP asset first.");
            if (Shader.Find("ComicShop/ToonEmission") == null) throw new InvalidOperationException("ToonEmission shader is not imported.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ComicShopToon", "FinalSetup");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply final toon interior");
            try
            {
                // Validate/rebuild the shell BEFORE changing any light. Reuses the yellow opening.
                ToonArchitectureRepair.GenerateShell();
                ToonArchitectureRepair.Repair();
                Bounds b = room.roomBounds;
                var old = room.transform.Find(RootName);
                var candidates = SceneObjects<Light>()
                    .Where(l => (old == null || !l.transform.IsChildOf(old)) && l.name.IndexOf("Pendant", StringComparison.OrdinalIgnoreCase) >= 0
                        && b.Contains(room.transform.InverseTransformPoint(l.transform.position)))
                    .Select(l => room.transform.InverseTransformPoint(l.transform.position)).ToList();
                // On rerun, preserve the generated fixture layout as well.
                if (candidates.Count == 0 && old != null)
                    candidates.AddRange(old.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("Pendant "))
                        .Select(l => room.transform.InverseTransformPoint(l.transform.position)));
                var positions = Distribute(candidates, b);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                int disabled = 0;
                foreach (var l in SceneObjects<Light>())
                {
                    if (!l.enabled) continue;
                    // Replaces active-scene lighting, including old window test and directional lights.
                    Undo.RecordObject(l, "Disable previous scene lighting"); l.enabled = false; disabled++;
                }
                var root = Child(RootName, room.transform);
                ConfigurePipeline();
                ToonOutlineSetup.Install();
                ConfigureStyle(root.transform);
                ConfigureVolume(root.transform);
                ConfigureBake();
                PrepareGeometry(room);
                ConfigureAmbient();
                Vector3 inward, window;
                Window(room, out inward, out window);
                Vector3 horizontal = Mathf.Abs(inward.z) > 0.5f ? Vector3.right : Vector3.forward;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = window + horizontal * (i == 0 ? -1 : 1) * room.openingSize.x * 0.25f;
                    p.y += room.openingSize.y * 0.3f;
                    p -= inward * (room.shellOffset + room.shellThickness + 0.1f);
                    NewLight(root.transform, "Window " + (i+1), p, inward + Vector3.down * 0.45f, 48f, 18f, 6500f, true, 80f, 60f);
                }
                // Closest pendant to the window supplies the third shadow map.
                positions.Sort((a,c) => (a-window).sqrMagnitude.CompareTo((c-window).sqrMagnitude));
                var glow = CreateGlowMaterial("PendantGlow", new Color(1f, 0.72f, 0.38f));
                for (int i = 0; i < positions.Count; i++)
                {
                    NewLight(root.transform, "Pendant " + (i+1), positions[i], Vector3.down, 8f, 8f, 3200f, i == 0, 120f, 90f);
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.name = "Pendant Glow " + (i+1);
                    bulb.transform.SetParent(root.transform, false); bulb.transform.localPosition = positions[i] + Vector3.up * 0.04f;
                    bulb.transform.localScale = Vector3.one * 0.09f;
                    Object.DestroyImmediate(bulb.GetComponent<Collider>());
                    var r = bulb.GetComponent<MeshRenderer>(); r.sharedMaterial = glow;
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    Undo.RegisterCreatedObjectUndo(bulb, "Create bulb glow");
                }
                var neonPos = window + inward * 0.12f;
                neonPos.y += Mathf.Max(0, room.openingSize.y * 0.3f);
                var neon = NewLight(root.transform, "Neon Fill", neonPos, inward, 1.2f, 1.5f, 6500f, false, 100f, 70f);
                neon.useColorTemperature = false; neon.color = new Color(1f, 0.08f, 0.4f);
                CreateNeon(root.transform, neonPos, inward);
                CreateProbes(root.transform, b);
                foreach(var existingProbe in SceneObjects<ReflectionProbe>())
                { Undo.RecordObject(existingProbe, "Disable previous reflection capture"); existingProbe.enabled=false; }
                var reflection=Undo.AddComponent<ReflectionProbe>(Child("Baked Reflection",root.transform));
                reflection.transform.localPosition=b.center; reflection.mode=ReflectionProbeMode.Baked;
                reflection.resolution=128; reflection.center=Vector3.zero;
                var worldBounds=new Bounds(room.transform.TransformPoint(b.center),Vector3.zero);
                for(int i=0;i<8;i++) worldBounds.Encapsulate(room.transform.TransformPoint(new Vector3(
                    (i&1)==0?b.min.x:b.max.x, (i&2)==0?b.min.y:b.max.y, (i&4)==0?b.min.z:b.max.z)));
                reflection.size=worldBounds.size; reflection.transform.rotation=Quaternion.identity;
                reflection.boxProjection=true; reflection.intensity=0.25f; reflection.cullingMask=-1;
                foreach (var camera in SceneObjects<Camera>())
                {
                    Undo.RecordObject(camera, "Final camera settings");
                    var d = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (d == null) Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                    else Undo.RecordObject(d, "Final camera settings");
                }
                var cameraSetup = Undo.AddComponent<ToonCameraSetup>(root);
                cameraSetup.ConfigureExisting();
                int cleared = ClearLegacyBlocks();
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = root;
                Debug.Log($"Final toon setup: disabled {disabled} old lights; created 2 window + {positions.Count} pendant + 1 neon lights; exactly 3 shadowed spots. Cleared {cleared} legacy material blocks. Preserved room/opening. New profile/settings assets: {Folder}. Bake next; save scene with Ctrl+S. Scene and recorded settings support Undo; generated asset files are retained.", root);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
        static List<Vector3> Distribute(List<Vector3> candidates, Bounds b)
        {
            var result = new List<Vector3>();
            // Greedy nearest-fixture assignment to a 4x2 grid, independent of scene name numbering.
            for (int z = 0; z < 2; z++) for (int x = 0; x < 4; x++)
            {
                Vector3 target = new Vector3(Mathf.Lerp(b.min.x,b.max.x,(x+0.5f)/4f), b.max.y-0.6f,
                    Mathf.Lerp(b.min.z,b.max.z,(z+0.5f)/2f));
                var usable = candidates.Where(p => p.y > b.center.y && result.All(q => (p-q).sqrMagnitude > 0.25f)).ToList();
                Vector3 selected = usable.Count > 0 ? usable.OrderBy(p => (p-target).sqrMagnitude).First() : target;
                if (usable.Count == 0) Debug.LogWarning("No matching existing pendant for grid cell: created fallback light at local " + target);
                result.Add(selected);
            }
            return result;
        }
        static void Window(ToonRoom room, out Vector3 inward, out Vector3 p)
        {
            bool x = room.shopfront == ToonRoom.Facade.NegativeZ || room.shopfront == ToonRoom.Facade.PositiveZ;
            bool positive = room.shopfront == ToonRoom.Facade.PositiveZ || room.shopfront == ToonRoom.Facade.PositiveX;
            var b = room.roomBounds; p = b.center; p.y = b.min.y + room.openingCenter.y;
            inward = (x ? Vector3.forward : Vector3.right) * (positive ? -1 : 1);
            if (x) { p.x += room.openingCenter.x; p.z = positive ? b.max.z : b.min.z; }
            else { p.z += room.openingCenter.x; p.x = positive ? b.max.x : b.min.x; }
        }
        static Light NewLight(Transform parent, string name, Vector3 p, Vector3 dir, float intensity,
            float range, float kelvin, bool shadows, float outer, float inner)
        {
            var go = Child(name, parent); go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.lightmapBakeType = LightmapBakeType.Mixed;
            l.color = Color.white; l.useColorTemperature = true; l.colorTemperature = kelvin;
            l.lightUnit = LightUnit.Candela; l.enableSpotReflector = false; l.intensity = intensity; l.bounceIntensity = 1f; l.range = range; l.spotAngle = outer; l.innerSpotAngle = inner;
            l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            l.shadowBias = 0.1f; l.shadowNormalBias = 0.2f; l.shadowNearPlane = 0.05f;
            l.cullingMask = -1; l.renderingLayerMask = -1;
            var data = go.AddComponent<UniversalAdditionalLightData>(); data.usePipelineSettings = false;
            var serializedLight = new SerializedObject(data);
            Set(serializedLight, "m_AdditionalLightsShadowResolutionTier", name.StartsWith("Window") ? 2 : 1);
            serializedLight.ApplyModifiedProperties();
            return l;
        }
        static void ConfigureStyle(Transform parent)
        {
            var style = Object.Instantiate(ToonStyleController.ActiveStyle); style.name = "FinalToonStyle";
            style.ShadowSteps = 3; style.RampSmoothness = 0.01f; style.LightFalloffScale = 1;
            style.ShadowTint = new Color(58f/255f,46f/255f,82f/255f);
            style.ShadowLift = 0.12f; style.BakedExposure = 2; style.BakedSteps = 3; style.BakedInfluence = 0.25f;
            style.DirectMax = 2; style.EmissionGain = 4; style.HalftoneEnabled = 0f; style.HalftoneStrength = 0.25f;
            style.HalftoneScale = 8; style.HalftoneAngle = 45; style.SpecEnabled = 0; style.RimEnabled = 0;
            style.OutlineThicknessPixels = 1.25f;
            AssetDatabase.CreateAsset(style, AssetPath("FinalToonStyle.asset"));
            foreach (var c in SceneObjects<ToonStyleController>()) { Undo.RecordObject(c,"Disable previous style owner"); c.enabled = false; }
            var owner = Undo.AddComponent<ToonStyleController>(Child("Global Style", parent)); owner.style = style;
            ToonStyleController.PublishActive(); EditorUtility.SetDirty(owner);
            ToonStyleEditor.ResetAll();
        }
        static void ConfigurePipeline()
        {
            // All project URP assets: quality switching cannot silently restore a different look.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] {"Assets"}))
            {
                var p = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(p,"Final URP settings");
                p.supportsHDR = true; p.msaaSampleCount = 1; p.renderScale = 1; p.useSRPBatcher = true;
                p.shadowDistance = 35; p.shadowCascadeCount = 2; p.cascade2Split = 0.35f;
                p.shadowDepthBias = 0.1f; p.shadowNormalBias = 0.2f; p.additionalLightsShadowmapResolution = 2048;
                var so = new SerializedObject(p);
                Set(so,"m_RequireDepthTexture",true); Set(so,"m_SoftShadowsSupported",false);
                Set(so,"m_MainLightShadowsSupported",true); Set(so,"m_AdditionalLightShadowsSupported",true);
                Set(so,"m_AdditionalLightsRenderingMode",1); Set(so,"m_AnyShadowsSupported",true);
                Set(so,"m_ColorGradingMode",1); Set(so,"m_ColorGradingLutSize",32);
                Set(so,"m_LightProbeSystem",0); Set(so,"m_MixedLightingSupported",true);
                Set(so,"m_GPUResidentDrawerMode",0);
                Set(so,"m_AdditionalLightsShadowResolutionTierMedium",512);
                so.ApplyModifiedProperties(); EditorUtility.SetDirty(p);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] {"Assets"}))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(data,"Final renderer settings");
                var so = new SerializedObject(data); Set(so,"m_RenderingMode",2); so.ApplyModifiedProperties();
                foreach (var outline in data.rendererFeatures.OfType<ScreenSpaceOutline>())
                { Undo.RecordObject(outline,"Use full-screen outline mask"); outline.settings.layerMask=-1; EditorUtility.SetDirty(outline); }
                foreach (var feature in data.rendererFeatures)
                    if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                    { Undo.RecordObject(feature,"Disable SSAO"); feature.SetActive(false); EditorUtility.SetDirty(feature); }
                data.SetDirty(); EditorUtility.SetDirty(data);
            }
        }
        static void ConfigureBake()
        {
            var s = new LightingSettings { name = "FinalToonLighting", bakedGI = true, realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly, lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                sampling = LightingSettings.Sampling.Fixed,
                directSampleCount = 64, indirectSampleCount = 512, environmentSampleCount = 128,
                maxBounces = 3, lightmapResolution = 16, lightmapMaxSize = 2048, lightmapPadding = 4, lightmapCompression = LightmapCompression.None,
                directionalityMode = LightmapsMode.NonDirectional, ao = false, albedoBoost = 1, indirectScale = 1,
                filteringMode = LightingSettings.FilterMode.Advanced,
                denoiserTypeDirect = LightingSettings.DenoiserType.None,
                denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage,
                denoiserTypeAO = LightingSettings.DenoiserType.None,
                filterTypeDirect = LightingSettings.FilterType.None,
                filterTypeIndirect = LightingSettings.FilterType.None,
                filterTypeAO = LightingSettings.FilterType.None };
            AssetDatabase.CreateAsset(s, AssetPath("FinalToonLighting.lighting"));
            foreach (var settings in Resources.FindObjectsOfTypeAll<LightmapSettings>())
                Undo.RecordObject(settings, "Scene lighting settings reference");
            Lightmapping.lightingSettings = s;
        }
        static void ConfigureAmbient()
        {
            // RenderSettings are scene-owned; record their serialized object for Undo.
            var settings = new SerializedObject(Unsupported.GetRenderSettings());
            Undo.RecordObject(settings.targetObject,"Final ambient settings");
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f,0.29f,0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.14f,0.12f,0.18f);
            RenderSettings.ambientGroundColor = new Color(0.07f,0.06f,0.09f);
            RenderSettings.ambientIntensity = 0.35f; RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.defaultReflectionResolution = 128; RenderSettings.reflectionBounces = 1;
            RenderSettings.sun = null;
        }
        static void ConfigureVolume(Transform parent)
        {
            foreach (var v in SceneObjects<Volume>()) { Undo.RecordObject(v,"Disable previous volume"); v.enabled = false; }
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "FinalToonVolume";
            AssetDatabase.CreateAsset(profile, AssetPath("FinalToonVolume.asset"));
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); return c;
            }
            Add<Tonemapping>().mode.Override(TonemappingMode.None);
            var color = Add<ColorAdjustments>(); color.postExposure.Override(0.35f); color.contrast.Override(6);
            color.saturation.Override(8); color.hueShift.Override(0); color.colorFilter.Override(Color.white);
            var white = Add<WhiteBalance>(); white.temperature.Override(0); white.tint.Override(0);
            var smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.94f,0.96f,1.08f,0)); smh.midtones.Override(new Vector4(1,1,1,0));
            smh.highlights.Override(new Vector4(1.04f,1.01f,0.96f,0)); smh.shadowsStart.Override(0); smh.shadowsEnd.Override(0.3f);
            smh.highlightsStart.Override(0.6f); smh.highlightsEnd.Override(1);
            var bloom = Add<Bloom>(); bloom.intensity.Override(0.18f); bloom.threshold.Override(1.2f);
            bloom.scatter.Override(0.55f); bloom.clamp.Override(8); bloom.tint.Override(Color.white); bloom.highQualityFiltering.Override(false);
            var vig = Add<Vignette>(); vig.intensity.Override(0.1f); vig.smoothness.Override(0.35f); vig.rounded.Override(false);
            vig.color.Override(Color.black); vig.center.Override(new Vector2(0.5f,0.5f));
            Add<FilmGrain>().intensity.Override(0); Add<MotionBlur>().intensity.Override(0);
            Add<DepthOfField>().mode.Override(DepthOfFieldMode.Off); Add<ChromaticAberration>().intensity.Override(0);
            Add<LensDistortion>().intensity.Override(0); Add<ColorLookup>().contribution.Override(0);
            var volume = Undo.AddComponent<Volume>(Child("Global Volume",parent));
            volume.isGlobal = true; volume.priority = 100; volume.weight = 1; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }
        static void CreateProbes(Transform parent, Bounds b)
        {
            var positions = new List<Vector3>();
            for (float z = b.min.z+0.5f; z < b.max.z-0.25f; z += 2f)
                for (float x = b.min.x+0.5f; x < b.max.x-0.25f; x += 2f)
                    foreach (float h in new[] {0.35f,1.5f,2.8f})
                        if (h < b.size.y-0.2f) positions.Add(new Vector3(x,b.min.y+h,z));
            var probes = Undo.AddComponent<LightProbeGroup>(Child("Baked Bounce Probes",parent));
            probes.probePositions = positions.ToArray();
        }
        static Material CreateGlowMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("ComicShop/ToonEmission")) { name = name };
            material.SetColor("_BaseColor",color); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material,AssetPath(name+".mat")); return material;
        }
        static void CreateNeon(Transform parent, Vector3 position, Vector3 inward)
        {
            // Simple single-mesh OPEN sign. No font dependency or per-letter draw calls.
            string[] rows = {"111 111 111 110", "101 101 100 101", "101 111 110 101", "101 100 100 101", "111 100 111 101"};
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int y=0;y<5;y++) for (int x=0;x<15;x++) if (rows[y][x]=='1')
            {
                int n=vertices.Count; float px=(x-7)*0.06f, py=(2-y)*0.06f, r=0.026f;
                vertices.Add(new Vector3(px-r,py-r,0)); vertices.Add(new Vector3(px+r,py-r,0));
                vertices.Add(new Vector3(px+r,py+r,0)); vertices.Add(new Vector3(px-r,py+r,0));
                triangles.AddRange(new[] {n,n+1,n+2,n,n+2,n+3});
            }
            var mesh = new Mesh {name="OPEN Neon"}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh,AssetPath("OpenNeon.asset"));
            var go=Child("OPEN Neon (Generated)",parent); go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.LookRotation(-inward,Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r2=go.AddComponent<MeshRenderer>(); r2.sharedMaterial=CreateGlowMaterial("NeonGlow",new Color(1,0.08f,0.4f));
            r2.shadowCastingMode=ShadowCastingMode.Off; r2.receiveShadows=false;
        }
        static Mesh ReadableCopy(Mesh source)
        {
            using(var input=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var output=Mesh.AllocateWritableMeshData(1);
                bool applied=false;
                try
                {
                    var src=input[0]; var dst=output[0];
                    dst.SetVertexBufferParams(src.vertexCount,source.GetVertexAttributes());
                    for(int i=0;i<src.vertexBufferCount;i++) src.GetVertexData<byte>(i).CopyTo(dst.GetVertexData<byte>(i));
                    dst.SetIndexBufferParams(src.GetIndexData<byte>().Length / (src.indexFormat==IndexFormat.UInt16?2:4),src.indexFormat);
                    src.GetIndexData<byte>().CopyTo(dst.GetIndexData<byte>());
                    dst.subMeshCount=src.subMeshCount;
                    for(int i=0;i<src.subMeshCount;i++) dst.SetSubMesh(i,src.GetSubMesh(i),MeshUpdateFlags.DontRecalculateBounds);
                    var clone=new Mesh {name=source.name+"_LightmapUV"};
                    Mesh.ApplyAndDisposeWritableMeshData(output,clone,MeshUpdateFlags.DontRecalculateBounds);
                    applied=true; clone.bounds=source.bounds; return clone;
                }
                finally { if(!applied) output.Dispose(); }
            }
        }

        static void PrepareGeometry(ToonRoom room)
        {
            int fixedCount=0, dynamicCount=0;
            var copies=new Dictionary<Mesh,Mesh>();
            foreach(var r in SceneObjects<MeshRenderer>())
            {
                if(r.GetComponent<ToonHullBinding>()!=null) continue;
                var book=r.GetComponentInParent<BookItem>();
                if(book!=null || r.GetComponentInParent<Rigidbody>()!=null || r.GetComponentInParent<Animator>()!=null)
                {
                    Undo.RecordObject(r,"Configure dynamic probes"); Undo.RecordObject(r.gameObject,"Exclude dynamic GI contributor");
                    var flags=GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,flags & ~(StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic));
                    r.receiveGI=ReceiveGI.LightProbes; r.lightProbeUsage=LightProbeUsage.BlendProbes; dynamicCount++;
                    continue;
                }
                string path="";
                for(var t=r.transform;t!=null;t=t.parent) path+="/"+t.name.ToLowerInvariant();
                bool fixedSurface=ToonArchitectureRepair.IsArchitecture(r,room) || path.Contains("floor") || path.Contains("shelf") || path.Contains("shelv") || path.Contains("counter") || path.Contains("lightblockers");
                if(!fixedSurface) continue;
                var mf=r.GetComponent<MeshFilter>(); if(mf==null || mf.sharedMesh==null) continue;
                Undo.RecordObject(r,"Configure static GI"); Undo.RecordObject(r.gameObject,"Mark static architecture");
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                r.receiveGI=ReceiveGI.Lightmaps; fixedCount++;
                if(!mf.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                {
                    if(!copies.TryGetValue(mf.sharedMesh,out var copy))
                    {
                        copy=ReadableCopy(mf.sharedMesh);
                        if(!Unwrapping.GenerateSecondaryUVSet(copy))
                            throw new InvalidOperationException("Lightmap unwrap failed for " + mf.sharedMesh.name);
                        AssetDatabase.CreateAsset(copy,AssetPath("LightmapMesh.asset"));
                        copies.Add(mf.sharedMesh,copy);
                    }
                    Undo.RecordObject(mf,"Assign generated lightmap UV mesh"); mf.sharedMesh=copy;
                }
            }
            Debug.Log($"Bake geometry: {fixedCount} fixed surfaces, {dynamicCount} dynamic/probe renderers; {copies.Count} shared meshes received UV2 clones. Unrecognized furniture is left unchanged; Step 2 Architecture marker can mark an entire fixed hierarchy.");
        }

        static int ClearLegacyBlocks()
        {
            int count=0;
            foreach(var legacy in SceneObjects<ComicShopV16.ShopV16Appearance>())
                foreach(var r in legacy.GetComponentsInChildren<MeshRenderer>(true))
                    if(r.sharedMaterials.Length>0 && r.sharedMaterials.All(m=>m!=null && m.shader.name=="ComicShop/ToonLit"))
                    { r.SetPropertyBlock(null); count++; }
            return count;
        }
        [MenuItem(Menu + "2 - Audit Lighting and Performance")]
        public static void Audit()
        {
            var lights=SceneObjects<Light>().Where(l=>l.isActiveAndEnabled).ToArray();
            var renderers=SceneObjects<MeshRenderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
            int blocks=renderers.Count(r=>r.HasPropertyBlock());
            int shadows=lights.Count(l=>l.shadows!=LightShadows.None);
            Debug.Log($"Toon audit: lights={lights.Length}, realtime shadow lights={shadows}; renderers={renderers.Length}, renderers with property blocks={blocks}; baked lightmaps={LightmapSettings.lightmaps.Length}. SRP Batcher does not merge these renderer draws. Check Frame Debugger in a player build. Window: two spots 48 each. Pendant: eight spots 8 each. No Directional Light expected.");
            foreach(var r in renderers)
            {
                var f=r.GetComponent<MeshFilter>();
                if(GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI) && f!=null && f.sharedMesh!=null && !f.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                    Debug.LogWarning("Contribute GI renderer has no UV2; use mesh importer Generate Lightmap UVs before baking: "+r.name,r);
            }
            foreach(var l in lights.Where(l=>l.type==LightType.Directional)) Debug.LogWarning("Unexpected active directional light",l);
            if(shadows!=3) Debug.LogWarning("Expected exactly three realtime shadow-casting lights.");
            if(LightmapSettings.lightmaps.Length==0) Debug.LogWarning("No baked lightmaps yet. ShadowLift is readability fill, not baked GI.");
        }
        [MenuItem(Menu + "3 - Bake Bounce Lighting")]
        static void Bake()
        {
            if (Lightmapping.isRunning || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Lightmapping.BakeAsync()) Debug.LogError("Unity could not start the lighting bake. Check the Lighting window and lightmapper device.");
        }
    }
}
