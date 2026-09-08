Shader "ComicShop/Book Cel"
{
    Properties
    {
        [MainTexture] _BaseMap("Original cover", 2D) = "white" {}
        [MainColor] _BaseColor("Original tint", Color) = (1,1,1,1)
        _ContourScale("Contour thickness", Range(1,2)) = 1.3
        _CreaseOpacity("Small corner creases", Range(0,1)) = 0.8
        _IllustrationInk("Hand drawn book details", Range(0,1)) = 0.85
        _ToonStrength("Surface toon strength", Range(0,1)) = 0.65
        _EdgeHighlight("Printed edge highlight", Range(0,0.2)) = 0.02
        _PaperDetail("Subtle paper detail", Range(0,0.1)) = 0
        [HideInInspector] _InkCoverAxis("Cover axis", Vector) = (0,0,1,0)
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
                float4 _InkCoverAxis;
                half _ContourScale, _CreaseOpacity;
                half _ToonStrength, _EdgeHighlight, _PaperDetail;
                half _IllustrationInk;
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
            float Crease(float2 p, float2 a, float2 b)
            {
                float2 segment = b - a;
                float t = saturate(dot(p - a, segment) / dot(segment, segment));
                float d = length(p - (a + t * segment));
                float width = lerp(0.0018, 0.00025, t);
                float aa = max(length(fwidth(p)), 0.0001);
                float stroke = 1 - smoothstep(width - aa, width + aa, d);
                // Fade fine pen marks out at a distance rather than shimmer.
                return stroke * saturate(width / aa) * (1 - smoothstep(0.85, 1, t));
            }
            float BookPen(float2 p)
            {
                // Broken binding seam and asymmetric corner folds: keep the artwork clear.
                float pen = Crease(p, float2(0.038,0.12), float2(0.041,0.43));
                pen = max(pen, Crease(p, float2(0.041,0.48), float2(0.037,0.86)));
                pen = max(pen, Crease(p, float2(0.026,0.925), float2(0.086,0.975)));
                pen = max(pen, Crease(p, float2(0.086,0.975), float2(0.105,0.963)));
                pen = max(pen, Crease(p, float2(0.96,0.11), float2(0.895,0.032)));
                pen = max(pen, Crease(p, float2(0.895,0.032), float2(0.872,0.038)));
                // Sparse tapered pen hatching beside folds, not an all-over noise layer.
                pen = max(pen, Crease(p, float2(0.048,0.9), float2(0.073,0.876)));
                pen = max(pen, Crease(p, float2(0.055,0.889), float2(0.077,0.871)));
                pen = max(pen, Crease(p, float2(0.932,0.069), float2(0.951,0.047)));
                pen = max(pen, Crease(p, float2(0.923,0.06), float2(0.938,0.044)));
                pen = max(pen, Crease(p, float2(0.96,0.71), float2(0.963,0.8)));
                return pen;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 cover = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                // Bright neutral fill keeps the cover readable on every face.
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate(dot(normal, light.direction));
                half lightEnergy = saturate(dot(light.color, half3(0.2126,0.7152,0.0722)) * light.distanceAttenuation);
                half luminance = dot(cover, half3(0.2126,0.7152,0.0722));
                cover = max(0, lerp(luminance.xxx, cover, 1.06h));
                // Three bright toon bands; preserve the printed cover texture.
                float bandAA = max(fwidth(diffuse), 0.008);
                half middle = smoothstep(0.33 - bandAA, 0.33 + bandAA, diffuse);
                half bright = smoothstep(0.70 - bandAA, 0.70 + bandAA, diffuse);
                half3 shade = lerp(half3(0.87,0.89,0.94), half3(0.97,0.97,0.98), middle);
                shade = lerp(shade, half3(1.07,1.045,1.01), bright);
                cover *= lerp(half3(1,1,1), shade, _ToonStrength * lightEnergy);
                float3 distance = max(0, min(input.positionOS - _InkBoundsMin.xyz, _InkBoundsMax.xyz - input.positionOS));
                float3 relative = distance / max(_InkWidths.xyz * _ContourScale, float3(1e-6,1e-6,1e-6));
                // Second closest box plane: covers the 12 edges, not face interiors.
                float second = min(max(relative.x, relative.y), min(max(relative.x, relative.z), max(relative.y, relative.z)));
                float aa = max(fwidth(second), 0.001);
                half ink = 1 - smoothstep(1 - aa, 1 + aa, second);
                float3 box = saturate((input.positionOS - _InkBoundsMin.xyz) / max(_InkBoundsMax.xyz - _InkBoundsMin.xyz, float3(1e-6,1e-6,1e-6)));
                float2 face = _InkCoverAxis.x > 0.5 ? box.yz : (_InkCoverAxis.y > 0.5 ? box.xz : box.xy);
                float faceDepth = dot(box, _InkCoverAxis.xyz);
                float onCover = step(0.49, abs(faceDepth - 0.5));
                // Narrow inset light catches the bound edge without washing out cover art.
                float edgeDistance = min(min(face.x, 1-face.x), min(face.y, 1-face.y));
                float edgeAA = max(fwidth(edgeDistance), 0.0001);
                float edgeLight = 1-smoothstep(0.002, 0.006 + edgeAA, abs(edgeDistance-0.024));
                edgeLight *= saturate(0.004 / edgeAA) * onCover;
                cover += (1-saturate(cover)) * half3(1,0.94,0.82) * edgeLight * _EdgeHighlight;
                // Object-space print grain stays attached to the book. Fade before aliasing.
                float2 grainUV = face * 180;
                float grainFade = 1-smoothstep(0.25, 0.75, max(length(ddx(grainUV)), length(ddy(grainUV))));
                float grain = sin(grainUV.x * 6.2831853) * sin(grainUV.y * 6.2831853);
                cover *= 1 + grain * grainFade * _PaperDetail * onCover;
                float crease = max(Crease(face, float2(0.055,0.945), float2(0.12,0.885)),
                                   Crease(face, float2(0.945,0.06), float2(0.905,0.105)));
                ink = max(ink, crease * onCover * _CreaseOpacity);
                ink = max(ink, BookPen(face) * onCover * _IllustrationInk);
                // Two inset ink seams follow the thickness on side faces.
                float seamDistance = min(abs(faceDepth-0.16), abs(faceDepth-0.84));
                float seamAA = max(fwidth(faceDepth), 0.0001);
                float sideSeam = (1-smoothstep(0.009-seamAA, 0.009+seamAA, seamDistance))
                    * saturate(0.009/seamAA) * (1-onCover);
                ink = max(ink, sideSeam * _IllustrationInk * 0.65);
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
