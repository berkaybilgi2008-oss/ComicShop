Shader "Custom/BookToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _CreaseColor ("Crease Color", Color) = (0,0,0,1)
        _CreaseStrength ("Crease Strength", Range(1,100)) = 28
        _CreaseThreshold ("Crease Threshold", Range(0,1)) = 0.035
        _SilhouetteColor ("Silhouette Color", Color) = (0,0,0,1)
        _SilhouetteStrength ("Silhouette Strength", Range(0,4)) = 1.15
        _SilhouettePower ("Silhouette Power", Range(1,12)) = 4
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
                float4 _CreaseColor;
                float _CreaseStrength;
                float _CreaseThreshold;
                float4 _SilhouetteColor;
                float _SilhouetteStrength;
                float _SilhouettePower;
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
                half3 n = normalize(IN.normalWS);

                // Three clear comic-book lighting bands while preserving the source texture.
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(n, normalize(mainLight.direction)));
                half3 ambient = SampleSH(n);
                half band = ndotl >= 0.62h ? 1.0h : (ndotl >= 0.28h ? 0.62h : 0.30h);
                half3 lit = ambient * 0.45h + mainLight.color * band * mainLight.shadowAttenuation;
                half3 diffuse = albedo.rgb * max(lit, 0.10h);

                // Screen-space normal change gives a restrained comic crease line.
                // Unlike generated edge geometry, it can never create detached triangles.
                half normalChange = max(length(ddx(n)), length(ddy(n)));
                half crease = smoothstep(0.05h, 0.85h,
                    saturate((normalChange - _CreaseThreshold) * _CreaseStrength));

                // Camera-facing silhouette accent without an inverted-hull pass.
                // This avoids the large black polygon artifacts caused by expanding
                // irregular FBX normals in object space.
                half3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half facing = saturate(dot(n, viewDir));
                half silhouette = pow(saturate(1.0h - facing), _SilhouettePower) * _SilhouetteStrength;
                silhouette = smoothstep(0.12h, 0.85h, silhouette);

                half edge = max(crease, silhouette);
                half3 edgeColor = lerp(_CreaseColor.rgb, _SilhouetteColor.rgb, silhouette);
                diffuse = lerp(diffuse, edgeColor, edge);

                return half4(diffuse, albedo.a);
            }
            ENDHLSL
        }
    }
}
