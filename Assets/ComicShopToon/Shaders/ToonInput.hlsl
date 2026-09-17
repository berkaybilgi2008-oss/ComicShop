#ifndef COMICSHOP_TOON_INPUT
#define COMICSHOP_TOON_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
// Identical unconditional layout in every pass and keyword variant.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseColor;
float _HalftoneEnabled, _OutlineEnabled, _UseLocalStyle;
float _LocalShadowSteps;
float _OverrideShadowSteps;
float _LocalRampSmoothness;
float _OverrideRampSmoothness;
float4 _LocalShadowTint;
float _OverrideShadowTint;
float _LocalBakedSteps;
float _OverrideBakedSteps;
float _LocalBakedInfluence;
float _OverrideBakedInfluence;
float _LocalLightFalloffScale;
float _OverrideLightFalloffScale;
float _LocalSpecEnabled;
float _OverrideSpecEnabled;
float _LocalSpecThreshold;
float _OverrideSpecThreshold;
float4 _LocalSpecColor;
float _OverrideSpecColor;
float _LocalSpecStrength;
float _OverrideSpecStrength;
float _LocalRimEnabled;
float _OverrideRimEnabled;
float _LocalRimThreshold;
float _OverrideRimThreshold;
float _LocalRimLitOnly;
float _OverrideRimLitOnly;
float4 _LocalRimColor;
float _OverrideRimColor;
float _LocalRimStrength;
float _OverrideRimStrength;
float _LocalHalftoneEnabled;
float _OverrideHalftoneEnabled;
float _LocalHalftoneScale;
float _OverrideHalftoneScale;
float _LocalHalftoneStrength;
float _OverrideHalftoneStrength;
float _LocalHalftoneAngle;
float _OverrideHalftoneAngle;
float _LocalHalftoneRadius;
float _OverrideHalftoneRadius;
float4 _LocalHalftoneColor;
float _OverrideHalftoneColor;
CBUFFER_END
// Global uniforms MUST NOT be material properties or UnityPerMaterial entries.
float _ToonShadowLift, _ToonBakedExposure, _ToonDirectMax;
float _ToonShadowSteps;
float _ToonRampSmoothness;
float4 _ToonShadowTint;
float _ToonBakedSteps;
float _ToonBakedInfluence;
float _ToonLightFalloffScale;
float _ToonSpecEnabled;
float _ToonSpecThreshold;
float4 _ToonSpecColor;
float _ToonSpecStrength;
float _ToonRimEnabled;
float _ToonRimThreshold;
float _ToonRimLitOnly;
float4 _ToonRimColor;
float _ToonRimStrength;
float _ToonHalftoneEnabled;
float _ToonHalftoneScale;
float _ToonHalftoneStrength;
float _ToonHalftoneAngle;
float _ToonHalftoneRadius;
float4 _ToonHalftoneColor;
// Default variant contains no override loads or runtime selection.
#if defined(_TOON_LOCAL_STYLE)
#define _ShadowSteps (_OverrideShadowSteps > 0.5 ? _LocalShadowSteps : _ToonShadowSteps)
#define _RampSmoothness (_OverrideRampSmoothness > 0.5 ? _LocalRampSmoothness : _ToonRampSmoothness)
#define _ShadowTint (_OverrideShadowTint > 0.5 ? _LocalShadowTint : _ToonShadowTint)
#define _BakedSteps (_OverrideBakedSteps > 0.5 ? _LocalBakedSteps : _ToonBakedSteps)
#define _BakedInfluence (_OverrideBakedInfluence > 0.5 ? _LocalBakedInfluence : _ToonBakedInfluence)
#define _LightFalloffScale (_OverrideLightFalloffScale > 0.5 ? _LocalLightFalloffScale : _ToonLightFalloffScale)
#define _SpecEnabled (_OverrideSpecEnabled > 0.5 ? _LocalSpecEnabled : _ToonSpecEnabled)
#define _SpecThreshold (_OverrideSpecThreshold > 0.5 ? _LocalSpecThreshold : _ToonSpecThreshold)
#define _SpecColor (_OverrideSpecColor > 0.5 ? _LocalSpecColor : _ToonSpecColor)
#define _SpecStrength (_OverrideSpecStrength > 0.5 ? _LocalSpecStrength : _ToonSpecStrength)
#define _RimEnabled (_OverrideRimEnabled > 0.5 ? _LocalRimEnabled : _ToonRimEnabled)
#define _RimThreshold (_OverrideRimThreshold > 0.5 ? _LocalRimThreshold : _ToonRimThreshold)
#define _RimLitOnly (_OverrideRimLitOnly > 0.5 ? _LocalRimLitOnly : _ToonRimLitOnly)
#define _RimColor (_OverrideRimColor > 0.5 ? _LocalRimColor : _ToonRimColor)
#define _RimStrength (_OverrideRimStrength > 0.5 ? _LocalRimStrength : _ToonRimStrength)
#define ToonHalftoneEnabled (_OverrideHalftoneEnabled > 0.5 ? _LocalHalftoneEnabled : _ToonHalftoneEnabled)
#define _HalftoneScale (_OverrideHalftoneScale > 0.5 ? _LocalHalftoneScale : _ToonHalftoneScale)
#define _HalftoneStrength (_OverrideHalftoneStrength > 0.5 ? _LocalHalftoneStrength : _ToonHalftoneStrength)
#define _HalftoneAngle (_OverrideHalftoneAngle > 0.5 ? _LocalHalftoneAngle : _ToonHalftoneAngle)
#define _HalftoneRadius (_OverrideHalftoneRadius > 0.5 ? _LocalHalftoneRadius : _ToonHalftoneRadius)
#define _HalftoneColor (_OverrideHalftoneColor > 0.5 ? _LocalHalftoneColor : _ToonHalftoneColor)
#else
#define _ShadowSteps _ToonShadowSteps
#define _RampSmoothness _ToonRampSmoothness
#define _ShadowTint _ToonShadowTint
#define _BakedSteps _ToonBakedSteps
#define _BakedInfluence _ToonBakedInfluence
#define _LightFalloffScale _ToonLightFalloffScale
#define _SpecEnabled _ToonSpecEnabled
#define _SpecThreshold _ToonSpecThreshold
#define _SpecColor _ToonSpecColor
#define _SpecStrength _ToonSpecStrength
#define _RimEnabled _ToonRimEnabled
#define _RimThreshold _ToonRimThreshold
#define _RimLitOnly _ToonRimLitOnly
#define _RimColor _ToonRimColor
#define _RimStrength _ToonRimStrength
#define ToonHalftoneEnabled _ToonHalftoneEnabled
#define _HalftoneScale _ToonHalftoneScale
#define _HalftoneStrength _ToonHalftoneStrength
#define _HalftoneAngle _ToonHalftoneAngle
#define _HalftoneRadius _ToonHalftoneRadius
#define _HalftoneColor _ToonHalftoneColor
#endif
float3 ToonAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
}
float3 ToonNormal(float2 uv, float3 normalWS, float4 tangentWS)
{
#if defined(_NORMALMAP)
    float3 n = normalize(normalWS);
    float3 t = normalize(tangentWS.xyz);
    float3 b = cross(n, t) * tangentWS.w;
    float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv));
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, float3x3(t,b,n)));
#else
    return NormalizeNormalPerPixel(normalWS);
#endif
}
#endif
