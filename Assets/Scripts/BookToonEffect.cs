using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    // The shop is warm, muted and slightly dusty rather than high-saturation.
    // These values affect the book material only and preserve the original cover texture.
    private static readonly Color ShopWarmPalette = new Color(0.76f, 0.55f, 0.38f, 1f);
    private const float PaletteSaturation = 0.78f;
    private const float PaletteValue = 0.96f;
    private const float WarmNeutralShift = 0.14f;

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
        if (source == null || (source.shader != null && source.shader.name == "ComicShop/ToonLit"))
            return source;

        if (Materials.TryGetValue(source, out Material cached) && cached != null)
            return cached;

        Material template = Resources.Load<Material>("ComicShopToon/ToonRuntimeTemplate");
        Shader shader = ComicShop.Rendering.ToonStyleController.DefaultStyle.ToonShader;
        if (shader == null) shader = Shader.Find("ComicShop/ToonLit");
        if (shader == null) return source;

        Material cel = template != null ? new Material(template) : new Material(shader);
        cel.name = source.name + "_GlobalToon";
        cel.enableInstancing = true;

        // Local book style: the environment is warm wood + amber lighting,
        // so book shadows should not inherit a cold purple global shadow.
        cel.SetFloat("_UseLocalStyle", 1f);
        cel.EnableKeyword("_TOON_LOCAL_STYLE");

        cel.SetFloat("_OverrideShadowSteps", 1f);
        cel.SetFloat("_LocalShadowSteps", 2f);
        cel.SetFloat("_OverrideRampSmoothness", 1f);
        cel.SetFloat("_LocalRampSmoothness", 0.03f);
        cel.SetFloat("_OverrideShadowTint", 1f);
        cel.SetColor("_LocalShadowTint", new Color(0.34f, 0.24f, 0.18f, 1f));
        cel.SetFloat("_OverrideBakedInfluence", 1f);
        cel.SetFloat("_LocalBakedInfluence", 0f);

        cel.SetFloat("_OverrideSpecEnabled", 1f);
        cel.SetFloat("_LocalSpecEnabled", 0f);
        cel.SetFloat("_OverrideRimEnabled", 1f);
        cel.SetFloat("_LocalRimEnabled", 0f);

        cel.SetFloat("_OverrideHalftoneEnabled", 1f);
        cel.SetFloat("_LocalHalftoneEnabled", 0f);

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

        Color baseColor = source.HasProperty("_BaseColor")
            ? source.GetColor("_BaseColor")
            : (source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);

        // Harmonize the cover's existing texture instead of replacing it:
        // reduce saturation, keep brightness slightly soft, and warm mostly
        // neutral/gray artwork toward the shop's amber-brown palette.
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        float neutralWeight = 1f - s;
        h = Mathf.Repeat(Mathf.Lerp(h, 0.075f, neutralWeight * WarmNeutralShift), 1f);
        s *= PaletteSaturation;
        v *= PaletteValue;

        Color harmonized = Color.HSVToRGB(h, s, v);
        harmonized = Color.Lerp(harmonized, ShopWarmPalette, neutralWeight * 0.08f);
        harmonized.a = baseColor.a;

        cel.SetColor("_BaseColor", harmonized);
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
