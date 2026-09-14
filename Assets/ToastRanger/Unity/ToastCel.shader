Shader "ToastRanger/Cel URP" {
 Properties {
 _BaseColor("Palette Color", Color)=(1,1,1,1)
 _ShadowTint("Shadow Tint",Color)=(0.49,0.43,0.36,1)
 _MidLevel("Middle Band",Range(0,1))=0.80
 _ShadowThreshold("Shadow Threshold",Range(-1,1))=0.05
 _LightThreshold("Light Threshold",Range(-1,1))=0.6
 }
 SubShader {
 Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor; half4 _ShadowTint; float _MidLevel; float _ShadowThreshold; float _LightThreshold;
 CBUFFER_END
 ENDHLSL
 Pass {
 Name "CelForward" Tags {"LightMode"="UniversalForwardOnly"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float fog:TEXCOORD2;};
 V vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 half4 frag(V i):SV_Target {
 Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
 float n=dot(normalize(i.normalWS),l.direction);
 half3 band=n<_ShadowThreshold?_ShadowTint.rgb:(n<_LightThreshold?half3(_MidLevel,_MidLevel,_MidLevel):half3(1,1,1));
 band=lerp(_ShadowTint.rgb,band,step(0.5,l.shadowAttenuation));
 half3 color=_BaseColor.rgb*band*lerp(half3(1,1,1),l.color,0.25);
 return half4(MixFog(color,i.fog),1);
 }
 ENDHLSL
 }
 Pass {
 Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask 0
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 struct A {float4 positionOS:POSITION;};
 float4 vert(A i):SV_POSITION{return TransformObjectToHClip(i.positionOS.xyz);}
 half4 frag():SV_Target{return 0;}
 ENDHLSL
 }

 Pass {
 Name "DepthNormals" Tags {"LightMode"="DepthNormals"} ZWrite On ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma vertex NormalVertex
 #pragma fragment NormalFragment
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 struct NormalAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
 struct NormalVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
 NormalVaryings NormalVertex(NormalAttributes i) {
 NormalVaryings o;
 o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
 o.normalWS=TransformObjectToWorldNormal(i.normalOS);
 return o;
 }
 half4 NormalFragment(NormalVaryings i):SV_Target {
 float3 normalWS=normalize(i.normalWS);
 #if defined(_GBUFFER_NORMALS_OCT)
 float2 oct=PackNormalOctQuadEncode(normalWS);
 return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
 #else
 return half4(normalWS,0);
 #endif
 }
 ENDHLSL
 }

 Pass {
 Name "DepthNormalsOnly" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma vertex NormalVertex
 #pragma fragment NormalFragment
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 struct NormalAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
 struct NormalVaryings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
 NormalVaryings NormalVertex(NormalAttributes i) {
 NormalVaryings o;
 o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
 o.normalWS=TransformObjectToWorldNormal(i.normalOS);
 return o;
 }
 half4 NormalFragment(NormalVaryings i):SV_Target {
 float3 normalWS=normalize(i.normalWS);
 #if defined(_GBUFFER_NORMALS_OCT)
 float2 oct=PackNormalOctQuadEncode(normalWS);
 return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
 #else
 return half4(normalWS,0);
 #endif
 }
 ENDHLSL
 }
 Pass {
 Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
 float3 _LightDirection; float3 _LightPosition;
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 float4 vert(A i):SV_POSITION {
 float3 p=TransformObjectToWorld(i.positionOS.xyz);float3 n=TransformObjectToWorldNormal(i.normalOS);
 #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
 float3 l=normalize(_LightPosition-p);
 #else
 float3 l=_LightDirection;
 #endif
 float4 h=TransformWorldToHClip(ApplyShadowBias(p,n,l));
 #if UNITY_REVERSED_Z
 h.z=min(h.z,UNITY_NEAR_CLIP_VALUE);
 #else
 h.z=max(h.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return h;
 }
 half4 frag():SV_Target{return 0;}
 ENDHLSL
 }
 }
}
