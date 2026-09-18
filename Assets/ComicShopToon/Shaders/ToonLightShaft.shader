Shader "ComicShop/Light Shaft"
{
 Properties { _BaseColor("Color",Color)=(1,0.65,0.3,1) _Density("Density",Float)=0.035 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend One One ZWrite Off ZTest Always Cull Front
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   float _Density;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
   V Vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); return o; }
   half4 Frag(V i):SV_Target
   {
    float3 camera=GetCameraPositionWS();
    float3 ray=normalize(i.positionWS-camera);
    float3 origin=TransformWorldToObject(camera);
    // Unnormalized object-space direction retains world-meter ray parameterization.
    float3 direction=mul((float3x3)GetWorldToObjectMatrix(),ray);
    float3 safe=(step(0,direction)*2-1)*max(abs(direction),0.000001);
    float3 a=(-0.5-origin)/safe, b=(0.5-origin)/safe;
    float3 lo=min(a,b), hi=max(a,b);
    float enter=max(0,max(lo.x,max(lo.y,lo.z)));
    float leave=min(hi.x,min(hi.y,hi.z));
    float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
    float raw=SampleSceneDepth(uv);
    float eye=LinearEyeDepth(raw,_ZBufferParams);
    float forwardDepth=max(0.001,-mul((float3x3)GetWorldToViewMatrix(),ray).z);
    leave=min(leave,eye/forwardDepth);
    if(leave<=enter) return 0;
    float ds=(leave-enter)/16;
    float sum=0;
    [unroll] for(int j=0;j<16;j++)
    {
     float3 p=origin+direction*(enter+(j+0.5)*ds);
     float z=p.z+0.5;
     float radius=lerp(0.025,0.5,z);
     float edge=1-smoothstep(0.45,1,length(p.xy)/max(radius,0.001));
     float ends=smoothstep(0,0.06,z)*(1-smoothstep(0.65,1,z));
     sum+=edge*ends*ds;
    }
    float alpha=1-exp(-sum*max(0,_Density));
    return half4(_BaseColor.rgb*alpha,0);
   }
   ENDHLSL
  }
 }
}
