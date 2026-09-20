using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (Material material in Materials.Values)
            if (material != null) Object.Destroy(material);
        Materials.Clear();
    }
    private void Awake()
    {
        ApplyGlobalToonMaterials();
    }

    public static void ApplyToBook(GameObject book)
    {
        if (book == null) return;
        BookToonEffect effect = book.GetComponent<BookToonEffect>();
        if (effect == null)
        {
            book.AddComponent<BookToonEffect>();
            return;
        }
        effect.ApplyGlobalToonMaterials();
    }

    private void ApplyGlobalToonMaterials()
    {
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.transform.name.EndsWith("_CreaseLines")) continue;
            Material[] slots = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                Material cel = ResolveMaterial(slots[i]);
                if (cel == slots[i]) continue;
                slots[i] = cel;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = slots;
        }
        // Books use the renderer feature. Do not attach legacy per-object ink MPBs.

    }

    public static Material ResolveMaterial(Material source)
    {
        if (source == null || (source.shader != null && source.shader.name == "ComicShop/ToonLit")) return source;
        if (Materials.TryGetValue(source, out Material cached) && cached != null) return cached;
        Material template = Resources.Load<Material>("ComicShopToon/ToonRuntimeTemplate");
        Shader shader = ComicShop.Rendering.ToonStyleController.DefaultStyle.ToonShader;
        if (shader == null) shader = Shader.Find("ComicShop/ToonLit");
        if (shader == null) return source;
        Material cel = template != null ? new Material(template) : new Material(shader);
        cel.name = source.name + "_GlobalToon";
        cel.enableInstancing = true;
        // Book-specific art direction: crisp two-step shadows, restrained halftone,
        // subtle rim/specular accents and a readable comic outline.
        cel.SetFloat("_UseLocalStyle", 1f);
        cel.EnableKeyword("_TOON_LOCAL_STYLE");
        cel.SetFloat("_OverrideShadowSteps", 1f);
        cel.SetFloat("_LocalShadowSteps", 2f);
        cel.SetFloat("_OverrideRampSmoothness", 1f);
        cel.SetFloat("_LocalRampSmoothness", 0.012f);
        cel.SetFloat("_OverrideBakedInfluence", 1f);
        cel.SetFloat("_LocalBakedInfluence", 0.12f);
        cel.SetFloat("_OverrideSpecEnabled", 1f);
        cel.SetFloat("_LocalSpecEnabled", 1f);
        cel.SetFloat("_OverrideSpecThreshold", 1f);
        cel.SetFloat("_LocalSpecThreshold", 0.93f);
        cel.SetFloat("_OverrideSpecStrength", 1f);
        cel.SetFloat("_LocalSpecStrength", 0.16f);
        cel.SetFloat("_OverrideRimEnabled", 1f);
        cel.SetFloat("_LocalRimEnabled", 1f);
        cel.SetFloat("_OverrideRimThreshold", 1f);
        cel.SetFloat("_LocalRimThreshold", 0.72f);
        cel.SetFloat("_OverrideRimStrength", 1f);
        cel.SetFloat("_LocalRimStrength", 0.12f);
        cel.SetFloat("_OverrideHalftoneEnabled", 1f);
        cel.SetFloat("_LocalHalftoneEnabled", 1f);
        cel.SetFloat("_OverrideHalftoneScale", 1f);
        cel.SetFloat("_LocalHalftoneScale", 12f);
        cel.SetFloat("_OverrideHalftoneStrength", 1f);
        cel.SetFloat("_LocalHalftoneStrength", 0.22f);
        cel.SetFloat("_OverrideHalftoneAngle", 1f);
        cel.SetFloat("_LocalHalftoneAngle", 45f);
        cel.SetFloat("_OverrideHalftoneRadius", 1f);
        cel.SetFloat("_LocalHalftoneRadius", 0.22f);
        cel.SetFloat("_OutlineEnabled", 1f);
        cel.SetFloat("_HalftoneEnabled", 1f);
        cel.EnableKeyword("_TOON_HALFTONE");
        string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        if (source.HasProperty(map))
        {
            cel.SetTexture("_BaseMap", source.GetTexture(map));
            cel.SetTextureScale("_BaseMap", source.GetTextureScale(map));
            cel.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
        }
        if (source.HasProperty("_BaseColor")) cel.SetColor("_BaseColor", source.GetColor("_BaseColor"));
        else if (source.HasProperty("_Color")) cel.SetColor("_BaseColor", source.GetColor("_Color"));
        Materials[source] = cel;
        return cel;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToExistingBooks()
    {
        BookItem[] books = Object.FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        foreach (BookItem book in books)
            ApplyToBook(book.gameObject);
    }
}
