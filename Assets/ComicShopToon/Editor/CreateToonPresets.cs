using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ComicShop.Rendering.Editor
{
    public static class CreateToonPresets
    {
        [MenuItem("Tools/ComicShop/Create Toon Materials And Volume")]
        static void Create()
        {
            const string folder = "Assets/ComicShopToon/Presets";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/ComicShopToon", "Presets");
            Shader shader = Shader.Find("ComicShop/ToonLit");
            if (shader == null) { Debug.LogError("Import ToonLit.shader first."); return; }
            string[] names = { "Book", "Wood", "Wallpaper", "FloorCream", "FloorGreen", "Mustard", "OrangeRed", "Sage", "Cream" };
            for (int i = 0; i < names.Length; i++)
            {
                var mat = new Material(shader) { name = "Toon_" + names[i], enableInstancing = true };
                string[] colors = { "FFFFFF", "8A5A33", "C8913F", "DCD0A8", "6E8768", "D9A441", "E2542B", "5F7A66", "E8E3D7" };
                ColorUtility.TryParseHtmlString("#" + colors[i], out Color baseColor);
                mat.SetColor("_BaseColor", baseColor);
                mat.SetFloat("_HalftoneEnabled", 1);
                mat.EnableKeyword("_TOON_HALFTONE");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + mat.name + ".mat"));
            }
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ComicShopVolume.asset");
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                T component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }
            var tone = Add<Tonemapping>(); tone.mode.Override(TonemappingMode.None);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0); color.contrast.Override(8);
            color.saturation.Override(10); color.colorFilter.Override(Color.white);
            color.hueShift.Override(0);
            var balance = Add<WhiteBalance>(); balance.temperature.Override(-3); balance.tint.Override(2);
            var split = Add<ShadowsMidtonesHighlights>();
            split.shadows.Override(new Vector4(.92f, .86f, 1.08f, -.02f));
            split.midtones.Override(new Vector4(1, .99f, .97f, 0));
            split.highlights.Override(new Vector4(1.06f, 1.02f, .94f, 0));
            split.shadowsStart.Override(0); split.shadowsEnd.Override(.3f);
            split.highlightsStart.Override(.6f); split.highlightsEnd.Override(1);
            var bloom = Add<Bloom>(); bloom.threshold.Override(1.15f);
            bloom.intensity.Override(.18f); bloom.scatter.Override(.35f);
            bloom.tint.Override(Color.white); bloom.clamp.Override(8);
            bloom.highQualityFiltering.Override(false); bloom.dirtIntensity.Override(0);
            var vignette = Add<Vignette>(); vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(.5f, .5f));
            vignette.intensity.Override(.12f); vignette.smoothness.Override(.35f);
            vignette.rounded.Override(false);
            var grain = Add<FilmGrain>(); grain.intensity.Override(0);
            grain.response.Override(.8f);
            Add<MotionBlur>().intensity.Override(0);
            Add<DepthOfField>().mode.Override(DepthOfFieldMode.Off);
            Add<ChromaticAberration>().intensity.Override(0);
            Add<LensDistortion>().intensity.Override(0);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            Debug.Log("Created new Toon presets. Assign the profile to a Global Volume; assign book textures to Toon_Book materials.");
        }
    }
}
