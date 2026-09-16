Shader "ComicShop/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        [Toggle(_TOON_HALFTONE)] _HalftoneEnabled("Halftone", Float) = 1
        [ToggleUI] _OutlineEnabled("Outline Mask", Float) = 1
        [HideInInspector] _UseLocalStyle("Advanced Local Overrides", Float) = 0
        [HideInInspector] _OverrideShadowSteps("Override ShadowSteps", Float) = 0
        [HideInInspector] _LocalShadowSteps("ShadowSteps", Float) = 2
        [HideInInspector] _OverrideRampSmoothness("Override RampSmoothness", Float) = 0
        [HideInInspector] _LocalRampSmoothness("RampSmoothness", Float) = 0.02
        [HideInInspector] _OverrideShadowTint("Override ShadowTint", Float) = 0
        [HideInInspector] _LocalShadowTint("ShadowTint", Color) = (0.22745098,0.18039216,0.32156863,1)
        [HideInInspector] _OverrideBakedSteps("Override BakedSteps", Float) = 0
        [HideInInspector] _LocalBakedSteps("BakedSteps", Float) = 3
        [HideInInspector] _OverrideBakedInfluence("Override BakedInfluence", Float) = 0
        [HideInInspector] _LocalBakedInfluence("BakedInfluence", Float) = 0.2
        [HideInInspector] _OverrideLightFalloffScale("Override LightFalloffScale", Float) = 0
        [HideInInspector] _LocalLightFalloffScale("LightFalloffScale", Float) = 8
        [HideInInspector] _OverrideSpecEnabled("Override SpecEnabled", Float) = 0
        [HideInInspector] _LocalSpecEnabled("SpecEnabled", Float) = 0
        [HideInInspector] _OverrideSpecThreshold("Override SpecThreshold", Float) = 0
        [HideInInspector] _LocalSpecThreshold("SpecThreshold", Float) = 0.96
        [HideInInspector] _OverrideSpecColor("Override SpecColor", Float) = 0
        [HideInInspector] _LocalSpecColor("SpecColor", Color) = (1,0.98,0.9,1)
        [HideInInspector] _OverrideSpecStrength("Override SpecStrength", Float) = 0
        [HideInInspector] _LocalSpecStrength("SpecStrength", Float) = 0.4
        [HideInInspector] _OverrideRimEnabled("Override RimEnabled", Float) = 0
        [HideInInspector] _LocalRimEnabled("RimEnabled", Float) = 0
        [HideInInspector] _OverrideRimThreshold("Override RimThreshold", Float) = 0
        [HideInInspector] _LocalRimThreshold("RimThreshold", Float) = 0.75
        [HideInInspector] _OverrideRimLitOnly("Override RimLitOnly", Float) = 0
        [HideInInspector] _LocalRimLitOnly("RimLitOnly", Float) = 1
        [HideInInspector] _OverrideRimColor("Override RimColor", Float) = 0
        [HideInInspector] _LocalRimColor("RimColor", Color) = (0.85,0.9,1,1)
        [HideInInspector] _OverrideRimStrength("Override RimStrength", Float) = 0
        [HideInInspector] _LocalRimStrength("RimStrength", Float) = 0.2
        [HideInInspector] _OverrideHalftoneEnabled("Override HalftoneEnabled", Float) = 0
        [HideInInspector] _LocalHalftoneEnabled("HalftoneEnabled", Float) = 1
        [HideInInspector] _OverrideHalftoneScale("Override HalftoneScale", Float) = 0
        [HideInInspector] _LocalHalftoneScale("HalftoneScale", Float) = 8
        [HideInInspector] _OverrideHalftoneStrength("Override HalftoneStrength", Float) = 0
        [HideInInspector] _LocalHalftoneStrength("HalftoneStrength", Float) = 0.4
        [HideInInspector] _OverrideHalftoneAngle("Override HalftoneAngle", Float) = 0
        [HideInInspector] _LocalHalftoneAngle("HalftoneAngle", Float) = 45
        [HideInInspector] _OverrideHalftoneRadius("Override HalftoneRadius", Float) = 0
        [HideInInspector] _LocalHalftoneRadius("HalftoneRadius", Float) = 0.27
        [HideInInspector] _OverrideHalftoneColor("Override HalftoneColor", Float) = 0
        [HideInInspector] _LocalHalftoneColor("HalftoneColor", Color) = (0.14117647,0.10980392,0.20784314,1)
        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps",2DArray)=""{}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd",2DArray)=""{}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks",2DArray)=""{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma shader_feature_local_fragment _ _TOON_LOCAL_STYLE
            #pragma shader_feature_local _ _NORMALMAP
            #pragma multi_compile_fragment _ _TOON_GLOBAL_SPECULAR
            #pragma multi_compile_fragment _ _TOON_GLOBAL_RIM
            #pragma multi_compile_fragment _ _TOON_GLOBAL_HALFTONE
            #pragma shader_feature_local_fragment _ _TOON_HALFTONE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "ToonForward.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back ZWrite On ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "ToonAuxiliary.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Back ZWrite On ZTest LEqual
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex AuxVertex
            #pragma fragment DepthFragment
            
            #include "ToonAuxiliary.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Back ZWrite On ZTest LEqual
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex AuxVertex
            #pragma fragment NormalsFragment
            #pragma shader_feature_local _ _NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "ToonAuxiliary.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ToonMask"
            Tags { "LightMode"="ToonMask" }
            Cull Back ZWrite On ZTest LEqual
            ZWrite Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex AuxVertex
            #pragma fragment MaskFragment
            
            #include "ToonAuxiliary.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ToonMetaVertex
            #pragma fragment ToonMetaFragment
            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct MetaAttributes
            {
                float4 positionOS:POSITION;
                float2 uv:TEXCOORD0;
                float2 uv1:TEXCOORD1;
                float2 uv2:TEXCOORD2;
            };
            struct MetaVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            MetaVaryings ToonMetaVertex(MetaAttributes i)
            {
                MetaVaryings o;
                o.positionCS=MetaVertexPosition(i.positionOS,i.uv1,i.uv2,unity_LightmapST,unity_DynamicLightmapST);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
                return o;
            }
            half4 ToonMetaFragment(MetaVaryings i):SV_Target
            {
                MetaInput m=(MetaInput)0;
                m.Albedo=ToonAlbedo(i.uv);
                m.Emission=0;
                return MetaFragment(m);
            }
            ENDHLSL
        }
    }
    CustomEditor "ComicShop.Rendering.Editor.ToonLitGUI"
    FallBack Off
}
