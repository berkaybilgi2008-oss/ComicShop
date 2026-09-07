Shader "Custom/BookToon"
{
 Properties { _BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _ShadowColor("Comic Shadow",Color)=(0.32,0.34,0.40,1) _LightThreshold("Light Threshold",Range(0,1))=0.58 _ShadowSoftness("Shadow Softness",Range(0.001,0.25))=0.025 _InkColor("Ink Color",Color)=(0.01,0.008,0.006,1) _InkStrength("Ink Strength",Range(0,2))=1.0 _InkThreshold("Ink Threshold",Range(0,1))=0.32 _CreaseStrength("Crease Strength",Range(0,4))=1.5 _OutlineColor("Outline Color",Color)=(0,0,0,1) _OutlineWidth("Outline Width",Range(0,0.08))=0.018 }
 SubShader {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
  Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } HLSLPROGRAM
   #pragma vertex vert #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _BaseColor; float4 _ShadowColor; float _LightThreshold; float _ShadowSoftness; float4 _InkColor; float _InkStrength; float _InkThreshold; float _CreaseStrength; float4 _OutlineColor; float _OutlineWidth; CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;}; struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD1;float3 normalWS:TEXCOORD2;float2 uv:TEXCOORD0;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.normalOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=normalize(n.normalWS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 frag(V i):SV_Target{half4 a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;half3 n=normalize(i.normalWS);Light l=GetMainLight();half d=saturate(dot(n,normalize(l.direction)));half b=smoothstep(_LightThreshold-_ShadowSoftness,_LightThreshold+_ShadowSoftness,d);half3 c=a.rgb*lerp(_ShadowColor.rgb,half3(1,1,1),b)*l.color*l.distanceAttenuation*l.shadowAttenuation;
   #if defined(_ADDITIONAL_LIGHTS) uint count=GetAdditionalLightsCount();for(uint x=0u;x<count;x++){Light q=GetAdditionalLight(x,i.positionWS);half dd=saturate(dot(n,normalize(q.direction)));half bb=smoothstep(_LightThreshold-_ShadowSoftness,_LightThreshold+_ShadowSoftness,dd);c+=a.rgb*lerp(_ShadowColor.rgb,half3(1,1,1),bb)*q.color*q.distanceAttenuation*q.shadowAttenuation;}#endif
   half3 vd=GetWorldSpaceNormalizeViewDir(i.positionWS);half facing=saturate(abs(dot(n,vd)));half silhouette=1-smoothstep(_InkThreshold,_InkThreshold+0.12,facing);half crease=smoothstep(0.08,0.42,saturate(max(length(ddx(n)),length(ddy(n)))*_CreaseStrength));half ink=saturate(max(silhouette,crease)*_InkStrength);return half4(lerp(c,_InkColor.rgb,ink),a.a);}
  ENDHLSL }
  Pass { Name "ComicOutline" Tags { "LightMode"="SRPDefaultUnlit" } Cull Front ZWrite Off ZTest LEqual HLSLPROGRAM
   #pragma vertex outlineVert #pragma fragment outlineFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) float4 _OutlineColor; float _OutlineWidth; CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;}; struct V{float4 positionCS:SV_POSITION;};
   V outlineVert(A i){V o;float3 p=i.positionOS.xyz+i.normalOS*_OutlineWidth;VertexPositionInputs v=GetVertexPositionInputs(p);o.positionCS=v.positionCS;return o;}
   half4 outlineFrag(V i):SV_Target{return _OutlineColor;}
  ENDHLSL }
 }
}
