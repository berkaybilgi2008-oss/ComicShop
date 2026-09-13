Shader "ComicShop/V16 Print Overlay" {
Properties {_Strength("Strength",Range(0,1))=1}
SubShader {
Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"}
Pass {
ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float _Strength;
struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
half4 frag(V i):SV_Target {
float2 px=i.uv*_ScreenParams.xy;
float dots=(1-smoothstep(0.7,0.95,length(fmod(px,3)-1.5)))*0.55*0.15;
float vignette=saturate((length((i.uv-float2(0.5,0.55))/float2(0.72,0.57))-0.42)/0.58)*0.42;
float grain=step(fmod(dot(px,float2(0.993,0.122)),7),3)*0.05;
float a=1-(1-dots)*(1-vignette)*(1-grain);
float3 c=float3(30,18,8)/255;
#ifndef UNITY_COLORSPACE_GAMMA
c=lerp(c / 12.92, pow(max((c + 0.055) / 1.055, 0.0), 2.4), step(0.04045, c));
#endif
return half4(c,a*_Strength);
}
ENDHLSL
}
}
}
