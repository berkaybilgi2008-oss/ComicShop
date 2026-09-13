#ifndef COMICSHOP_TOON_FORWARD
#define COMICSHOP_TOON_FORWARD
#include "ToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
struct Attributes
{
    float4 positionOS:POSITION;
    float3 normalOS:NORMAL;
    float2 uv:TEXCOORD0;
    float2 lightmapUV:TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS:SV_POSITION;
    float3 positionWS:TEXCOORD0;
    float3 normalWS:TEXCOORD1;
    float2 uv:TEXCOORD2;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
    float4 probeOcclusion:TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings ToonVertex(Attributes i)
{
    Varyings o=(Varyings)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i,o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
    o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(i.normalOS);
    o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
    OUTPUT_LIGHTMAP_UV(i.lightmapUV,unity_LightmapST,o.lightmapUV);
    OUTPUT_SH4(o.positionWS,o.normalWS,GetWorldSpaceNormalizeViewDir(o.positionWS),o.vertexSH,o.probeOcclusion);
    return o;
}
float Band(float x)
{
    int steps=clamp((int)round(_ShadowSteps),2,3);
    float result=0;
    [unroll] for(int k=1;k<3;k++)
    {
        if(k<steps)
        {
            float t=(float)k/steps;
            float w=max(_RampSmoothness,0.00001);
            result+=smoothstep(t-w*0.5,t+w*0.5,saturate(x));
        }
    }
    return result/(steps-1);
}
float3 PosterizeGI(float3 gi)
{
    float n=max(2,round(_BakedSteps))-1;
    // Quantize all channels; preserving continuously varying chroma would reintroduce gradients.
    return floor(saturate(gi)*n+0.5)/n;
}
void AccumulateToonLight(Light l,float3 n,float3 v,bool punctual,
    inout float3 direct,inout float3 spec,inout float litMask)
{
    float angular=Band(saturate(dot(n,l.direction))*l.shadowAttenuation);
    float energy=max(l.color.r,max(l.color.g,l.color.b));
    float attenuation=punctual ? saturate(l.distanceAttenuation*_LightFalloffScale) : saturate(l.distanceAttenuation);
    // Include light energy BEFORE quantization. Never multiply a smooth radial falloff after the ramp.
    float intensitySteps=max(2,round(_ShadowSteps))-1;
    float irradiance=floor(saturate(energy*attenuation)*intensitySteps+0.5)/intensitySteps;
    float lightBand=angular*irradiance;
    float3 hue=l.color/max(energy,0.0001);
    direct+=hue*lightBand;
    litMask=max(litMask,lightBand);
#if defined(_TOON_SPECULAR)
    float3 h=SafeNormalize(l.direction+v);
    spec+=_SpecColor.rgb*hue*step(1-saturate(_SpecSize),saturate(dot(n,h)))*step(0.001,lightBand);
#endif
}
float Halftone(float2 pixel)
{
    float spacing=max(4,_HalftoneScale);
    float2 rotated=float2(pixel.x-pixel.y,pixel.x+pixel.y)*0.70710678118;
    float2 cell=frac(rotated/spacing)-0.5;
    float d=length(cell);
    float aa=max(fwidth(d),0.0001);
    return 1-smoothstep(0.27-aa*0.5,0.27+aa*0.5,d);
}
half4 ToonFragment(Varyings i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS;
    inputData.normalWS=NormalizeNormalPerPixel(i.normalWS);
    inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    float4 shadowCoord;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
#else
    // Fragment world position selects the cascade correctly across large wall triangles.
    shadowCoord=TransformWorldToShadowCoord(i.positionWS);
#endif
    half4 shadowMask=half4(1,1,1,1);
    half3 gi;
#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    gi=SAMPLE_GI(i.vertexSH,GetAbsolutePositionWS(i.positionWS),inputData.normalWS,
        inputData.viewDirectionWS,i.positionCS.xy,i.probeOcclusion,shadowMask);
#else
    gi=SAMPLE_GI(i.lightmapUV,i.vertexSH,inputData.normalWS);
    shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);
#endif
    float3 direct=0, spec=0;
    float litMask=0;
    Light mainLight=GetMainLight(shadowCoord,i.positionWS,shadowMask);
    AccumulateToonLight(mainLight,inputData.normalWS,inputData.viewDirectionWS,false,direct,spec,litMask);
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
    {
        Light l=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        AccumulateToonLight(l,inputData.normalWS,inputData.viewDirectionWS,false,direct,spec,litMask);
    }
#endif
    uint pixelLightCount=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light l=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        AccumulateToonLight(l,inputData.normalWS,inputData.viewDirectionWS,true,direct,spec,litMask);
    LIGHT_LOOP_END
#endif
    float3 albedo=ToonAlbedo(i.uv);
    // Required palette interpolation, followed by an explicit chromatic shadow correction.
    float3 baseColor=lerp(_ShadowTint.rgb*albedo,albedo,litMask);
    float3 shadowBase=_ShadowTint.rgb*albedo;
    float shadowLuma=dot(shadowBase,float3(0.2126,0.7152,0.0722));
    float tintLuma=max(dot(_ShadowTint.rgb,float3(0.2126,0.7152,0.0722)),0.0001);
    float3 shifted=lerp(shadowBase,_ShadowTint.rgb*(shadowLuma/tintLuma),_ShadowHueShift);
    int palette=(int)round(_Palette);
    if(palette==1 || palette==2)
    {
        float3 anchor=palette==1 ? _WoodShadow.rgb : _WallpaperShadow.rgb;
        shifted=lerp(shifted,anchor,_PaletteShadowMix);
    }
    baseColor=lerp(shifted,baseColor,litMask);
    // Tint energy is bounded; eight overlapping pendants cannot wash the surface to white.
    float3 color=baseColor*lerp(_ShadowFloor.xxx,max(saturate(direct*_DirectGain),float3(0.35,0.35,0.35)),litMask);
    color+=albedo*PosterizeGI(gi)*_BakedInfluence;
#if defined(_TOON_HALFTONE) && !defined(_COMIC_HALFTONE_OFF)
    if(_ComicHalftoneDisabled<0.5)
        color=lerp(color,_HalftoneColor.rgb,Halftone(i.positionCS.xy)*_HalftoneStrength*(1-step(0.001,litMask)));
#endif
    color+=spec;
#if defined(_TOON_RIM)
    float rim=pow(1-saturate(dot(inputData.normalWS,inputData.viewDirectionWS)),max(0.01,_RimPower));
    color+=_RimColor.rgb*step(_RimThreshold,rim)*lerp(1,step(0.001,litMask),_RimLitOnly);
#endif
    color+=_EmissionColor.rgb;
    float fog=ComputeFogFactor(TransformWorldToHClip(i.positionWS).z);
    color=MixFog(color,fog);
    return half4(color,1);
}
#endif
