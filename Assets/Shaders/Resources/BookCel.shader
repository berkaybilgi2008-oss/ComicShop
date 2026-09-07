Shader "ComicShop/Book Cel"
{
    Properties
    {
        [MainTexture] _BaseMap("Original cover", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ShadowTint("Cool paper shadow", Color) = (0.48,0.49,0.63,1)
        _MidTint("Midtone", Color) = (0.80,0.78,0.85,1)
        _LightTint("Warm paper light", Color) = (1.06,1.02,0.94,1)
        _Saturation("Cover saturation", Range(0,2)) = 1.12
        _Posterize("Subtle printed colour bands", Range(0,1)) = 0.22
        _Rim("Soft edge highlight", Range(0,0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "BookCelForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _ShadowTint, _MidTint, _LightTint;
                half _Saturation, _Posterize, _Rim;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(position);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 cover = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half luminance = dot(cover, half3(0.2126,0.7152,0.0722));
                cover = max(0, lerp(luminance.xxx, cover, _Saturation));
                cover = lerp(cover, floor(cover * 12 + 0.5) / 12, _Posterize);
                Light light = GetMainLight(input.shadowCoord);
                half diffuse = saturate(dot(normal, light.direction) * 0.5 + 0.5);
                half lighting = diffuse * lerp(0.35h, 1.0h, light.shadowAttenuation);
                half middle = smoothstep(0.32h, 0.36h, lighting);
                half bright = smoothstep(0.68h, 0.72h, lighting);
                half3 bands = lerp(_ShadowTint.rgb, _MidTint.rgb, middle);
                bands = lerp(bands, _LightTint.rgb, bright);
                half3 ambient = max(SampleSH(normal), half3(0.16,0.16,0.16));
                half3 illumination = clamp(ambient * 0.45 + light.color * light.distanceAttenuation * 0.8,
                    half3(0.55,0.55,0.55), half3(1.15,1.15,1.15));
                half facing = saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half rim = smoothstep(0.62h, 0.92h, 1 - facing) * _Rim * bright;
                half3 color = cover * bands * illumination + rim * _LightTint.rgb;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
