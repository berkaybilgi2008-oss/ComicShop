Shader "ComicShop/ToonEmission"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        CBUFFER_END
        float _ToonEmissionGain;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float fog : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings EmissionVertex(Attributes i)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
            o.normalWS=TransformObjectToWorldNormal(i.normalOS);
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        half4 EmissionFragment(Varyings i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            return half4(MixFog(_BaseColor.rgb*_ToonEmissionGain,i.fog),1);
        }
        half4 DepthFragment(Varyings i):SV_Target { return 0; }
        half4 NormalsFragment(Varyings i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float3 n=NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
            float2 oct=PackNormalOctQuadEncode(n);
            return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
            #else
            return half4(n,0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex EmissionVertex
            #pragma fragment EmissionFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex EmissionVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex EmissionVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
