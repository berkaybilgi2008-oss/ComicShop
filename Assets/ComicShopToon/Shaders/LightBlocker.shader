Shader "ComicShop/LightBlocker"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        // Closed slabs, not a color/depth-writing invisible-camera material.
        // ShadowsOnly renderer submits only this caster to realtime shadow maps.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; };
            V Vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(i.normalOS);
                float3 l=_LightDirection;
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                l=normalize(_LightPosition-p);
#endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(p,n,l));
#if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
#else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
#endif
                return o;
            }
            half4 Frag(V i):SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv1:TEXCOORD1; float2 uv2:TEXCOORD2; };
            struct V { float4 positionCS:SV_POSITION; };
            V Vert(A i)
            {
                V o;
                o.positionCS=MetaVertexPosition(i.positionOS,i.uv1,i.uv2,unity_LightmapST,unity_DynamicLightmapST);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                MetaInput meta=(MetaInput)0;
                meta.Albedo=0; meta.Emission=0;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
