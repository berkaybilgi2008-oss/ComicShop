Shader "ComicShop/Book Cel"
{
    Properties
    {
        [MainTexture] _BaseMap("Original cover", 2D) = "white" {}
        [MainColor] _BaseColor("Original tint", Color) = (1,1,1,1)
        [HideInInspector] _InkBoundsMin("Bounds minimum", Vector) = (-0.5,-0.5,-0.5,0)
        [HideInInspector] _InkBoundsMax("Bounds maximum", Vector) = (0.5,0.5,0.5,0)
        [HideInInspector] _InkWidths("Edge widths", Vector) = (0.015,0.015,0.015,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CoverAndTwelveEdges"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _InkBoundsMin, _InkBoundsMax, _InkWidths;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 cover = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                // Original texture: no quantization, saturation, rim or shadow thresholds.
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 lighting = max(SampleSH(normal), half3(0.28,0.28,0.28))
                    + light.color * saturate(dot(normal, light.direction)) * light.distanceAttenuation;
                cover *= min(lighting, half3(1.15,1.15,1.15));
                float3 distance = max(0, min(input.positionOS - _InkBoundsMin.xyz, _InkBoundsMax.xyz - input.positionOS));
                float3 relative = distance / max(_InkWidths.xyz, float3(1e-6,1e-6,1e-6));
                // Second closest box plane: covers the 12 edges, not face interiors.
                float second = min(max(relative.x, relative.y), min(max(relative.x, relative.z), max(relative.y, relative.z)));
                float aa = max(fwidth(second), 0.001);
                half ink = 1 - smoothstep(1 - aa, 1 + aa, second);
                return half4(MixFog(lerp(cover, half3(0,0,0), ink), input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
