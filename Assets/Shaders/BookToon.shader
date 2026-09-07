Shader "Custom/BookToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _ShadowColor ("Comic Shadow", Color) = (0.38,0.40,0.46,1)
        _LightThreshold ("Light Threshold", Range(0,1)) = 0.58
        _ShadowSoftness ("Shadow Softness", Range(0.001,0.25)) = 0.035
        _InkColor ("Ink Color", Color) = (0.015,0.01,0.008,1)
        _InkStrength ("Ink Strength", Range(0,2)) = 1.0
        _InkThreshold ("Ink Threshold", Range(0,1)) = 0.22
        _CreaseStrength ("Crease Strength", Range(0,4)) = 1.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float4 _ShadowColor;
                float _LightThreshold; float _ShadowSoftness; float4 _InkColor;
                float _InkStrength; float _InkThreshold; float _CreaseStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes IN) { Varyings OUT; VertexPositionInputs p=GetVertexPositionInputs(IN.positionOS.xyz); VertexNormalInputs n=GetVertexNormalInputs(IN.normalOS); OUT.positionCS=p.positionCS; OUT.positionWS=p.positionWS; OUT.normalWS=normalize(n.normalWS); OUT.uv=TRANSFORM_TEX(IN.uv,_BaseMap); return OUT; }
            half3 ToonBand(half3 a,half3 lc,half3 ld,half3 n,half att) { half d=saturate(dot(normalize(n),normalize(ld))); half b=smoothstep(_LightThreshold-_ShadowSoftness,_LightThreshold+_ShadowSoftness,d); return a*lerp(_ShadowColor.rgb,half3(1,1,1),b)*lc*att; }
            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,IN.uv)*_BaseColor; half3 n=normalize(IN.normalWS);
                Light ml=GetMainLight(); half3 color=ToonBand(albedo.rgb,ml.color,ml.direction,n,ml.distanceAttenuation*ml.shadowAttenuation);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count=GetAdditionalLightsCount(); for(uint i=0u;i<count;i++){ Light l=GetAdditionalLight(i,IN.positionWS); color+=ToonBand(albedo.rgb,l.color,l.direction,n,l.distanceAttenuation*l.shadowAttenuation); }
                #endif
                half3 viewDir=GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half facing=saturate(abs(dot(n,viewDir)));
                half silhouetteInk=1.0h-smoothstep(_InkThreshold,_InkThreshold+0.16h,facing);
                half normalChange=max(length(ddx(n)),length(ddy(n)));
                half creaseInk=smoothstep(0.10h,0.55h,saturate(normalChange*_CreaseStrength));
                half ink=saturate(max(silhouetteInk,creaseInk)*_InkStrength);
                return half4(lerp(color,_InkColor.rgb,ink),albedo.a);
            }
            ENDHLSL
        }
    }
}
