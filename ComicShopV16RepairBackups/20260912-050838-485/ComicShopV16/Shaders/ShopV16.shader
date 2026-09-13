Shader "ComicShop/V16 Source Toon" {
Properties {
_BaseMap("Texture",2D)="white"{}
_BaseColor("Source linear color",Color)=(1,1,1,1)
_Emission("Emission",Vector)=(0,0,0,0)
_Unlit("Unlit",Float)=0
_Smooth("Smooth ramp",Float)=0
_FlipY("Flip UV",Float)=0
_Cull("Cull",Float)=2
_SrcBlend("Source blend",Float)=5
_DstBlend("Destination blend",Float)=10
_ZWrite("Depth write",Float)=1
_Cutoff("Alpha cutoff",Float)=0
_Receive("Receive sun shadows",Float)=1
_OffsetFactor("Depth offset factor",Float)=0
_OffsetUnits("Depth offset units",Float)=0
}
SubShader {
Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass {
Name "SourceToon"
Tags {"LightMode"="UniversalForwardOnly"}
Cull [_Cull] Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
Offset [_OffsetFactor], [_OffsetUnits]
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseColor, _Emission;
float _Unlit,_Smooth,_FlipY,_Cutoff,_Receive;
CBUFFER_END
int _ShopLightCount;
float4 _ShopLightPositions[32],_ShopLightColors[32],_ShopLightParams[32],_ShopAmbient;
struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;};
V vert(A a){V o;o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);if(_FlipY>0.5)o.uv.y=1-o.uv.y;return o;}
float3 decodeSRGB(float3 c){return lerp(c/12.92,pow(max((c+0.055)/1.055,0),2.4),step(0.04045,c));}
float ramp(float d){float t=saturate(d*0.5+0.5);if(_Smooth>0.5){float x=saturate((t*24-0.5)/23)*23;float a=floor(x),b=min(a+1,23);return lerp(round((0.42+0.58*a/23)*255)/255,round((0.42+0.58*b/23)*255)/255,frac(x));}return t<1.0/3?97.0/255:(t<2.0/3?184.0/255:1);}
half4 frag(V i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
#ifdef UNITY_COLORSPACE_GAMMA
tex.rgb=decodeSRGB(tex.rgb);
#endif
float alpha=tex.a*_BaseColor.a;clip(alpha-_Cutoff);
float3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
float3 light=_ShopAmbient.rgb;
for(int k=0;k<_ShopLightCount;k++){
float3 delta=_ShopLightPositions[k].xyz-i.positionWS*_ShopLightPositions[k].w;
float dist=length(delta);float3 dir=delta/max(dist,0.00001);float attenuation=1;
if(_ShopLightPositions[k].w>0.5&&_ShopLightParams[k].x>0)attenuation=pow(saturate(1-dist/_ShopLightParams[k].x),_ShopLightParams[k].y);
float shadow=1;
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
if(_ShopLightParams[k].z>0.5&&_Receive>0.5){
float4 coord=TransformWorldToShadowCoord(i.positionWS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
coord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
#endif
Light main=GetMainLight(coord);if(dot(main.direction,dir)>0.999)shadow=main.shadowAttenuation;
}
#endif
light+=_ShopLightColors[k].rgb*attenuation*ramp(dot(n,dir))*shadow;
}
float3 c=tex.rgb*_BaseColor.rgb*lerp(light,float3(1,1,1),_Unlit)+_Emission.rgb;
#ifdef UNITY_COLORSPACE_GAMMA
c=LinearToSRGB(c);
#endif
return half4(c,alpha);
}
ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
}
}
