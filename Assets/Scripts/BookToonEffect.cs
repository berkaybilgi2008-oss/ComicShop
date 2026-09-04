using UnityEngine;

/// <summary>
/// Tum BookItem renderer'larina URP toon/cell shading uygular.
/// Mevcut materyalin texture ve rengini korur.
/// </summary>
public class BookToonEffect : MonoBehaviour
{
    static Shader toonShader;

    void Awake()
    {
        ApplyToRenderers();
    }

    void ApplyToRenderers()
    {
        if (toonShader == null)
            toonShader = Shader.Find("Custom/BookToon");

        if (toonShader == null)
        {
            Debug.LogWarning("BookToonEffect: Custom/BookToon shader bulunamadi.");
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i];
                if (original == null)
                    continue;

                Texture baseTexture = null;
                Color baseColor = Color.white;

                if (original.HasProperty("_BaseMap"))
                    baseTexture = original.GetTexture("_BaseMap");
                else if (original.HasProperty("_MainTex"))
                    baseTexture = original.GetTexture("_MainTex");

                if (original.HasProperty("_BaseColor"))
                    baseColor = original.GetColor("_BaseColor");
                else if (original.HasProperty("_Color"))
                    baseColor = original.GetColor("_Color");

                original.shader = toonShader;

                if (baseTexture != null)
                    original.SetTexture("_BaseMap", baseTexture);

                original.SetColor("_BaseColor", baseColor);
            }

            renderer.materials = materials;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToBooks()
    {
        BookItem[] books = Object.FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        foreach (BookItem book in books)
        {
            if (book != null && book.GetComponent<BookToonEffect>() == null)
                book.gameObject.AddComponent<BookToonEffect>();
        }
    }
}
