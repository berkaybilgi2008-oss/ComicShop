Shader "ToastRanger/Outline URP" {
 Properties {_OutlineColor("Ink",Color)=(0.08,0.06,0.05,1) _Width("Width in metres",Range(0,0.01))=0.003}
 SubShader {
 Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1"}
 Pass {
 Tags {"LightMode"="SRPDefaultUnlit"} Cull Front ZWrite Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _OutlineColor; float _Width;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 float4 vert(A i):SV_POSITION {float3 p=TransformObjectToWorld(i.positionOS.xyz);p+=TransformObjectToWorldNormal(i.normalOS)*_Width;return TransformWorldToHClip(p);}
 half4 frag():SV_Target{return _OutlineColor;}
 ENDHLSL
 }
 }
}
