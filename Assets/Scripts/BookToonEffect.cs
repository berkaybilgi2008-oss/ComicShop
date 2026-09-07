using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
    private bool edgesBuilt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (Material material in Materials.Values)
            if (material != null) Object.Destroy(material);
        Materials.Clear();
    }
    private void Awake()
    {
        BuildBlackEdgeLines();
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
        effect.BuildBlackEdgeLines();
    }

    private void BuildBlackEdgeLines()
    {
        Shader shader = Resources.Load<Shader>("BookCel");
        if (shader == null) return;
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.transform.name.EndsWith("_CreaseLines")) continue;
            Material[] slots = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                Material source = slots[i];
                if (source == null || source.shader == shader) continue;
                if (!Materials.TryGetValue(source, out Material cel) || cel == null)
                {
                    cel = new Material(shader) { name = source.name + "_BookCel", enableInstancing = true };
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
                }
                slots[i] = cel;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = slots;
        }
        if (!edgesBuilt)
        {
            edgesBuilt = true;
            BookEdgeLines.ApplyToBook(gameObject);
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
