Shader "Custom/BookToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _EdgeThreshold ("Edge Threshold", Range(0.01,1)) = 0.08
        _EdgeSoftness ("Edge Softness", Range(0.001,0.5)) = 0.04
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float _EdgeThreshold;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = normalize(normal.normalWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Detect sharp changes between adjacent mesh surface normals.
                // Lower threshold + wider transition makes the seam line thicker
                // while keeping the effect tied to actual geometric surface joins.
                half normalChange = length(fwidth(normalize(IN.normalWS)));
                half edge = smoothstep(_EdgeThreshold,
                                       _EdgeThreshold + _EdgeSoftness,
                                       normalChange);

                // Normal book lighting only; no toon shadow bands are applied.
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(IN.normalWS, normalize(mainLight.direction)));
                half3 ambient = SampleSH(IN.normalWS);
                half3 diffuse = albedo.rgb * (ambient + mainLight.color * ndotl * mainLight.shadowAttenuation);
                diffuse = max(diffuse, albedo.rgb * 0.08h);

                return half4(lerp(diffuse, _EdgeColor.rgb, edge), albedo.a);
            }
            ENDHLSL
        }
    }
}
