using UnityEngine;

[DisallowMultipleComponent]
public class BookToonEffect : MonoBehaviour
{
    private static Shader toonShader;
    private static Shader outlineShader;
    private const string OutlineRootName = "__BookBlackOutline";

    private void Awake()
    {
        ApplyToRenderers();
        BuildBlackOutlines();
    }

    public static void ApplyToBook(GameObject book)
    {
        if (book == null)
            return;

        BookToonEffect effect = book.GetComponent<BookToonEffect>();
        if (effect == null)
        {
            effect = book.AddComponent<BookToonEffect>();
            return;
        }

        effect.ApplyToRenderers();
        effect.BuildBlackOutlines();
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
            if (renderer == null || renderer.transform.name == OutlineRootName || renderer.GetComponentInParent<BookEdgeLines>() != null)
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

    private void BuildBlackOutlines()
    {
        if (outlineShader == null)
            outlineShader = Shader.Find("Custom/OutlineOnly");

        if (outlineShader == null)
        {
            Debug.LogWarning("BookToonEffect: Custom/OutlineOnly shader bulunamadi.");
            return;
        }

        Transform old = transform.Find(OutlineRootName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject root = new GameObject(OutlineRootName);
        root.transform.SetParent(transform, false);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer source in renderers)
        {
            if (source == null || source.transform == root.transform || source.transform.IsChildOf(root.transform))
                continue;

            MeshRenderer meshRenderer = source as MeshRenderer;
            MeshFilter meshFilter = source.GetComponent<MeshFilter>();
            if (meshRenderer != null && meshFilter != null && meshFilter.sharedMesh != null)
            {
                GameObject outline = new GameObject(source.gameObject.name + "_BlackOutline");
                outline.transform.SetParent(root.transform, false);
                outline.transform.position = source.transform.position;
                outline.transform.rotation = source.transform.rotation;
                outline.transform.localScale = source.transform.lossyScale * 1.045f;

                MeshFilter filter = outline.AddComponent<MeshFilter>();
                filter.sharedMesh = meshFilter.sharedMesh;
                MeshRenderer renderer = outline.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GetBlackOutlineMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                continue;
            }

            SkinnedMeshRenderer skinned = source as SkinnedMeshRenderer;
            if (skinned != null && skinned.sharedMesh != null)
            {
                GameObject outline = new GameObject(source.gameObject.name + "_BlackOutline");
                outline.transform.SetParent(root.transform, false);
                outline.transform.position = source.transform.position;
                outline.transform.rotation = source.transform.rotation;
                outline.transform.localScale = source.transform.lossyScale * 1.045f;

                SkinnedMeshRenderer renderer = outline.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = skinned.sharedMesh;
                renderer.bones = skinned.bones;
                renderer.rootBone = skinned.rootBone;
                renderer.sharedMaterial = GetBlackOutlineMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private static Material blackOutlineMaterial;

    private static Material GetBlackOutlineMaterial()
    {
        if (blackOutlineMaterial == null)
        {
            Shader shader = Shader.Find("Custom/OutlineOnly");
            if (shader == null)
                return null;

            blackOutlineMaterial = new Material(shader);
            blackOutlineMaterial.name = "BookBlackOutline_Runtime";
            blackOutlineMaterial.SetColor("_Color", Color.black);
            if (blackOutlineMaterial.HasProperty("_OffsetFactor"))
                blackOutlineMaterial.SetFloat("_OffsetFactor", 1f);
            if (blackOutlineMaterial.HasProperty("_OffsetUnits"))
                blackOutlineMaterial.SetFloat("_OffsetUnits", 2f);
        }
        return blackOutlineMaterial;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToExistingBooks()
    {
        BookItem[] books = Object.FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        foreach (BookItem book in books)
            ApplyToBook(book.gameObject);
    }
}
