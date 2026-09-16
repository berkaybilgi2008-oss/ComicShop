Shader "ComicShop/V16 Source Toon"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Source linear color", Color) = (1,1,1,1)
        _Emission("Emission", Vector) = (0,0,0,0)
        _Unlit("Unlit", Float) = 0
        [HideInInspector] _Smooth("Legacy ramp", Float) = 0
        _FlipY("Flip UV", Float) = 0
        _Cull("Cull", Float) = 2
        _SrcBlend("Source blend", Float) = 5
        _DstBlend("Destination blend", Float) = 10
        _ZWrite("Depth write", Float) = 1
        _Cutoff("Alpha cutoff", Float) = 0
        _Receive("Receive shadows", Float) = 1
        _OffsetFactor("Depth offset factor", Float) = 0
        _OffsetUnits("Depth offset units", Float) = 0
        _ShadowColor("Warm shadow tint", Color) = (0.52,0.39,0.30,1)
        _AmbientFloor("Ambient floor", Range(0,0.3)) = 0.06
        _LightSteps("Light steps", Range(2,3)) = 3
        _LightThreshold("Light threshold", Range(0.01,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _Emission, _ShadowColor;
        float _Unlit, _Smooth, _FlipY, _Cutoff, _Receive;
        float _Cull, _SrcBlend, _DstBlend, _ZWrite, _OffsetFactor, _OffsetUnits;
        float _AmbientFloor, _LightSteps, _LightThreshold;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float fog : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            if (_FlipY > 0.5) o.uv.y = 1 - o.uv.y;
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }
        half4 Surface(Varyings i)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
            clip(tex.a * _BaseColor.a - max(_Cutoff, 0.001));
            return tex * _BaseColor;
        }
        // Quantize the attenuated contribution: a disabled or occluded light adds zero.
        // Intensity remains continuous; light boundaries remain discrete.
        half3 ToonLight(Light light, half3 normal)
        {
            float shadow = lerp(1.0, light.shadowAttenuation, saturate(_Receive));
            float energy = max(light.color.r, max(light.color.g, light.color.b));
            float exposure = saturate(dot(normal, light.direction)) *
                light.distanceAttenuation * shadow;
            float bands = max(2.0, round(_LightSteps));
            float band = floor(saturate(exposure / max(_LightThreshold, 0.001)) *
                (bands - 1.0) + 0.5) / (bands - 1.0);
            return light.color * band * step(0.00001, energy);
        }
        ENDHLSL

        Pass
        {
            Name "SourceToon"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull] Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
            Offset [_OffsetFactor], [_OffsetUnits]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 albedo = Surface(i);
                half3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #endif
                half3 direct = ToonLight(GetMainLight(shadowCoord, i.positionWS, half4(1,1,1,1)), n);
                #if USE_CLUSTER_LIGHT_LOOP
                    UNITY_LOOP for (uint lightIndex = 0;
                        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                        ++lightIndex)
                    {
                        direct += ToonLight(GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1)), n);
                    }
                #endif
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        direct += ToonLight(GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1)), n);
                    LIGHT_LOOP_END
                #endif
                half3 lighting = _ShadowColor.rgb * _AmbientFloor + direct;
                half3 color = albedo.rgb * lerp(lighting, half3(1,1,1), saturate(_Unlit)) + _Emission.rgb;
                return half4(MixFog(color, i.fog), albedo.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings o = Vert(input);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = normalize(_LightPosition - o.positionWS);
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, normalize(o.normalWS), direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
                #endif
                return o;
            }
            half4 ShadowFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Surface(i);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Surface(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalsFrag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                Surface(i);
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}

