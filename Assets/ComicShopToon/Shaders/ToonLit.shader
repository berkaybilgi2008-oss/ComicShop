Shader "ComicShop/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo",2D)="white"{}
        [MainColor] _BaseColor("Albedo Tint",Color)=(1,1,1,1)
        _Palette("Palette", Float) = 0
        _ShadowTint("ShadowTint",Color)=(0.22745098,0.18039216,0.32156863,1)
        _WoodLit("WoodLit",Color)=(0.54117647,0.35294118,0.20000000,1)
        _WoodShadow("WoodShadow",Color)=(0.29019608,0.18431373,0.13333333,1)
        _WallpaperLit("WallpaperLit",Color)=(0.78431373,0.56862745,0.24705882,1)
        _WallpaperShadow("WallpaperShadow",Color)=(0.47843137,0.30980392,0.16470588,1)
        _FloorCream("FloorCream",Color)=(0.86274510,0.81176471,0.65882353,1)
        _FloorGreen("FloorGreen",Color)=(0.43137255,0.52941176,0.40784314,1)
        _Mustard("Mustard",Color)=(0.85098039,0.64313725,0.25490196,1)
        _OrangeRed("OrangeRed",Color)=(0.88627451,0.32941176,0.16862745,1)
        _Sage("Sage",Color)=(0.37254902,0.47843137,0.40000000,1)
        _Cream("Cream",Color)=(0.90980392,0.89019608,0.84313725,1)
        _HalftoneColor("HalftoneColor",Color)=(0.14117647,0.10980392,0.20784314,1)
        _SpecColor("SpecColor",Color)=(0.90980392,0.89019608,0.84313725,1)
        _RimColor("RimColor",Color)=(0.85098039,0.64313725,0.25490196,1)
        [HDR] _EmissionColor("Emission",Color)=(0,0,0,1)
        _PaletteShadowMix("Shadow Anchor Mix",Range(0,1))=0.25
        _ShadowHueShift("Cool Hue Shift",Range(0,1))=0.65
        _ShadowSteps("Light Steps",Range(2,3))=2
        _RampSmoothness("Ramp Transition Width",Range(0.00001,0.05))=0.02
        _BakedSteps("Baked Steps",Range(2,8))=3
        _BakedInfluence("Baked Influence",Range(0,1))=0.2
        _ShadowFloor("Shadow Floor",Range(0,1))=0.65
        _DirectGain("Direct Gain",Range(0,2))=1
        _LightFalloffScale("Punctual Falloff Scale",Float)=8
        [Toggle(_TOON_SPECULAR)] _SpecEnabled("Toon Specular",Float)=0
        _SpecSize("Specular Size",Range(0.001,0.5))=0.04
        [Toggle(_TOON_RIM)] _RimEnabled("Rim",Float)=0
        _RimPower("Rim Power",Float)=3
        _RimThreshold("Rim Threshold",Range(0,1))=0.65
        [ToggleUI] _RimLitOnly("Rim Lit Side Only",Float)=1
        [Toggle(_TOON_HALFTONE)] _HalftoneEnabled("Halftone",Float)=0
        _HalftoneScale("Halftone Spacing Pixels",Range(4,32))=8
        _HalftoneStrength("Halftone Strength",Range(0,1))=0.4
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma shader_feature_local_fragment _ _TOON_SPECULAR
            #pragma shader_feature_local_fragment _ _TOON_RIM
            #pragma shader_feature_local_fragment _ _TOON_HALFTONE
            #pragma multi_compile_fragment _ _COMIC_HALFTONE_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex AuxVertex
            #pragma fragment NormalsFragment
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
                m.Emission=_EmissionColor.rgb;
                return MetaFragment(m);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
