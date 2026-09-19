using UnityEngine;

namespace ComicShop.Rendering
{
    [CreateAssetMenu(menuName = "ComicShop/Toon Style", fileName = "ToonStyle")]
    public sealed class ToonStyleAsset : ScriptableObject
    {
        [Range(2, 3)] public float ShadowSteps = 2f;
        [Range(0f, 0.05f)] public float RampSmoothness = 0.02f;
         public Color ShadowTint = new Color(0.22745098f,0.18039216f,0.32156863f,1f);
        [Range(2, 8)] public float BakedSteps = 3f;
        [Range(0, 1)] public float BakedInfluence = 0.2f;
        [Min(0.01f)] public float LightFalloffScale = 8f;
        [Range(0, 1)] public float SpecEnabled = 0f;
        [Range(0, 1)] public float SpecThreshold = 0.96f;
         public Color SpecColor = new Color(1f,0.98f,0.9f,1f);
        [Range(0, 2)] public float SpecStrength = 0.4f;
        [Range(0, 1)] public float RimEnabled = 0f;
        [Range(0, 1)] public float RimThreshold = 0.75f;
        [Range(0, 1)] public float RimLitOnly = 1f;
         public Color RimColor = new Color(0.85f,0.9f,1f,1f);
        [Range(0, 2)] public float RimStrength = 0.2f;
        [Range(0, 1)] public float HalftoneEnabled = 0f;
        [Range(4, 32)] public float HalftoneScale = 8f;
        [Range(0, 1)] public float HalftoneStrength = 0.4f;
        [Range(0, 180)] public float HalftoneAngle = 45f;
        [Range(0.05f, 0.45f)] public float HalftoneRadius = 0.27f;
         public Color HalftoneColor = new Color(0.14117647f,0.10980392f,0.20784314f,1f);
        [Header("Interior exposure — global only")]
        [Tooltip("Art-directed shadow readability floor, not baked or physical GI.")]
        [Range(0f, 0.3f)] public float ShadowLift = 0.12f;
        [Range(0f, 4f)] public float BakedExposure = 2f;
        [Range(1f, 4f)] public float DirectMax = 2f;
        [Range(0f, 8f)] public float EmissionGain = 4f;
        [Header("Outlines — shared by all materials")]
        public Color OutlineColor = new Color(0.025f, 0.018f, 0.04f, 1f);
        [Range(0f, 0.1f)] public float OutlineWidth = 0.01f;
        [Min(0.1f)] public float OutlineReferenceDistance = 5f;
        [Range(0f, 4f)] public float OutlineThicknessPixels = 1.5f;
        [Range(0.001f, 0.2f)] public float DepthThreshold = 0.035f;
        [Range(0.01f, 2f)] public float NormalThreshold = 0.25f;
        [HideInInspector] public Shader ToonShader;
        static readonly int[] Ids = {
            Shader.PropertyToID("_ToonShadowSteps"),
            Shader.PropertyToID("_ToonRampSmoothness"),
            Shader.PropertyToID("_ToonShadowTint"),
            Shader.PropertyToID("_ToonBakedSteps"),
            Shader.PropertyToID("_ToonBakedInfluence"),
            Shader.PropertyToID("_ToonLightFalloffScale"),
            Shader.PropertyToID("_ToonSpecEnabled"),
            Shader.PropertyToID("_ToonSpecThreshold"),
            Shader.PropertyToID("_ToonSpecColor"),
            Shader.PropertyToID("_ToonSpecStrength"),
            Shader.PropertyToID("_ToonRimEnabled"),
            Shader.PropertyToID("_ToonRimThreshold"),
            Shader.PropertyToID("_ToonRimLitOnly"),
            Shader.PropertyToID("_ToonRimColor"),
            Shader.PropertyToID("_ToonRimStrength"),
            Shader.PropertyToID("_ToonHalftoneEnabled"),
            Shader.PropertyToID("_ToonHalftoneScale"),
            Shader.PropertyToID("_ToonHalftoneStrength"),
            Shader.PropertyToID("_ToonHalftoneAngle"),
            Shader.PropertyToID("_ToonHalftoneRadius"),
            Shader.PropertyToID("_ToonHalftoneColor")
        };
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int OutlineReferenceId = Shader.PropertyToID("_OutlineReferenceDistance");
        static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        public void Apply()
        {
            Shader.SetGlobalFloat("_ToonShadowLift", Mathf.Clamp(ShadowLift, 0f, 0.3f));
            Shader.SetGlobalFloat("_ToonBakedExposure", Mathf.Clamp(BakedExposure, 0f, 4f));
            Shader.SetGlobalFloat("_ToonDirectMax", Mathf.Clamp(DirectMax, 1f, 4f));
            Shader.SetGlobalFloat("_ToonEmissionGain", Mathf.Clamp(EmissionGain, 0f, 8f));
            Shader.SetGlobalFloat(Ids[0], ShadowSteps);
            Shader.SetGlobalFloat(Ids[1], RampSmoothness);
            Shader.SetGlobalColor(Ids[2], QualitySettings.activeColorSpace == ColorSpace.Linear ? ShadowTint.linear : ShadowTint);
            Shader.SetGlobalFloat(Ids[3], BakedSteps);
            Shader.SetGlobalFloat(Ids[4], BakedInfluence);
            Shader.SetGlobalFloat(Ids[5], LightFalloffScale);
            Shader.SetGlobalFloat(Ids[6], SpecEnabled);
            Shader.SetGlobalFloat(Ids[7], SpecThreshold);
            Shader.SetGlobalColor(Ids[8], QualitySettings.activeColorSpace == ColorSpace.Linear ? SpecColor.linear : SpecColor);
            Shader.SetGlobalFloat(Ids[9], SpecStrength);
            Shader.SetGlobalFloat(Ids[10], RimEnabled);
            Shader.SetGlobalFloat(Ids[11], RimThreshold);
            Shader.SetGlobalFloat(Ids[12], RimLitOnly);
            Shader.SetGlobalColor(Ids[13], QualitySettings.activeColorSpace == ColorSpace.Linear ? RimColor.linear : RimColor);
            Shader.SetGlobalFloat(Ids[14], RimStrength);
            Shader.SetGlobalFloat(Ids[15], HalftoneEnabled);
            Shader.SetGlobalFloat(Ids[16], HalftoneScale);
            Shader.SetGlobalFloat(Ids[17], HalftoneStrength);
            Shader.SetGlobalFloat(Ids[18], HalftoneAngle);
            Shader.SetGlobalFloat(Ids[19], HalftoneRadius);
            Shader.SetGlobalColor(Ids[20], QualitySettings.activeColorSpace == ColorSpace.Linear ? HalftoneColor.linear : HalftoneColor);
            Shader.SetGlobalColor(OutlineColorId, QualitySettings.activeColorSpace == ColorSpace.Linear ? OutlineColor.linear : OutlineColor);
            Shader.SetGlobalFloat(OutlineWidthId, Mathf.Max(0, OutlineWidth));
            Shader.SetGlobalFloat(OutlineReferenceId, Mathf.Max(0.1f, OutlineReferenceDistance));
            Shader.SetGlobalFloat(OutlineThicknessId, Mathf.Max(0, OutlineThicknessPixels));
            Shader.SetGlobalFloat(DepthThresholdId, Mathf.Max(0.001f, DepthThreshold));
            Shader.SetGlobalFloat(NormalThresholdId, Mathf.Max(0.01f, NormalThreshold));
            SetKeyword("_TOON_GLOBAL_SPECULAR", SpecEnabled > 0.5f);
            SetKeyword("_TOON_GLOBAL_RIM", RimEnabled > 0.5f);
            SetKeyword("_TOON_GLOBAL_HALFTONE", HalftoneEnabled > 0.5f);
        }
        static void SetKeyword(string keyword, bool enabled)
        {
            if (enabled) Shader.EnableKeyword(keyword);
            else Shader.DisableKeyword(keyword);
        }
        // OnValidate may be called off the main thread. Controller/editor update
        // publishes the edited asset on the next main-thread tick.
        void OnValidate() { }
    }
}
