using System.Collections.Generic;
using UnityEngine;

/// <summary>Place on one bookcase root. The sign's texture owns its publisher.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class ShelfLogoBinding : MonoBehaviour
{
    public BrandCatalog catalog;
    public Renderer logoRenderer;
    [Min(0)] public int materialIndex;
    [Tooltip("URP logo materyalindeki doku alani.")]
    public string textureProperty = "_BaseMap";

    private readonly List<Material> materials = new List<Material>();
    private MaterialPropertyBlock block;
    private int resolvedPublisher = -1;
    private Texture resolvedTexture;
    public string Status { get; private set; } = "Logo baglantisi henuz kontrol edilmedi";

    // Runtime logo edits are intentionally not an unsynchronized multiplayer action.
    // A changed/hidden sign fails closed until the scene is reloaded.
    public int PublisherID => isActiveAndEnabled && ReadLogo() == resolvedTexture
        ? resolvedPublisher : -1;

    public Texture ReadLogo()
    {
        if (logoRenderer == null || !logoRenderer.enabled ||
            !logoRenderer.gameObject.activeInHierarchy ||
            !logoRenderer.transform.IsChildOf(transform)) return null;
        if (logoRenderer is SpriteRenderer sprite) return sprite.sprite != null ? sprite.sprite.texture : null;
        logoRenderer.GetSharedMaterials(materials);
        if (materialIndex < 0 || materialIndex >= materials.Count) return null;
        var material = materials[materialIndex];
        if (material == null || string.IsNullOrEmpty(textureProperty) || !material.HasProperty(textureProperty)) return null;
        // A property-block override must agree with the visible image too.
        if (block == null) block = new MaterialPropertyBlock();
        logoRenderer.GetPropertyBlock(block, materialIndex);
        Texture texture = block.GetTexture(textureProperty);
        if (texture != null) return texture;
        // A per-material block overrides the renderer-wide block as a whole.
        if (block.isEmpty)
        {
            logoRenderer.GetPropertyBlock(block);
            texture = block.GetTexture(textureProperty);
            if (texture != null) return texture;
        }
        return material.GetTexture(textureProperty);
    }

    void OnEnable()
    {
        if (!Application.isPlaying) RefreshAll();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeScene() => RefreshAll();

    void Start()
    {
        // Includes scene objects instantiated before the first gameplay update.
        if (Application.isPlaying) RefreshAll();
    }

    public static void RefreshAll()
    {
        var bindings = FindObjectsByType<ShelfLogoBinding>(FindObjectsSortMode.None);
        foreach (var binding in bindings)
        {
            binding.resolvedTexture = binding.ReadLogo();
            binding.resolvedPublisher = binding.isActiveAndEnabled && binding.catalog != null
                ? binding.catalog.GetBrandForLogo(binding.resolvedTexture) : -1;
            binding.Status = binding.resolvedPublisher < 0
                ? "Logo yok, gorunmuyor veya BrandCatalog ile eslesmiyor"
                : binding.catalog.GetBrandName(binding.resolvedPublisher);
        }
        // Use raw candidates so both sides of a duplicate are rejected, regardless of order.
        foreach (var binding in bindings)
        {
            int id = binding.catalog != null ? binding.catalog.GetBrandForLogo(binding.resolvedTexture) : -1;
            if (!binding.isActiveAndEnabled || id < 0) continue;
            foreach (var other in bindings)
            {
                if (other == binding || !other.isActiveAndEnabled ||
                    other.gameObject.scene != binding.gameObject.scene || other.catalog == null) continue;
                if (other.catalog.GetBrandForLogo(other.resolvedTexture) != id) continue;
                binding.resolvedPublisher = -1;
                binding.Status = "Ayni yayincinin logosu birden fazla kitaplikta";
                break;
            }
        }
    }
}
