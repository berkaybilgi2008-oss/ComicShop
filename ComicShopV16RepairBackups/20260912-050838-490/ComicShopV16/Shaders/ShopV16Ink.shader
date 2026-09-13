Shader "ComicShop/V16 Ink" {
Properties {_Thickness("Thickness",Float)=0.0026}
SubShader {
Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-1"}
Pass {
Tags {"LightMode"="SRPDefaultUnlit"}
Cull Front ZWrite On
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float _Thickness;
struct A {float4 p:POSITION;float3 n:NORMAL;};
float4 vert(A a):SV_POSITION {float3 v=TransformWorldToView(TransformObjectToWorld(a.p.xyz));float3 n=normalize(mul((float3x3)UNITY_MATRIX_V,TransformObjectToWorldNormal(a.n)));v+=n*_Thickness*clamp(-v.z,0.35,45);return mul(UNITY_MATRIX_P,float4(v,1));}
half4 frag():SV_Target {
// Source ShaderMaterial emits raw sRGB numbers, unlike MeshToonMaterial.
float3 c=float3(36,26,18)/255;
#ifndef UNITY_COLORSPACE_GAMMA
c=SRGBToLinear(c);
#endif
return half4(c,1);}
ENDHLSL
}
}
}
