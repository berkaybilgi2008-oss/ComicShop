using UnityEngine;

[DisallowMultipleComponent]
public class BookEdgeLines : MonoBehaviour
{
    [Range(0.005f, 0.05f)] public float borderFraction = 0.025f;
    private MaterialPropertyBlock properties;

    public static void ApplyToBook(GameObject book)
    {
        if (book == null) return;
        var effect = book.GetComponent<BookEdgeLines>();
        if (effect == null) book.AddComponent<BookEdgeLines>();
        else effect.Rebuild();
    }
    void Awake() => Rebuild();

    [ContextMenu("Rebuild Book Crease Lines")]
    public void Rebuild()
    {
        Transform old = transform.Find("__BookCreaseLines");
        if (old != null)
        {
            old.gameObject.SetActive(false);
            foreach (MeshFilter filter in old.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
            Destroy(old.gameObject);
        }
        if (properties == null) properties = new MaterialPropertyBlock();
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.transform.name.EndsWith("_CreaseLines")) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null) continue;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 scale = filter.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 size = Vector3.Scale(bounds.size, scale);
            float middle = size.x + size.y + size.z - Mathf.Min(size.x, size.y, size.z) - Mathf.Max(size.x, size.y, size.z);
            float width = middle * borderFraction;
            Vector3 widths = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
                widths[axis] = Mathf.Max(0.000001f, Mathf.Min(width / Mathf.Max(scale[axis], 0.000001f), bounds.size[axis] * 0.22f));
            renderer.GetPropertyBlock(properties);
            properties.SetVector("_InkBoundsMin", bounds.min);
            properties.SetVector("_InkBoundsMax", bounds.max);
            properties.SetVector("_InkWidths", widths);
            renderer.SetPropertyBlock(properties);
        }
    }
}
