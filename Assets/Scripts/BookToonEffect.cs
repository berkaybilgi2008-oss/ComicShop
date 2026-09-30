using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    // Warm amber/sepia grade inspired by the shop interior.
    // The original cover texture stays intact underneath this material overlay.
    private static readonly Color ShopBookOverlay = new Color(1.08f, 0.78f, 0.52f, 1f);
    private const float OverlayStrength = 0.34f;
    private const float Desaturation = 0.18f;
    private const float Lift = 0.035f;

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

        cel.SetFloat("_UseLocalStyle", 1f);
        cel.EnableKeyword("_TOON_LOCAL_STYLE");

        // Keep the cover art, but grade the whole surface toward the warm
        // wood/amber palette visible throughout the shop.
        cel.SetColor("_BookPaletteOverlay", ShopBookOverlay);
        cel.SetFloat("_BookPaletteStrength", OverlayStrength);
        cel.SetFloat("_BookPaletteDesaturation", Desaturation);
        cel.SetFloat("_BookPaletteLift", Lift);

        // Warm local toon shadows so the graded cover and its shading belong
        // to the same visual world as the wooden shelves and amber lamps.
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

        // Only a small base-color lift is applied; the actual cover texture
        // remains the dominant source of detail and color.
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        s *= 0.90f;
        v *= 0.98f;
        Color harmonized = Color.HSVToRGB(h, s, v);
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
