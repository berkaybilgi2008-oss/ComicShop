using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Ayarlar > Goruntu: Dusuk / Orta / Yuksek / Cok Yuksek grafik secenegi.
/// Proje URP varligi calisma zamaninda KOPYALANIR ve kopya degistirilir; boylece editorde
/// Play sirasinda secilen seviye diskteki PC_RPAsset'e yazilmaz. "Yuksek" mevcut gorunumdur.
/// Tum seviyelerde cel shading (cizgiler, sert bantlar, lamba golgeleri) ayni kalir.
/// </summary>
public static class GraphicsQuality
{
    public const int Low = 0, Medium = 1, High = 2, VeryHigh = 3;
    public static readonly string[] LocKeys =
        { "settings.gfx.low", "settings.gfx.medium", "settings.gfx.high", "settings.gfx.ultra" };

    static UniversalRenderPipelineAsset source, runtime;
    static float baseShadowDistance;
    static int baseCascades, baseMsaa;
    static float baseRenderScale;
    static int baseMainShadowRes = 2048, baseAdditionalShadowRes = 2048;
    static readonly Dictionary<Light, LightShadows> lightShadows = new Dictionary<Light, LightShadows>();
    static bool hooked, applied;

    public static int Level { get; private set; } = High;
    public static bool BooksCastShadows => Level >= High;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        source = runtime = null; hooked = applied = false; lightShadows.Clear(); Level = High;
    }

    public static void Apply(int level)
    {
        level = Mathf.Clamp(level, Low, VeryHigh);
        // ShopSettings.Apply her kaydirici hareketinde cagrilir; seviye degismediyse is yapma.
        if (applied && level == Level) return;
        applied = true;
        Level = level;
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) => ApplyScene();
        }
        ApplyPipeline();
        ApplyScene();
    }

    static void ApplyPipeline()
    {
        if (runtime == null)
        {
            source = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (source == null) return;
            runtime = Object.Instantiate(source);
            runtime.name = source.name + " (Runtime Quality)";
            baseShadowDistance = source.shadowDistance;
            baseCascades = source.shadowCascadeCount;
            baseMsaa = source.msaaSampleCount;
            baseRenderScale = source.renderScale;
            baseMainShadowRes = source.mainLightShadowmapResolution;
            baseAdditionalShadowRes = source.additionalLightsShadowmapResolution;
            QualitySettings.renderPipeline = runtime;
            if (GraphicsSettings.defaultRenderPipeline == source) GraphicsSettings.defaultRenderPipeline = runtime;
        }
        // Cel shading korunur: cozunurluk (renderScale) HER seviyede ayni kalir, cunku ekran-uzayi
        // cizgi (ScreenSpaceOutline) ve sert isik bantlari piksel yogunluguna baglidir. Doku
        // kalitesi (mip limit) de dusurulmez; renk/ramp bantlari bulaniklasmaz. Lamba golgeleri
        // hic kapanmaz. Seviyeler yalnizca golge mesafesi/cozunurlugu, kenar yumusatma, anizotropik
        // filtre, LOD ve yerdeki binlerce kitabin golge dusurmesi ile ayrisir.
        runtime.renderScale = baseRenderScale;
        QualitySettings.globalTextureMipmapLimit = 0;
        switch (Level)
        {
            case Low:
                runtime.msaaSampleCount = 1;
                runtime.shadowDistance = Mathf.Min(baseShadowDistance, 25f);
                runtime.shadowCascadeCount = Mathf.Min(2, Mathf.Max(1, baseCascades));
                runtime.mainLightShadowmapResolution = Mathf.Min(baseMainShadowRes, 1024);
                // Lamba golge atlasi kucultulmez: 20+ lamba golgesi 512'lik atlasa sigmayinca golgeler
                // bulaniklasip cel bantlari bozuluyordu.
                runtime.additionalLightsShadowmapResolution = baseAdditionalShadowRes;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.lodBias = 0.7f;
                break;
            case Medium:
                runtime.msaaSampleCount = 1;
                runtime.shadowDistance = Mathf.Min(baseShadowDistance, 40f);
                runtime.shadowCascadeCount = Mathf.Min(2, Mathf.Max(1, baseCascades));
                runtime.mainLightShadowmapResolution = baseMainShadowRes;
                runtime.additionalLightsShadowmapResolution = baseAdditionalShadowRes;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.lodBias = 1f;
                break;
            case High:
                runtime.msaaSampleCount = baseMsaa;
                runtime.shadowDistance = baseShadowDistance;
                runtime.shadowCascadeCount = baseCascades;
                runtime.mainLightShadowmapResolution = baseMainShadowRes;
                runtime.additionalLightsShadowmapResolution = baseAdditionalShadowRes;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.lodBias = 1.5f;
                break;
            default:
                runtime.msaaSampleCount = 4;
                runtime.shadowDistance = baseShadowDistance * 1.35f;
                runtime.shadowCascadeCount = 4;
                runtime.mainLightShadowmapResolution = Mathf.Max(baseMainShadowRes, 4096);
                runtime.additionalLightsShadowmapResolution = Mathf.Max(baseAdditionalShadowRes, 4096);
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                QualitySettings.lodBias = 2f;
                break;
        }
    }

    static void ApplyScene()
    {
        // Eski surumde Dusuk seviye lamba golgelerini kapatiyordu; cel gorunumu bozdugu icin artik
        // tum isiklar sahnedeki orijinal golge ayarina geri doner.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light == null) continue;
            if (!lightShadows.TryGetValue(light, out var original))
                lightShadows[light] = original = light.shadows;
            light.shadows = original;
        }
        foreach (var book in BookItem.Active) ApplyToBook(book);
    }

    /// <summary>Yerdeki binlerce kitap Dusuk/Orta seviyede golge haritasina cizilmez.</summary>
    public static void ApplyToBook(BookItem book)
    {
        if (book == null) return;
        var mode = BooksCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        foreach (var renderer in book.GetComponentsInChildren<Renderer>(true))
            if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly) renderer.shadowCastingMode = mode;
    }
}
