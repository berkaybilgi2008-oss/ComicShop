#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ComicShop.Rendering.Editor
{
    public static class ComicShopAutoSetup
    {
        const string Menu = "Tools/ComicShop/Apply Warm Shop Look";
        const string Root = "Assets/ComicShopToon/AutoPresets";
        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
        static bool ContainsAny(string value, params string[] words)
        {
            value = value.ToLowerInvariant();
            foreach (string word in words) if (value.Contains(word)) return true;
            return false;
        }
        static bool IsBook(Component renderer)
        {
            foreach (MonoBehaviour component in renderer.GetComponentsInParent<MonoBehaviour>(true))
                if (component != null && ContainsAny(component.GetType().Name,
                    "bookitem", "networkbook", "booktooneffect")) return true;
            return false;
        }
        static void Changed(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (target is Component component)
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        [MenuItem(Menu, true)]
        static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null;

        [MenuItem(Menu)]
        static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Shader toon = Shader.Find("ComicShop/ToonLit");
            if (!scene.IsValid() || !scene.isLoaded || toon == null)
            {
                EditorUtility.DisplayDialog("ComicShop", "Open the shop scene and import ComicShop/ToonLit first.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/ComicShopToon"))
                AssetDatabase.CreateFolder("Assets", "ComicShopToon");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/ComicShopToon", "AutoPresets");
            string folder = AssetDatabase.GenerateUniqueAssetPath(Root + "/WarmShop");
            AssetDatabase.CreateFolder(Root, System.IO.Path.GetFileName(folder));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Warm ComicShop Look");
            var cache = new Dictionary<Material, Material>();
            int changed = 0, skipped = 0;
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    if (IsBook(renderer)) { skipped++; continue; }
                    Material[] slots = renderer.sharedMaterials;
                    bool dirty = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        Material source = slots[i];
                        if (source == null || source.shader == null) continue;
                        string shaderName = source.shader.name;
                        // Preserve glass, particles, ink hulls, signs and special-purpose shaders.
                        bool supported = shaderName == "ComicShop/ToonLit"
                            || shaderName == "Universal Render Pipeline/Lit"
                            || shaderName == "Universal Render Pipeline/Simple Lit";
                        if (!supported || source.IsKeywordEnabled("_EMISSION") || source.renderQueue >= 2450
                            || (source.HasProperty("_Surface") && source.GetFloat("_Surface") > .5f)
                            || source.IsKeywordEnabled("_ALPHATEST_ON")) { skipped++; continue; }
                        if (!cache.TryGetValue(source, out Material material))
                        {
                            material = MakeMaterial(source, toon);
                            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/WarmMaterial.mat"));
                            cache.Add(source, material);
                        }
                        slots[i] = material;
                        dirty = true;
                    }
                    if (!dirty) continue;
                    Undo.RecordObject(renderer, "Apply Warm Shop Materials");
                    renderer.sharedMaterials = slots;
                    Changed(renderer);
                    changed++;
                }
                SetupVolume(scene, folder);
                SetupLights(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    Undo.RecordObject(camera, "Configure Toon Camera");
                    camera.allowMSAA = false;
                    camera.allowDynamicResolution = false;
                    Changed(camera);
                    UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                    Undo.RecordObject(data, "Enable Toon Post Processing");
                    data.volumeLayerMask = data.volumeLayerMask.value | 1;
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    Changed(data);
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"ComicShop: {changed} renderers updated with {cache.Count} shared material copies; {skipped} book/special entries preserved. Ctrl+Z restores scene assignments. Generated assets remain in {folder}. Existing baked lighting is unchanged.");
                EditorUtility.DisplayDialog("ComicShop", "Warm shop look applied.\n\nCtrl+Z restores scene changes. Original materials and book covers are preserved.\nSave the scene only after checking the result.", "OK");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                EditorUtility.DisplayDialog("ComicShop", "Setup failed; scene changes were reverted. Check Console. Generated unused assets may remain in AutoPresets.", "OK");
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
        static Material MakeMaterial(Material source, Shader toon)
        {
            bool alreadyToon = source.shader == toon;
            Material material = alreadyToon ? new Material(source) : new Material(toon);
            material.name = source.name + "_WarmShop";
            material.enableInstancing = true;
            if (!alreadyToon)
            {
                string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(map))
                {
                    material.SetTexture("_BaseMap", source.GetTexture(map));
                    material.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
                }
                if (source.HasProperty("_BaseColor")) material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                else if (source.HasProperty("_Color")) material.SetColor("_BaseColor", source.GetColor("_Color"));
                if (source.HasProperty("_EmissionColor") && source.IsKeywordEnabled("_EMISSION"))
                    material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                material.globalIlluminationFlags = source.globalIlluminationFlags;
                material.SetFloat("_Palette", 0);
            }
            material.SetColor("_WoodLit", Hex("8A5A33"));
            material.SetColor("_WoodShadow", Hex("795033"));
            material.SetColor("_WallpaperLit", Hex("C8913F"));
            material.SetColor("_WallpaperShadow", Hex("956336"));
            // Warm chromatic shadow for custom textured surfaces as well as palette materials.
            material.SetColor("_ShadowTint", Hex("BD956F"));
            material.SetFloat("_ShadowHueShift", 0);
            material.SetFloat("_PaletteShadowMix", 1);
            material.SetFloat("_ShadowFloor", .9f);
            material.SetFloat("_ShadowSteps", 2);
            material.SetFloat("_RampSmoothness", .02f);
            material.SetFloat("_BakedSteps", 3);
            material.SetFloat("_BakedInfluence", .15f);
            material.SetFloat("_HalftoneEnabled", 0);
            material.DisableKeyword("_TOON_HALFTONE");
            material.SetFloat("_SpecEnabled", 0);
            material.DisableKeyword("_TOON_SPECULAR");
            material.SetFloat("_RimEnabled", 0);
            material.DisableKeyword("_TOON_RIM");
            return material;
        }
        static void SetupVolume(Scene scene, string folder)
        {
            Volume target = null;
            float priority = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
            {
                if (volume.gameObject.name == "ComicShop Warm Look") target = volume;
                else priority = Mathf.Max(priority, volume.priority + 1);
            }
            if (target == null)
            {
                var go = new GameObject("ComicShop Warm Look");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create Warm Shop Volume");
                target = Undo.AddComponent<Volume>(go);
            }
            Undo.RecordObject(target, "Configure Warm Shop Volume");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, folder + "/WarmShopVolume.asset");
            T Add<T>() where T : VolumeComponent
            {
                T item = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(item, profile);
                return item;
            }
            Add<Tonemapping>().mode.Override(TonemappingMode.None);
            var c = Add<ColorAdjustments>();
            c.postExposure.Override(0); c.contrast.Override(3); c.saturation.Override(0);
            c.colorFilter.Override(Color.white); c.hueShift.Override(0);
            var white = Add<WhiteBalance>(); white.temperature.Override(0); white.tint.Override(0);
            var split = Add<ShadowsMidtonesHighlights>();
            split.shadows.Override(new Vector4(1,1,1,0));
            split.midtones.Override(new Vector4(1,1,1,0));
            split.highlights.Override(new Vector4(1,1,1,0));
            var bloom = Add<Bloom>(); bloom.threshold.Override(1.15f);
            bloom.intensity.Override(.12f); bloom.scatter.Override(.3f);
            Add<Vignette>().intensity.Override(.08f);
            Add<FilmGrain>().intensity.Override(0);
            Add<MotionBlur>().intensity.Override(0);
            Add<DepthOfField>().mode.Override(DepthOfFieldMode.Off);
            target.sharedProfile = profile; target.isGlobal = true;
            target.weight = 1; target.priority = priority;
            Changed(target); EditorUtility.SetDirty(profile);
        }
        static void SetupLights(Scene scene)
        {
            // Retain source positions, ranges, energy and bake mode: no stale-bake relighting.
            // Only reduce excessive shadow-caster cost; this does not recolor the whole room.
            Light sun = null, hero = null;
            var lights = new List<Light>();
            foreach (GameObject root in scene.GetRootGameObjects())
                lights.AddRange(root.GetComponentsInChildren<Light>(true));
            foreach (Light light in lights)
            {
                if (!light.enabled || !light.gameObject.activeInHierarchy || light.lightmapBakeType == LightmapBakeType.Baked) continue;
                if (light.type == LightType.Directional && (sun == null || light.intensity > sun.intensity)) sun = light;
                if (light.type == LightType.Spot && light.shadows != LightShadows.None
                    && (hero == null || light.intensity > hero.intensity)) hero = light;
            }
            foreach (Light light in lights)
            {
                if (light.lightmapBakeType == LightmapBakeType.Baked || light.shadows == LightShadows.None) continue;
                Undo.RecordObject(light, "Limit Toon Shadow Lights");
                light.shadows = light == sun || light == hero ? LightShadows.Hard : LightShadows.None;
                Changed(light);
            }
        }
    }
}
#endif
