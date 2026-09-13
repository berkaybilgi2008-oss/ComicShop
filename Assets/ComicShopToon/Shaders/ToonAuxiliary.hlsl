#ifndef COMICSHOP_TOON_AUXILIARY
#define COMICSHOP_TOON_AUXILIARY
#include "ToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
float3 _LightDirection;
float3 _LightPosition;
struct AuxAttributes
{
    float4 positionOS:POSITION;
    float3 normalOS:NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct AuxVaryings
{
    float4 positionCS:SV_POSITION;
    float3 normalWS:TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
AuxVaryings AuxVertex(AuxAttributes i)
{
    AuxVaryings o=(AuxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i,o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
    o.normalWS=TransformObjectToWorldNormal(i.normalOS);
    return o;
}
AuxVaryings ShadowVertex(AuxAttributes i)
{
    AuxVaryings o=AuxVertex(i);
    UNITY_SETUP_INSTANCE_ID(i);
    float3 p=TransformObjectToWorld(i.positionOS.xyz);
    float3 l=_LightDirection;
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    l=normalize(_LightPosition-p);
#endif
    o.positionCS=TransformWorldToHClip(ApplyShadowBias(p,o.normalWS,l));
#if UNITY_REVERSED_Z
    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
#else
    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
#endif
    return o;
}
half4 DepthFragment(AuxVaryings i):SV_Target { return 0; }
half4 MaskFragment(AuxVaryings i):SV_Target { return 1; }
half4 NormalsFragment(AuxVaryings i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n=normalize(i.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct=PackNormalOctQuadEncode(n);
    return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
#else
    return half4(n,0);
#endif
}
#endif
