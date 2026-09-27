using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vitrin camindan giren yapay gun batimi isigini guclendirir ve daha belirgin yapar:
///  * "Sunset Window" spot isiklari daha parlak, daha sicak ve daha genis.
///  * "Sunset Shaft" isik huzmesi hacimleri daha yogun, daha genis ve yavasca "nefes alir"
///    (havadaki toz gibi hafif titresim), boylece odanin icinde goze carpar.
/// Sahnedeki asil ayarlar degismez; oyun basladiginda calisma zamaninda uygulanir.
/// Cel shading'e dokunmaz (yalnizca isik siddeti/rengi ve huzme malzemesi).
/// </summary>
[DisallowMultipleComponent]
public sealed class SunsetBoost : MonoBehaviour
{
    const float LightBoost = 2.3f, DensityBoost = 3.2f, WidthBoost = 1.3f;

    readonly List<(Material material, float density)> shafts = new List<(Material, float)>();
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

    static void Install()
    {
        if (Object.FindFirstObjectByType<SunsetBoost>() != null) return;
        bool any = false;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (light != null && light.name.StartsWith("Sunset Window")) { any = true; break; }
        if (!any) return;
        new GameObject("Sunset Boost (runtime)").AddComponent<SunsetBoost>();
    }

    void Start()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light == null || !light.name.StartsWith("Sunset Window")) continue;
            light.intensity *= LightBoost;
            if (light.useColorTemperature) light.colorTemperature = Mathf.Min(light.colorTemperature, 2300f);
            if (light.type == LightType.Spot)
            {
                light.spotAngle = Mathf.Min(90f, light.spotAngle * 1.2f);
                light.innerSpotAngle = Mathf.Min(light.spotAngle - 5f, light.innerSpotAngle * 1.35f);
            }
            light.range *= 1.25f;
        }
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.name.StartsWith("Sunset Shaft")) continue;
            var material = renderer.material; // ornek kopya: asset degismez
            float density = material.HasProperty(DensityId) ? material.GetFloat(DensityId) * DensityBoost : 0f;
            if (material.HasProperty(DensityId)) material.SetFloat(DensityId, density);
            if (material.HasProperty(ColorId))
            {
                Color c = material.GetColor(ColorId);
                material.SetColor(ColorId, new Color(Mathf.Min(1.4f, c.r * 1.15f), c.g * 0.95f, c.b * 0.8f, c.a));
            }
            var t = renderer.transform;
            t.localScale = new Vector3(t.localScale.x * WidthBoost, t.localScale.y * WidthBoost, t.localScale.z);
            shafts.Add((material, density));
        }
    }

    void Update()
    {
        if (shafts.Count == 0) return;
        // Havadaki toz: huzme yogunlugu yavasca dalgalanir (iki farkli frekans: dogal gorunur).
        float time = Time.time;
        for (int i = 0; i < shafts.Count; i++)
        {
            var (material, density) = shafts[i];
            if (material == null) continue;
            float wave = 1f + 0.12f * Mathf.Sin(time * 0.55f + i * 1.7f) + 0.06f * Mathf.Sin(time * 1.9f + i);
            material.SetFloat(DensityId, density * wave);
        }
    }
}
