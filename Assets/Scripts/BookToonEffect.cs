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
        // Pastel comic treatment: stable, texture-first, and independent of scene lighting.
        // The toon shader is used only as a visual filter; no scene-light response is added.
        cel.SetFloat("_UseLocalStyle", 1f);
        cel.EnableKeyword("_TOON_LOCAL_STYLE");

        cel.SetFloat("_OverrideShadowSteps", 1f);
        cel.SetFloat("_LocalShadowSteps", 1f);
        cel.SetFloat("_OverrideRampSmoothness", 1f);
        cel.SetFloat("_LocalRampSmoothness", 1f);
        cel.SetFloat("_OverrideBakedInfluence", 1f);
        cel.SetFloat("_LocalBakedInfluence", 0f);

        cel.SetFloat("_OverrideSpecEnabled", 1f);
        cel.SetFloat("_LocalSpecEnabled", 0f);
        cel.SetFloat("_OverrideRimEnabled", 1f);
        cel.SetFloat("_LocalRimEnabled", 0f);

        cel.SetFloat("_OverrideHalftoneEnabled", 1f);
        cel.SetFloat("_LocalHalftoneEnabled", 0f);

        // Slight pastel lift keeps saturated covers softer without washing out artwork.
        Color pastel = baseColor;
        pastel.r = Mathf.Lerp(pastel.r, 1f, 0.10f);
        pastel.g = Mathf.Lerp(pastel.g, 1f, 0.10f);
        pastel.b = Mathf.Lerp(pastel.b, 1f, 0.10f);
        cel.SetColor("_BaseColor", pastel);

        cel.SetFloat("_OutlineEnabled", 1f);
        cel.SetFloat("_HalftoneEnabled", 0f);
        cel.DisableKeyword("_TOON_HALFTONE");
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
