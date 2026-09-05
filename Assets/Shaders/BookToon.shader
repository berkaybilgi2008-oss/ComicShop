Shader "Custom/BookToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _EdgeStrength ("Edge Strength", Range(1,200)) = 80
        _EdgeThreshold ("Edge Threshold", Range(0,1)) = 0.015
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
                float _EdgeStrength;
                float _EdgeThreshold;
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

                // Detect the actual change of surface direction between adjacent
                // screen pixels. This is deliberately amplified so hard mesh
                // creases become unmistakable instead of barely visible.
                half3 n = normalize(IN.normalWS);
                half normalChangeX = length(ddx(n));
                half normalChangeY = length(ddy(n));
                half normalChange = max(normalChangeX, normalChangeY);
                half edge = saturate((normalChange - _EdgeThreshold) * _EdgeStrength);
                edge = smoothstep(0.05h, 0.85h, edge);

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(n, normalize(mainLight.direction)));
                half3 ambient = SampleSH(n);

                // Strong toon-style light bands while keeping the original texture.
                half toonLight = smoothstep(0.25h, 0.30h, ndotl);
                half3 lit = ambient + mainLight.color * lerp(0.45h, 1.0h, toonLight) * mainLight.shadowAttenuation;
                half3 diffuse = albedo.rgb * lit;
                diffuse = max(diffuse, albedo.rgb * 0.08h);

                // Black only at detected geometric surface transitions.
                return half4(lerp(diffuse, _EdgeColor.rgb, edge), albedo.a);
            }
            ENDHLSL
        }
    }
}
