#ifndef COMICSHOP_TOON_INPUT
#define COMICSHOP_TOON_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseColor, _ShadowTint, _SpecColor, _RimColor, _HalftoneColor, _EmissionColor;
float4 _WoodLit, _WoodShadow, _WallpaperLit, _WallpaperShadow;
float4 _FloorCream, _FloorGreen, _Mustard, _OrangeRed, _Sage, _Cream;
float _Palette, _PaletteShadowMix, _ShadowHueShift;
float _ShadowSteps, _RampSmoothness, _BakedSteps, _BakedInfluence;
float _SpecEnabled, _SpecSize, _RimEnabled, _RimPower, _RimThreshold, _RimLitOnly;
float _HalftoneEnabled, _HalftoneScale, _HalftoneStrength;
float _LightFalloffScale, _ShadowFloor, _DirectGain;
CBUFFER_END
// Intentionally global, never a material property. Default zero means enabled.
float _ComicHalftoneDisabled;
float3 PaletteColor()
{
    int p = (int)round(_Palette);
    if(p == 1) return _WoodLit.rgb;
    if(p == 2) return _WallpaperLit.rgb;
    if(p == 3) return _FloorCream.rgb;
    if(p == 4) return _FloorGreen.rgb;
    if(p == 5) return _Mustard.rgb;
    if(p == 6) return _OrangeRed.rgb;
    if(p == 7) return _Sage.rgb;
    if(p == 8) return _Cream.rgb;
    return 1;
}
float3 ToonAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb * _BaseColor.rgb * PaletteColor();
}
#endif
