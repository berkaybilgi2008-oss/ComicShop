using UnityEngine;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static Shader toonShader;

    void Awake()
    {
        ApplyToRenderers();
    }

    public static void ApplyToBook(GameObject book)
    {
        if (book == null)
            return;

        BookToonEffect effect = book.GetComponent<BookToonEffect>();
        if (effect == null)
            effect = book.AddComponent<BookToonEffect>();
        else
            effect.ApplyToRenderers();
    }

    private void ApplyToRenderers()
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
                Material material = materials[i];
                if (material == null || material.shader == toonShader)
                    continue;

                Texture texture = null;
                Color color = Color.white;

                if (material.HasProperty("_BaseMap"))
                    texture = material.GetTexture("_BaseMap");
                else if (material.HasProperty("_MainTex"))
                    texture = material.GetTexture("_MainTex");

                if (material.HasProperty("_BaseColor"))
                    color = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color"))
                    color = material.GetColor("_Color");

                material.shader = toonShader;
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
            }

            renderer.materials = materials;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToExistingBooks()
    {
        BookItem[] books = Object.FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        foreach (BookItem book in books)
            ApplyToBook(book.gameObject);
    }
}
