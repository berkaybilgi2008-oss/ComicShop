#ifndef COMICSHOP_TOON_FORWARD
#define COMICSHOP_TOON_FORWARD
#include "ToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
struct Attributes
{
    float4 positionOS:POSITION;
    float3 normalOS:NORMAL;
    float4 tangentOS:TANGENT;
    float2 uv:TEXCOORD0;
    float2 lightmapUV:TEXCOORD1;
    float2 dynamicLightmapUV:TEXCOORD2;
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
#if defined(_NORMALMAP)
    float4 tangentWS:TEXCOORD5;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
    float2 dynamicLightmapUV:TEXCOORD6;
#endif
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
#if defined(_NORMALMAP)
    o.tangentWS=float4(TransformObjectToWorldDir(i.tangentOS.xyz),i.tangentOS.w*GetOddNegativeScale());
#endif
    o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
    OUTPUT_LIGHTMAP_UV(i.lightmapUV,unity_LightmapST,o.lightmapUV);
#if defined(DYNAMICLIGHTMAP_ON)
    o.dynamicLightmapUV=i.dynamicLightmapUV*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;
#endif
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
            float threshold=(float)k/steps;
            // Smoothness is a narrow edge AA window, not a lighting gradient.
            float width=clamp(_RampSmoothness,0,0.05);
            result += width > 0.00001
                ? smoothstep(threshold-width*0.5,threshold+width*0.5,saturate(x))
                : step(threshold,saturate(x));
        }
    }
    return result/(steps-1);
}
float3 PosterizeGI(float3 gi)
{
    float n=clamp(round(_BakedSteps),2,8)-1;
    // Quantize RGB independently; continuous chroma must not reintroduce gradients.
    return floor(saturate(gi*exp2(_ToonBakedExposure))*n+0.5)/n;
}
void AccumulateToonLight(Light l,float3 n,float3 v,bool punctual,
    inout float3 direct,inout float3 spec,inout float litMask)
{
    float angular=Band(saturate(dot(n,l.direction))*l.shadowAttenuation);
    float energy=max(l.color.r,max(l.color.g,l.color.b));
    float attenuation=punctual ? l.distanceAttenuation*max(0,_LightFalloffScale) : l.distanceAttenuation;
    float count=clamp(round(_ShadowSteps),2,3)-1;
    float irradiance=floor(clamp(energy*attenuation,0,max(1,_ToonDirectMax))*count+0.5)/count;
    float lightBand=angular*irradiance;
    float3 hue=l.color/max(energy,0.0001);
    // Strongest RGB contribution avoids overlap washing out the palette.
    direct=max(direct,hue*lightBand);
    litMask=max(litMask,saturate(lightBand));
#if defined(_TOON_GLOBAL_SPECULAR) || defined(_TOON_LOCAL_STYLE)
    if (_SpecEnabled>0.5)
    {
        float3 h=SafeNormalize(l.direction+v);
        // Exactly one binary highlight per light; no pow/smoothstep lobe.
        spec=max(spec,_SpecColor.rgb*_SpecStrength*hue
            *step(saturate(_SpecThreshold),saturate(dot(n,h)))*step(0.001,lightBand));
    }
#endif
}
float Halftone(float2 pixel)
{
    float spacing=max(4,_HalftoneScale);
    float s,c;
    sincos(radians(_HalftoneAngle),s,c);
    float2 rotated=float2(c*pixel.x-s*pixel.y,s*pixel.x+c*pixel.y);
    float2 cell=frac(rotated/spacing)-0.5;
    float d=length(cell);
    float aa=max(fwidth(d),0.0001);
    return 1-smoothstep(_HalftoneRadius-aa*0.5,_HalftoneRadius+aa*0.5,d);
}
half4 ToonFragment(Varyings i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS;
#if defined(_NORMALMAP)
    inputData.normalWS=ToonNormal(i.uv,i.normalWS,i.tangentWS);
#else
    inputData.normalWS=NormalizeNormalPerPixel(i.normalWS);
#endif
    inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    float4 shadowCoord;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
#else
    // Per-fragment cascade selection is essential on large walls/floors.
    shadowCoord=TransformWorldToShadowCoord(i.positionWS);
#endif
    half4 shadowMask=half4(1,1,1,1);
    half3 gi;
#if defined(DYNAMICLIGHTMAP_ON)
    gi=SAMPLE_GI(i.lightmapUV,i.dynamicLightmapUV,i.vertexSH,inputData.normalWS);
    shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    gi=SAMPLE_GI(i.vertexSH,GetAbsolutePositionWS(i.positionWS),inputData.normalWS,
        inputData.viewDirectionWS,i.positionCS.xy,i.probeOcclusion,shadowMask);
#else
    gi=SAMPLE_GI(i.lightmapUV,i.vertexSH,inputData.normalWS);
    shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);
#endif
    float3 direct=0,spec=0;
    float litMask=0;
    Light mainLight=GetMainLight(shadowCoord,i.positionWS,shadowMask);
    MixRealtimeAndBakedGI(mainLight,inputData.normalWS,gi);
    AccumulateToonLight(mainLight,inputData.normalWS,inputData.viewDirectionWS,false,direct,spec,litMask);
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light l=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        AccumulateToonLight(l,inputData.normalWS,inputData.viewDirectionWS,false,direct,spec,litMask);
    }
#endif
    uint pixelLightCount=GetAdditionalLightsCount();
    // Forward+ returns zero above; LIGHT_LOOP_BEGIN iterates cluster membership.
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light l=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        AccumulateToonLight(l,inputData.normalWS,inputData.viewDirectionWS,true,direct,spec,litMask);
    LIGHT_LOOP_END
#endif
    float3 albedo=ToonAlbedo(i.uv);
    float3 tint=_ShadowTint.rgb;
    float3 shadowHue=tint/max(max(tint.r,max(tint.g,tint.b)),0.0001);
    float3 color=lerp((tint+shadowHue*_ToonShadowLift)*albedo,albedo,litMask);
    // Normalize out the band already used in the palette lerp; preserve light hue.
    float3 lightHue=clamp(direct/max(litMask,0.0001),0,max(1,_ToonDirectMax));
    color*=lerp(float3(1,1,1),lightHue,litMask);
    color+=albedo*PosterizeGI(gi)*saturate(_BakedInfluence);
#if defined(_TOON_HALFTONE) && (defined(_TOON_GLOBAL_HALFTONE) || defined(_TOON_LOCAL_STYLE))
    if(ToonHalftoneEnabled>0.5)
    {
        // SV_POSITION anchors phase to raster pixels, with no time/world/UV input.
        // Convert render pixels to camera output pixels for render-scale stability.
        float2 pixel=i.positionCS.xy*(_ScreenParams.xy/_ScaledScreenParams.xy);
        color=lerp(color,_HalftoneColor.rgb,Halftone(pixel)*saturate(_HalftoneStrength)
            *(1-step(0.001,litMask)));
    }
#endif
    color+=spec;
#if defined(_TOON_GLOBAL_RIM) || defined(_TOON_LOCAL_STYLE)
    if(_RimEnabled>0.5)
    {
        float rim=step(saturate(_RimThreshold),1-saturate(dot(inputData.normalWS,inputData.viewDirectionWS)));
        color+=_RimColor.rgb*_RimStrength*rim*(_RimLitOnly>0.5?step(0.001,litMask):1);
    }
#endif
    // URP fog variants are FOG_LINEAR/FOG_EXP/FOG_EXP2, not Built-in UNITY_APPLY_FOG.
    color=MixFog(color,ComputeFogFactor(TransformWorldToHClip(i.positionWS).z));
    return half4(color,1);
}
#endif
