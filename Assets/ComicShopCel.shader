Shader "ComicShop/Raf Cel"
{
 Properties
 {
  _BaseColor ("Wood / trim color", Color) = (0.4,0.22,0.1,1)
  _ShadowTone ("Shadow brightness", Range(0,1)) = 0.48
  _MidTone ("Midtone brightness", Range(0,1)) = 0.78
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Name "CelForward"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor; float _ShadowTone; float _MidTone;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
   struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
   V vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(a.normalOS); o.uv=a.uv; return o; }
   half4 frag(V i):SV_Target
   {
    Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    float n=dot(normalize(i.normalWS),l.direction);
    float shade=n>0.62?1:(n>0.08?_MidTone:_ShadowTone);
    shade*=l.shadowAttenuation>0.5?1:0.65;
    float3 color=_BaseColor.rgb*shade;
    return half4(color,1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 vert(float4 p:POSITION):SV_POSITION { return TransformObjectToHClip(p.xyz); }
   half4 frag():SV_Target { return 0; }
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 vert(float4 p:POSITION):SV_POSITION { return TransformObjectToHClip(p.xyz); }
   half4 frag():SV_Target { return 0; }
   ENDHLSL
  }
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="ForwardBase" }
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"
   fixed4 _BaseColor; float _ShadowTone; float _MidTone;
   struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; SHADOW_COORDS(2) };
   v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; TRANSFER_SHADOW(o); return o; }
   fixed4 frag(v2f i):SV_Target { float n=dot(normalize(i.normal),normalize(UnityWorldSpaceLightDir(i.world))); float s=n>0.62?1:(n>0.08?_MidTone:_ShadowTone); s*=SHADOW_ATTENUATION(i)>0.5?1:0.65; return fixed4(_BaseColor.rgb*s,1); }
   ENDCG
  }
  UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 }
 Fallback "Diffuse"
}
