Shader "Hidden/ComicShop/ScreenSpaceOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "SobelInk"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            // Global style values; never material-authored.
            float4 _OutlineColor;
            float _OutlineThickness, _DepthThreshold, _NormalThreshold;
            float EyeDepth(float raw)
            {
                if(unity_OrthoParams.w>0.5)
                {
#if UNITY_REVERSED_Z
                    raw=1-raw;
#endif
                    return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                }
                return LinearEyeDepth(raw,_ZBufferParams);
            }
            bool IsBackground(float raw)
            {
#if UNITY_REVERSED_Z
                return raw<0.000001;
#else
                return raw>0.999999;
#endif
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float2 texel=rcp(_ScaledScreenParams.xy);
                float2 delta=texel*max(0,_OutlineThickness)*0.5;
                float centerRaw=SampleSceneDepth(uv);
                float centerDepth=EyeDepth(centerRaw);
                float3 centerNormal=SampleSceneNormals(uv);
                float dzx=0,dzy=0;
                float3 nx=0,ny=0;
                float selected=0;
                float nearestDepth=centerDepth;
                [unroll] for(int y=-1;y<=1;y++)
                {
                    [unroll] for(int x=-1;x<=1;x++)
                    {
                        float2 p=clamp(uv+float2(x,y)*delta,texel*0.5,1-texel*0.5);
                        float raw=SampleSceneDepth(p);
                        float z=EyeDepth(raw);
                        nearestDepth=min(nearestDepth,z);
                        float3 n=IsBackground(raw) ? centerNormal : SampleSceneNormals(p);
                        float kx=x*(y==0 ? 2:1);
                        float ky=y*(x==0 ? 2:1);
                        dzx+=z*kx; dzy+=z*ky;
                        nx+=n*kx; ny+=n*ky;
                        selected=max(selected,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,p).r);
                    }
                }
                // Relative depth difference: permitted meters grow with view distance.
                float depthEdge=length(float2(dzx,dzy))/(8*max(1,nearestDepth));
                float normalEdge=sqrt(dot(nx,nx)+dot(ny,ny))/8;
                // Suppress distant subpixel normal detail, preserving depth silhouettes.
                float distanceNormalThreshold=_NormalThreshold*lerp(1,2,saturate((nearestDepth-12)/13));
                float edge=max(step(_DepthThreshold,depthEdge),step(distanceNormalThreshold,normalEdge));
                edge *= step(0.001,_OutlineThickness);
                return half4(_OutlineColor.rgb,edge*step(0.5,selected)*_OutlineColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VisibleLayerMask"
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct MaskAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct MaskVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            MaskVaryings MaskVertex(MaskAttributes input)
            {
                MaskVaryings output = (MaskVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 MaskFragment(MaskVaryings input) : SV_Target { return 1; }
            ENDHLSL
        }
    }
    FallBack Off
}
