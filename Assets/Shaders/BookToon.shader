Shader "Custom/BookToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _EdgeColor ("Crease Color", Color) = (0,0,0,1)
        _EdgeStrength ("Crease Strength", Range(1,200)) = 100
        _EdgeThreshold ("Crease Threshold", Range(0,1)) = 0.01
        _OutlineColor ("Silhouette Color", Color) = (0,0,0,1)
        _OutlineWidth ("Silhouette Width", Range(0.001,0.08)) = 0.018
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
                float4 _OutlineColor;
                float _OutlineWidth;
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
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS = normalize(normal.normalWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 n = normalize(IN.normalWS);

                // Strong, discrete comic-book lighting bands.
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(n, normalize(mainLight.direction)));
                half3 ambient = SampleSH(n);
                half band = ndotl >= 0.62h ? 1.0h : (ndotl >= 0.28h ? 0.58h : 0.28h);
                half3 lit = ambient * 0.55h + mainLight.color * band * mainLight.shadowAttenuation;
                half3 diffuse = albedo.rgb * max(lit, albedo.rgb * 0.12h);

                // Keep a restrained screen-space crease accent as a backup for
                // shallow surface transitions that the explicit edge geometry misses.
                half normalChange = max(length(ddx(n)), length(ddy(n)));
                half crease = smoothstep(0.05h, 0.9h, saturate((normalChange - _EdgeThreshold) * _EdgeStrength));
                diffuse = lerp(diffuse, _EdgeColor.rgb, crease);

                return half4(diffuse, albedo.a);
            }
            ENDHLSL
        }

        // Inverted-hull silhouette pass: a thick black comic outline around the
        // outside contour of every book while leaving the original texture intact.
        Pass
        {
            Name "SilhouetteOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertOutline(Attributes IN)
            {
                Varyings OUT;
                float3 expanded = IN.positionOS + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(expanded);
                return OUT;
            }

            half4 fragOutline(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
