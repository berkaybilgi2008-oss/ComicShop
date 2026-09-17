Shader "ComicShop/ToonOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "InvertedHull"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Shared global style, no per-material look properties.
            float4 _OutlineColor;
            float _OutlineWidth, _OutlineReferenceDistance;
            struct A
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float4 smoothNormal:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 positionCS:SV_POSITION;
                float fog:TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            V Vert(A i)
            {
                V o=(V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 n=normalize(i.normalOS);
                if(i.smoothNormal.w>0.5 && dot(i.tangentOS.xyz,i.tangentOS.xyz)>0.1)
                {
                    float3 t=normalize(i.tangentOS.xyz);
                    float3 b=cross(n,t)*i.tangentOS.w;
                    float3 s=i.smoothNormal.xyz;
                    n=normalize(t*s.x+b*s.y+n*s.z);
                }
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                float3 nw=TransformObjectToWorldNormal(n);
                float depth=max(0.01,-TransformWorldToView(p).z);
                float reference=max(0.01,_OutlineReferenceDistance);
                float projectionY=abs(UNITY_MATRIX_P._m11);
                float compensation=depth/reference*1.7320508/max(0.001,projectionY);
                if(unity_OrthoParams.w>0.5)
                    compensation=1.7320508/(reference*max(0.001,projectionY));
                o.positionCS=TransformWorldToHClip(p+nw*_OutlineWidth*compensation);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(MixFog(_OutlineColor.rgb,i.fog),_OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
